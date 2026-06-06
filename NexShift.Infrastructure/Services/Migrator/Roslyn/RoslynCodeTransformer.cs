using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using NexShift.Core.Interfaces;
using NexShift.Infrastructure.Services.Migrator.Roslyn.Rewriters;

namespace NexShift.Infrastructure.Services.Migrator.Roslyn;

/// <summary>
/// Deterministic, AST-based code transformer using the Roslyn compiler API.
/// Replaces ClaudeCodeTransformer — zero external API calls, zero token costs,
/// fully local and auditable. Safe for on-premise enterprise deployments.
///
/// Pipeline per file:
///   1. Parse source → SyntaxTree + CompilationUnit
///   2. Apply pure regex/replace rules from DB (same as before)
///   3. Apply Roslyn rewriters in priority order
///   4. Inject missing constructor dependencies
///   5. Normalize using directives
///   6. Format output with Roslyn's normalizer
///   7. Return CodeTransformResult with full change log
/// </summary>
public sealed class RoslynCodeTransformer : ICodeTransformer
{
    private readonly ITransformationRuleRepository _repository;
    private readonly ILogger<RoslynCodeTransformer> _logger;

    public RoslynCodeTransformer(
        ITransformationRuleRepository repository,
        ILogger<RoslynCodeTransformer> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Pre-loads transformation rules into memory cache.
    /// Call once before processing a batch of files.
    /// </summary>
    public async Task WarmUpAsync(CancellationToken cancellationToken = default)
    {
        await _repository.GetAllActiveAsync(cancellationToken);
        _logger.LogInformation("RoslynCodeTransformer: rules pre-loaded into cache");
    }

    public async Task<CodeTransformResult> TransformAsync(
        CodeTransformRequest request,
        CancellationToken cancellationToken = default)
    {
        // Only process C# files
        if (!request.FilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            return Unchanged(request.OriginalCode);

        var rules = await _repository.GetAllActiveAsync(cancellationToken);
        var pureRules = rules.Where(r => !r.NeedsAI).OrderBy(r => r.Priority).ToList();

        // ── Step 1: Apply pure regex/string-replace rules from DB ─────────
        var (afterPureRules, pureChanges) = ApplyPureRules(request.OriginalCode, pureRules);

        // ── Step 2: Parse into Roslyn AST ─────────────────────────────────
        var tree = CSharpSyntaxTree.ParseText(afterPureRules,
            new CSharpParseOptions(LanguageVersion.CSharp12));

        var root = await tree.GetRootAsync(cancellationToken);
        var compilationUnit = (Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax)root;

        // ── Step 3: Apply Roslyn rewriters ────────────────────────────────
        var changes = new List<string>(pureChanges);
        var requiredSymbols = new List<string>();

        var httpRewriter = new HttpContextRewriter();
        compilationUnit = (Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax)
            httpRewriter.Visit(compilationUnit);

        if (httpRewriter.WasModified)
        {
            changes.Add("HttpContext.Current → _httpContextAccessor.HttpContext!");
            changes.Add("Removed: using System.Web");
            requiredSymbols.Add("IHttpContextAccessor");
        }

        var controllerRewriter = new ControllerRewriter();
        compilationUnit = (Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax)
            controllerRewriter.Visit(compilationUnit);

        if (controllerRewriter.WasModified)
        {
            changes.Add("ApiController → ControllerBase");
            changes.Add("ActionResult → IActionResult");
            requiredSymbols.Add("ControllerBase");
            requiredSymbols.Add("IActionResult");
        }

        var configRewriter = new ConfigurationRewriter();
        compilationUnit = (Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax)
            configRewriter.Visit(compilationUnit);

        if (configRewriter.WasModified)
        {
            changes.Add("WebConfigurationManager/ConfigurationManager → IConfiguration");
            requiredSymbols.Add("IConfiguration");
        }

        // ── Step 4: Inject missing constructor dependencies ───────────────
        var injections = new List<ConstructorInjector.Injection>();

        if (httpRewriter.NeedsAccessorInjection)
            injections.Add(new("IHttpContextAccessor", "_httpContextAccessor", "httpContextAccessor"));

        if (configRewriter.NeedsConfigurationInjection)
            injections.Add(new("IConfiguration", "_configuration", "configuration"));

        if (injections.Count > 0)
        {
            var injector = new ConstructorInjector(injections);
            compilationUnit = (Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax)
                injector.Visit(compilationUnit);

            if (injector.WasModified)
                changes.Add($"Constructor injection added: {string.Join(", ", injections.Select(i => i.InterfaceType))}");
        }

        // ── Step 5: Add missing using directives ──────────────────────────
        compilationUnit = UsingNormalizer.AddMissingUsings(compilationUnit, requiredSymbols);

        // ── Step 6: Format & normalize whitespace ─────────────────────────
        var transformed = compilationUnit
            .NormalizeWhitespace()
            .ToFullString();

        // ── Step 7: Detect remaining manual-review patterns ───────────────
        var manualReview = DetectManualReviewPatterns(transformed);

        var wasModified = transformed != request.OriginalCode || changes.Count > 0;

        if (wasModified)
            _logger.LogInformation("Transformed {File} — {ChangeCount} changes, {ManualCount} manual items",
                request.FilePath, changes.Count, manualReview.Count);
        else
            _logger.LogDebug("{File} — no transformation needed", request.FilePath);

        return new CodeTransformResult
        {
            TransformedCode    = wasModified ? transformed : request.OriginalCode,
            WasModified        = wasModified,
            WasAiTransformed   = false, // never
            ChangesApplied     = changes,
            ManualReviewRequired = manualReview
        };
    }

    // ── Pure rules: regex or string-replace from the DB ───────────────────
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
                code = System.Text.RegularExpressions.Regex.Replace(code, rule.Pattern, rule.Replacement);
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

    // ── Patterns that Roslyn cannot safely auto-migrate ───────────────────
    private static readonly string[] ManualPatterns =
    [
        "BinaryFormatter",
        "Thread.Abort",
        "AppDomain.CurrentDomain",
        "System.Runtime.Remoting",
        "FormsAuthentication",
        "System.Drawing",
        "Microsoft.Owin",
        "System.Web.UI",   // Web Forms — Etapa 2
        "Global.asax",
    ];

    private static List<string> DetectManualReviewPatterns(string code)
    {
        return ManualPatterns
            .Where(p => code.Contains(p, StringComparison.OrdinalIgnoreCase))
            .Select(p => $"// TODO: NEXSHIFT — Manual migration required: {p}")
            .ToList();
    }

    private static CodeTransformResult Unchanged(string code) => new()
    {
        TransformedCode      = code,
        WasModified          = false,
        WasAiTransformed     = false,
        ChangesApplied       = [],
        ManualReviewRequired = []
    };
}
