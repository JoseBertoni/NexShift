using System.Text.RegularExpressions;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NexShift.Core.Interfaces;
using Polly;
using Polly.Retry;

namespace NexShift.Infrastructure.Services.Migrator;

public class ClaudeCodeTransformer : ICodeTransformer
{
    private readonly AnthropicClient _client;
    private readonly ILogger<ClaudeCodeTransformer> _logger;
    private readonly ITransformationRuleRepository _repository;
    private readonly ResiliencePipeline _pipeline;

    public ClaudeCodeTransformer(
        IConfiguration configuration,
        ILogger<ClaudeCodeTransformer> logger,
        ITransformationRuleRepository repository)
    {
        _logger = logger;
        _repository = repository;

        var apiKey = configuration["Anthropic:ApiKey"]
                  ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                  ?? throw new InvalidOperationException("Anthropic API key not configured");

        _client = new AnthropicClient(apiKey);

        // Polly: timeout 45s por intento + 3 reintentos con backoff exponencial (2s, 4s, 8s)
        _pipeline = new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromSeconds(45))
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Reintento {Attempt}/3 llamando a Claude — {Exception}",
                        args.AttemptNumber + 1, args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    // ── Pre-load rules into cache ONCE before processing the file loop ────
    public async Task WarmUpAsync(CancellationToken cancellationToken = default)
    {
        await _repository.GetAllActiveAsync(cancellationToken);
        _logger.LogInformation("TransformationRules pre-loaded into cache");
    }

    public async Task<CodeTransformResult> TransformAsync(
        CodeTransformRequest request,
        CancellationToken cancellationToken = default)
    {
        // Rules are already in cache after WarmUpAsync — this is near-instant
        var rules = await _repository.GetAllActiveAsync(cancellationToken);

        var pureRules = rules.Where(r => !r.NeedsAI).OrderBy(r => r.Priority).ToList();
        var aiRules = rules.Where(r => r.NeedsAI).ToList();

        // ── Step 1: Apply pure regex/replace rules ────────────────────────
        var (transformedCode, pureChanges) = ApplyPureRules(request.OriginalCode, pureRules);

        // ── Step 2: Check if any AI rule matches ──────────────────────────
        var matchedAiRules = aiRules
            .Where(r => transformedCode.Contains(r.Pattern, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var result = new CodeTransformResult();

        if (matchedAiRules.Any())
        {
            // ── Step 3: Send to Claude only if needed ─────────────────────
            _logger.LogInformation(
                "Sending {File} to Claude — matched AI rules: {Rules}",
                request.FilePath,
                string.Join(", ", matchedAiRules.Select(r => r.Description)));

            var claudeRequest = new CodeTransformRequest
            {
                FilePath = request.FilePath,
                OriginalCode = transformedCode,
                SourceFramework = request.SourceFramework,
                TargetFramework = request.TargetFramework,
                DeprecatedPackagesFound = request.DeprecatedPackagesFound
            };

            result = await TransformWithClaudeAsync(claudeRequest, matchedAiRules, cancellationToken);
            result.WasAiTransformed = true;

            // Merge pure changes into Claude result
            result.ChangesApplied.InsertRange(0, pureChanges);
        }
        else if (pureChanges.Any())
        {
            // ── Pure rules were enough — no Claude needed ─────────────────
            _logger.LogInformation(
                "File {File} transformed with pure rules only — Claude skipped",
                request.FilePath);

            result.TransformedCode = transformedCode;
            result.WasModified = true;
            result.ChangesApplied = pureChanges;
        }
        else
        {
            // ── No changes needed ─────────────────────────────────────────
            _logger.LogDebug("File {File} — no transformation needed", request.FilePath);

            result.TransformedCode = request.OriginalCode;
            result.WasModified = false;
        }

        return result;
    }

    // ── Pure rules: simple string replace or regex ────────────────────────
    private static (string code, List<string> changes) ApplyPureRules(
        string code,
        IEnumerable<Core.Entities.TransformationRule> rules)
    {
        var changes = new List<string>();

        foreach (var rule in rules)
        {
            if (rule.Replacement == null) continue;

            bool matched;

            if (rule.IsRegex)
            {
                var before = code;
                code = Regex.Replace(code, rule.Pattern, rule.Replacement);
                matched = code != before;
            }
            else
            {
                matched = code.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase);
                if (matched)
                    code = code.Replace(rule.Pattern, rule.Replacement, StringComparison.OrdinalIgnoreCase);
            }

            if (matched)
                changes.Add(rule.Description);
        }

        return (code, changes);
    }

    // ── Claude: only called when AI rules match ───────────────────────────
    private async Task<CodeTransformResult> TransformWithClaudeAsync(
        CodeTransformRequest request,
        List<Core.Entities.TransformationRule> matchedRules,
        CancellationToken cancellationToken)
    {
        var prompt = BuildPrompt(request, matchedRules);

        try
        {
            var message = await _pipeline.ExecuteAsync(async ct =>
                await _client.Messages.GetClaudeMessageAsync(new MessageParameters
                {
                    Model = "claude-sonnet-4-5",
                    MaxTokens = 8000,
                    Messages = new List<Message>
                    {
                        new Message(RoleType.User, prompt)
                    }
                }, ct),
            cancellationToken);

            var response = message.Content.FirstOrDefault()?.ToString() ?? string.Empty;
            return ParseResponse(response, request.OriginalCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transformando {File} con Claude (3 reintentos agotados)", request.FilePath);
            return new CodeTransformResult
            {
                TransformedCode = AddTodoComment(request.OriginalCode, "Automatic transformation error — review manually"),
                WasModified = false,
                ManualReviewRequired = new List<string> { $"Error: {ex.Message}" }
            };
        }
    }

    private static string BuildPrompt(
        CodeTransformRequest request,
        List<Core.Entities.TransformationRule> matchedRules)
    {
        var rulesDescription = string.Join("\n", matchedRules.Select(r => $"- {r.Description}"));

        var deprecatedList = request.DeprecatedPackagesFound.Any()
            ? $"\nDeprecated packages found: {string.Join(", ", request.DeprecatedPackagesFound)}"
            : string.Empty;

        return $"""
            You are a .NET migration expert. Migrate the following C# file from {request.SourceFramework} to {request.TargetFramework}.

            File: {request.FilePath}{deprecatedList}

            Patterns detected that require AI transformation:
            {rulesDescription}

            STRICT RULES:
            1. Return ONLY the migrated C# code — no explanations, no markdown, no code blocks
            2. Replace HttpContext.Current with IHttpContextAccessor injected via constructor
            3. Replace GlobalConfiguration with ASP.NET Core equivalent in Program.cs
            4. Replace System.Drawing with SkiaSharp or ImageSharp equivalents
            5. Replace Microsoft.Owin middleware with ASP.NET Core middleware
            6. Update all necessary using statements
            7. Where you cannot migrate automatically, add: // TODO: NEXSHIFT - [description]
            8. Keep all business logic intact — only migrate infrastructure
            9. Do NOT modify lines that were already transformed before this step

            ORIGINAL CODE:
            {request.OriginalCode}
            """;
    }

    private static CodeTransformResult ParseResponse(string response, string originalCode)
    {
        var result = new CodeTransformResult();

        var code = response.Trim();
        if (code.StartsWith("```csharp")) code = code[9..];
        if (code.StartsWith("```")) code = code[3..];
        if (code.EndsWith("```")) code = code[..^3];
        code = code.Trim();

        result.TransformedCode = code;
        result.WasModified = code != originalCode;

        // Detect TODOs left by Claude for manual review
        result.ManualReviewRequired = code
            .Split('\n')
            .Where(l => l.Contains("// TODO: NEXSHIFT"))
            .Select(l => l.Trim())
            .ToList();

        // Detect changes applied by Claude
        if (code.Contains("IHttpContextAccessor") && !originalCode.Contains("IHttpContextAccessor"))
            result.ChangesApplied.Add("HttpContext.Current → IHttpContextAccessor");
        if (code.Contains("ControllerBase") && !originalCode.Contains("ControllerBase"))
            result.ChangesApplied.Add("ApiController → ControllerBase");
        if (code.Contains("IActionResult") && !originalCode.Contains("IActionResult"))
            result.ChangesApplied.Add("ActionResult → IActionResult");
        if (code.Contains("IConfiguration") && !originalCode.Contains("IConfiguration"))
            result.ChangesApplied.Add("WebConfigurationManager → IConfiguration");
        if (!code.Contains("using System.Web") && originalCode.Contains("using System.Web"))
            result.ChangesApplied.Add("System.Web usings removed");

        return result;
    }

    private static string AddTodoComment(string code, string message) =>
        $"// TODO: NEXSHIFT - {message}\n{code}";
}