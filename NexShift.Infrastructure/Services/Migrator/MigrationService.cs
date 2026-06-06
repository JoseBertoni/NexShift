using Microsoft.Extensions.Logging;
using NexShift.Core.Entities;
using NexShift.Core.Interfaces;

namespace NexShift.Infrastructure.Services.Migrator;

public class MigrationService : IMigrationService
{
    private readonly IGitHubService _gitHub;
    private readonly ILogger<MigrationService> _logger;
    private readonly ICodeTransformer _codeTransformer;
    private readonly ITransformationRuleRepository _transformationRules;
    private readonly IKnownDeprecatedPackageRepository _knownPackages;
    private readonly CsprojTransformer _csprojTransformer = new();
    private readonly WebConfigTransformer _webConfigTransformer = new();
    private readonly GlobalAsaxTransformer _globalAsaxTransformer = new();
    private readonly ZipBuilder _zipBuilder = new();
    private readonly IBacklogDetector _backlogDetector;

    // Patrones de alto riesgo — cuando aparecen el cambio es HighRisk
    private static readonly HashSet<string> HighRiskPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "FormsAuthentication", "WindowsIdentity", "BinaryFormatter",
        "Thread.Abort", "AppDomain", "System.Runtime.Remoting",
        "DllImport", "ComVisible", "ServiceContract", "OperationContract"
    };

    public MigrationService(
        IGitHubService gitHub,
        ILogger<MigrationService> logger,
        ICodeTransformer codeTransformer,
        IBacklogDetector backlogDetector,
        ITransformationRuleRepository transformationRules,
        IKnownDeprecatedPackageRepository knownPackages)
    {
        _gitHub = gitHub;
        _logger = logger;
        _codeTransformer = codeTransformer;
        _backlogDetector = backlogDetector;
        _transformationRules = transformationRules;
        _knownPackages = knownPackages;
    }

    public async Task<MigrationResult> MigrateAsync(
        string repoUrl,
        string targetFramework = "net8.0",
        Dictionary<string, string>? decisions = null,
        CancellationToken cancellationToken = default)
    {
        decisions ??= new Dictionary<string, string>();
        _logger.LogInformation("Iniciando migración de {Url} a {Framework}", repoUrl, targetFramework);

        var result = new MigrationResult();
        var outputFiles = new Dictionary<string, string>();
        var allChanges = new List<string>();
        var lockObj = new object();

        // Tracked for scoring
        var cleanCsFiles = 0;  // .cs files with no legacy patterns at all
        var totalCsProcessed = 0;
        var csprojMigrated = 0;
        var totalPackages = 0;
        var deprecatedPackages = 0;

        try
        {
            // ─── 1. Tree del repo ────────────────────────────────────────────
            var tree = await _gitHub.GetRepositoryTreeAsync(repoUrl, cancellationToken);

            _logger.LogInformation("Tree obtenido: {CsFiles} .cs, {Csproj} .csproj",
                tree.CsFiles.Count, tree.CsprojFiles.Count);

            // ─── 2. packages.config → diccionario por proyecto ───────────────
            var packagesByProject = new Dictionary<string, List<string>>();

            foreach (var configPath in tree.PackagesConfigFiles
                .Where(p => p.EndsWith("packages.config")))
            {
                try
                {
                    var content = await _gitHub.GetFileContentAsync(repoUrl, configPath, cancellationToken);
                    var packages = ParsePackageNames(content);
                    var projectDir = Path.GetDirectoryName(configPath)?.Replace("\\", "/") ?? string.Empty;
                    packagesByProject[projectDir] = packages;
                    _logger.LogInformation("packages.config: {Path} ({Count} packages)", configPath, packages.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo parsear {File}", configPath);
                }
            }

            // ─── 2.5. Cargar known packages en el transformer ────────────────
            var knownPkgs = await _knownPackages.GetAllActiveAsync(cancellationToken);
            _csprojTransformer.SetKnownPackages(knownPkgs);

            // ─── 3. .csproj legacy → SDK Style ───────────────────────────────
            foreach (var csprojPath in tree.CsprojFiles)
            {
                try
                {
                    var content = await _gitHub.GetFileContentAsync(repoUrl, csprojPath, cancellationToken);
                    var projectDir = Path.GetDirectoryName(csprojPath)?.Replace("\\", "/") ?? string.Empty;
                    var packages = packagesByProject.TryGetValue(projectDir, out var pkgs)
                        ? pkgs : new List<string>();

                    var (transformed, changes) = _csprojTransformer.Transform(content, targetFramework, packages);
                    outputFiles[csprojPath] = transformed;

                    // Score: count migrated csproj (TargetFramework updated)
                    if (transformed.Contains($"<TargetFramework>{targetFramework}</TargetFramework>"))
                        csprojMigrated++;

                    // Score: count packages
                    totalPackages += packages.Count;
                    deprecatedPackages += changes.Count(c => c.StartsWith("Reemplazado:"));

                    foreach (var change in changes)
                        allChanges.Add($"[{csprojPath}] {change}");

                    result.Changes.AddRange(changes.Select(c => new MigrationChange
                    {
                        FilePath = csprojPath,
                        Type = ChangeType.Modified,
                        Description = c,
                        Category = MigrationCategory.Automated
                    }));

                    _logger.LogInformation("✅ Transformado: {File} ({N} cambios)", csprojPath, changes.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo transformar {File}", csprojPath);
                }
            }

            // ─── 4. Web.config → appsettings.json ────────────────────────────
            var webConfigPath = tree.WebConfigFiles.FirstOrDefault();
            if (webConfigPath != null)
            {
                try
                {
                    var content = await _gitHub.GetFileContentAsync(repoUrl, webConfigPath, cancellationToken);
                    var (appsettings, changes) = _webConfigTransformer.Transform(content);

                    var dir = Path.GetDirectoryName(webConfigPath)?.Replace("\\", "/") ?? string.Empty;
                    var appsettingsPath = string.IsNullOrEmpty(dir)
                        ? "appsettings.json"
                        : $"{dir}/appsettings.json";

                    outputFiles[appsettingsPath] = appsettings;

                    foreach (var change in changes)
                        allChanges.Add($"[Web.config] {change}");

                    result.Changes.AddRange(changes.Select(c => new MigrationChange
                    {
                        FilePath = appsettingsPath,
                        Type = ChangeType.Created,
                        Description = c,
                        Category = MigrationCategory.Automated
                    }));

                    _logger.LogInformation("✅ Web.config → {Path}", appsettingsPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo transformar Web.config");
                }
            }

            // ─── 5. Global.asax → Program.cs ─────────────────────────────────
            var globalAsaxPath = tree.AllFiles.FirstOrDefault(f =>
                f.EndsWith("Global.asax.cs", StringComparison.OrdinalIgnoreCase));

            string? globalAsaxContent = null;
            if (globalAsaxPath != null)
            {
                try
                {
                    globalAsaxContent = await _gitHub.GetFileContentAsync(
                        repoUrl, globalAsaxPath, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo leer Global.asax.cs");
                }
            }

            var (programCs, globalChanges) = _globalAsaxTransformer.Transform(globalAsaxContent);
            var programCsPath = tree.CsprojFiles.Count == 1
                ? Path.GetDirectoryName(tree.CsprojFiles[0])?.Replace("\\", "/") + "/Program.cs"
                : "Program.cs";

            outputFiles[programCsPath ?? "Program.cs"] = programCs;

            foreach (var change in globalChanges)
                allChanges.Add($"[Global.asax] {change}");

            result.Changes.AddRange(globalChanges.Select(c => new MigrationChange
            {
                FilePath = programCsPath ?? "Program.cs",
                Type = ChangeType.Created,
                Description = c,
                Category = MigrationCategory.Automated
            }));

            // ─── 6. Archivos .cs → BacklogDetector + Claude ──────────────────
            var deprecatedNames = result.Changes
                .Where(c => c.Description.StartsWith("Reemplazado:"))
                .Select(c => c.Description)
                .ToList();

            var transformedCount = 0;
            var copiedCount = 0;
            var skippedCount = 0;

            _logger.LogInformation("Procesando {Count} archivos .cs...", tree.CsFiles.Count);

            // Pre-load rules into cache ONCE before the loop
            await _codeTransformer.WarmUpAsync(cancellationToken);

            // Semaphore: max 5 concurrent GitHub fetches + transformations
            var semaphore = new SemaphoreSlim(5, 5);

            var csTasks = tree.CsFiles.Select(async csFile =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    if (outputFiles.ContainsKey(csFile))
                    {
                        lock (lockObj) skippedCount++;
                        return;
                    }

                    var content = await _gitHub.GetFileContentAsync(repoUrl, csFile, cancellationToken);

                    // BacklogDetector: scan all files
                    var backlogItems = await _backlogDetector.DetectAsync(csFile, content, cancellationToken);
                    lock (lockObj) result.BacklogItems.AddRange(backlogItems);

                    // Let ClaudeCodeTransformer decide via rules — no pre-check needed
                    var transformRequest = new CodeTransformRequest
                    {
                        FilePath = csFile,
                        OriginalCode = content,
                        SourceFramework = "net45",
                        TargetFramework = targetFramework,
                        DeprecatedPackagesFound = deprecatedNames
                    };

                    var transformResult = await _codeTransformer.TransformAsync(transformRequest, cancellationToken);

                    lock (lockObj)
                    {
                        outputFiles[csFile] = transformResult.TransformedCode;
                        totalCsProcessed++;

                        if (transformResult.WasModified)
                        {
                            foreach (var change in transformResult.ChangesApplied)
                                allChanges.Add($"[{csFile}] {change}");

                            result.Changes.AddRange(transformResult.ChangesApplied.Select(c => new MigrationChange
                            {
                                FilePath = csFile,
                                Type = ChangeType.Modified,
                                Description = c,
                                Category = MigrationCategory.Automated,
                                Confidence = DetermineConfidence(c, transformResult.WasAiTransformed)
                            }));

                            // Guardar diff para el reporte (máximo 30 archivos)
                            if (result.FileDiffs.Count < 30)
                                result.FileDiffs[csFile] = new FileDiff(content, transformResult.TransformedCode);

                            foreach (var manual in transformResult.ManualReviewRequired)
                                allChanges.Add($"[MANUAL] {csFile}: {manual}");

                            result.BacklogItems.AddRange(transformResult.ManualReviewRequired.Select(m =>
                                new BacklogItem
                                {
                                    FilePath = csFile,
                                    Category = BacklogCategory.ManualRequired,
                                    Title = "Manual review required by AI",
                                    Description = m,
                                    Reason = "Claude detected a pattern it cannot transform automatically"
                                }));

                            if (transformResult.ChangesApplied.Any())
                                _logger.LogInformation(
                                    "✅ Transformed: {File} ({Changes} changes, {Manual} TODOs)",
                                    csFile, transformResult.ChangesApplied.Count,
                                    transformResult.ManualReviewRequired.Count);

                            transformedCount++;
                        }
                        else
                        {
                            // File had no legacy patterns → count as clean for scoring
                            if (!result.BacklogItems.Any(b => b.FilePath == csFile))
                                cleanCsFiles++;

                            copiedCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not process {File}", csFile);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(csTasks);

            _logger.LogInformation(
                "Archivos .cs → Transformados: {T}, Copiados: {C}, Skipped: {S}",
                transformedCount, copiedCount, skippedCount);

            // ─── 7. Migration score ───────────────────────────────────────────
            //
            // Score model (0–100):
            //
            //   40% → Clean .cs files ratio
            //          files with no legacy patterns / total .cs files
            //
            //   20% → .csproj migrated ratio
            //          csproj with correct TargetFramework / total csproj
            //
            //   20% → Non-deprecated packages ratio
            //          non-deprecated / total packages
            //          No packages found → full 20 pts (not penalized)
            //
            //  -15% → ManualRequired penalty (capped at 15 pts)
            //
            //   -5% → NeedsReview penalty (capped at 5 pts)
            //
            var manualItems = result.BacklogItems.Count(b => b.Category == BacklogCategory.ManualRequired);
            var reviewItems = result.BacklogItems.Count(b => b.Category == BacklogCategory.NeedsReview);

            var totalCs = tree.CsFiles.Count > 0 ? tree.CsFiles.Count : 1;
            var totalCsproj = tree.CsprojFiles.Count > 0 ? tree.CsprojFiles.Count : 1;

            var csScore = (double)cleanCsFiles / totalCs;
            var csprojScore = (double)csprojMigrated / totalCsproj;
            var packageScore = totalPackages > 0
                ? (double)(totalPackages - deprecatedPackages) / totalPackages
                : 1.0;

            var manualPenalty = Math.Min(1.0, (double)manualItems / totalCs);
            var reviewPenalty = Math.Min(1.0, (double)reviewItems / totalCs);

            var rawScore =
                (csScore * 40.0) +
                (csprojScore * 20.0) +
                (packageScore * 20.0) -
                (manualPenalty * 15.0) -
                (reviewPenalty * 5.0);

            result.MigrationPercentage = (int)Math.Max(0, Math.Min(100, rawScore));
            result.AutomatedCount = result.Changes.Count(c => c.Category == MigrationCategory.Automated);
            result.ManualCount = manualItems;
            result.ReviewCount = reviewItems;

            _logger.LogInformation(
                "Migration Score: {Score}% | Clean .cs: {Cs}/{TotalCs} | " +
                ".csproj OK: {Csproj}/{TotalCsproj} | " +
                "Packages OK: {PkgOk}/{TotalPkg} | " +
                "Manual: {Manual} | Review: {Review}",
                result.MigrationPercentage,
                cleanCsFiles, tree.CsFiles.Count,
                csprojMigrated, tree.CsprojFiles.Count,
                totalPackages - deprecatedPackages, totalPackages,
                manualItems, reviewItems);

            // ─── 7.5. Determinar carpetas/archivos a excluir según decisiones ─
            var excludedPaths = BuildExcludedPaths(decisions, tree.AllFiles);

            // ─── 7.6. Copiar archivos restantes del repo (paralelizado) ──────
            _logger.LogInformation("Copiando archivos restantes del repo...");

            var remainingFiles = tree.AllFiles
                .Where(f => !outputFiles.ContainsKey(f)
                         && !f.EndsWith("packages.config")
                         && !f.EndsWith("Global.asax")
                         && !f.EndsWith("Web.config")
                         && !excludedPaths.Any(ex => f.Contains(ex, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var copySemaphore = new SemaphoreSlim(10, 10);

            var copyTasks = remainingFiles.Select(async filePath =>
            {
                await copySemaphore.WaitAsync(cancellationToken);
                try
                {
                    var content = await _gitHub.GetFileContentAsync(repoUrl, filePath, cancellationToken);
                    lock (lockObj) outputFiles[filePath] = content;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not copy {File}", filePath);
                }
                finally
                {
                    copySemaphore.Release();
                }
            });

            await Task.WhenAll(copyTasks);

            _logger.LogInformation("ZIP final: {Count} archivos totales", outputFiles.Count);

            // ─── 8. ZIP ───────────────────────────────────────────────────────
            var report = ZipBuilder.GenerateReport(repoUrl, targetFramework, allChanges, result);
            result.ZipBytes = _zipBuilder.BuildWithReport(outputFiles, report);
            result.Success = true;

            _logger.LogInformation("✅ Migración completada. {Files} archivos, {Changes} cambios",
                outputFiles.Count, allChanges.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante la migración de {Url}", repoUrl);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private static List<string> ParsePackageNames(string packagesConfigContent)
    {
        try
        {
            var xml = System.Xml.Linq.XDocument.Parse(packagesConfigContent);
            return xml.Descendants("package")
                .Select(p => p.Attribute("id")?.Value ?? string.Empty)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Builds a list of path fragments to exclude from the ZIP based on user decisions.
    /// </summary>
    private static List<string> BuildExcludedPaths(
        Dictionary<string, string> decisions,
        List<string> allFiles)
    {
        var excluded = new List<string>();

        // HelpPage — Web API 2 scaffolding, no equivalent in ASP.NET Core
        var helpPageDecision = decisions.GetValueOrDefault("HelpPage", "swagger");
        if (helpPageDecision == "swagger")
        {
            excluded.Add("/Areas/HelpPage/");
            excluded.Add("\\Areas\\HelpPage\\");
        }

        // BundleConfig — Web.Optimization, no equivalent in ASP.NET Core
        var bundleDecision = decisions.GetValueOrDefault("BundleConfig", "remove");
        if (bundleDecision == "remove")
            excluded.Add("BundleConfig.cs");

        // RouteConfig — routes handled in Program.cs
        var routeDecision = decisions.GetValueOrDefault("RouteConfig", "remove");
        if (routeDecision == "remove")
            excluded.Add("RouteConfig.cs");

        // FilterConfig — global filters, handled in Program.cs
        var filterDecision = decisions.GetValueOrDefault("FilterConfig", "remove");
        if (filterDecision == "remove")
            excluded.Add("FilterConfig.cs");

        return excluded;
    }

    // ─── Plan (Dry-Run) ───────────────────────────────────────────────────────
    // Analiza el repo y devuelve exactamente qué pasaría si se ejecuta la migración,
    // SIN llamar a Claude y SIN modificar ningún archivo.
    public async Task<MigrationPlan> PlanAsync(
        string repoUrl,
        string targetFramework = "net8.0",
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Iniciando plan (dry-run) de {Url} → {Framework}", repoUrl, targetFramework);

        var plan = new MigrationPlan { RepoUrl = repoUrl, TargetFramework = targetFramework };
        var rules = await _transformationRules.GetAllActiveAsync(cancellationToken);
        var pureRules = rules.Where(r => !r.NeedsAI).ToList();
        var aiRules = rules.Where(r => r.NeedsAI).ToList();

        try
        {
            var tree = await _gitHub.GetRepositoryTreeAsync(repoUrl, cancellationToken);
            plan.TotalFilesScanned = tree.CsFiles.Count + tree.CsprojFiles.Count;

            // ── Detectar tipo de proyecto ────────────────────────────────────
            var allFilePaths = tree.AllFiles;
            string csprojContent = string.Empty;
            if (tree.CsprojFiles.Any())
            {
                try { csprojContent = await _gitHub.GetFileContentAsync(repoUrl, tree.CsprojFiles[0], cancellationToken); }
                catch { /* ignorar */ }
            }
            plan.ProjectType = DetectProjectType(allFilePaths, csprojContent);

            // Detectar framework del primer csproj
            if (!string.IsNullOrEmpty(csprojContent))
            {
                var xml = System.Xml.Linq.XDocument.Parse(csprojContent);
                var legacyFw = xml.Descendants("TargetFrameworkVersion").FirstOrDefault()?.Value;
                plan.DetectedFramework = legacyFw != null
                    ? "net" + legacyFw.Replace("v", "").Replace(".", "")
                    : xml.Descendants("TargetFramework").FirstOrDefault()?.Value ?? "unknown";
            }

            // ── Planificar .csproj ────────────────────────────────────────────
            foreach (var csprojPath in tree.CsprojFiles)
            {
                plan.PlannedChanges.Add(new PlannedChange
                {
                    FilePath = csprojPath,
                    Description = $"Convertir a SDK style y actualizar TargetFramework a {targetFramework}",
                    Confidence = ChangeConfidence.Safe,
                    TransformationType = "Framework"
                });
                plan.TotalFilesWithChanges++;
            }

            // ── Planificar Web.config ─────────────────────────────────────────
            if (tree.WebConfigFiles.Any())
            {
                plan.PlannedChanges.Add(new PlannedChange
                {
                    FilePath = tree.WebConfigFiles.First(),
                    Description = "Convertir Web.config a appsettings.json (AppSettings + ConnectionStrings)",
                    Confidence = ChangeConfidence.Safe,
                    TransformationType = "Config"
                });
                plan.TotalFilesWithChanges++;
            }

            // ── Planificar Global.asax ────────────────────────────────────────
            var globalAsax = tree.AllFiles.FirstOrDefault(f =>
                f.EndsWith("Global.asax.cs", StringComparison.OrdinalIgnoreCase));
            if (globalAsax != null)
            {
                plan.PlannedChanges.Add(new PlannedChange
                {
                    FilePath = globalAsax,
                    Description = "Generar Program.cs desde Global.asax (startup skeleton)",
                    Confidence = ChangeConfidence.ReviewRequired,
                    TransformationType = "Config"
                });
                plan.TotalFilesWithChanges++;
            }

            // ── Escanear .cs files con BacklogDetector + reglas ───────────────
            var semaphore = new SemaphoreSlim(8, 8);
            var lockObj = new object();

            var scanTasks = tree.CsFiles.Select(async csFile =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var content = await _gitHub.GetFileContentAsync(repoUrl, csFile, cancellationToken);

                    // Backlog detection (sin Claude)
                    var backlogItems = await _backlogDetector.DetectAsync(csFile, content, cancellationToken);

                    // Checar qué reglas puras matchean
                    var matchedPure = pureRules
                        .Where(r => r.Replacement != null &&
                                    content.Contains(r.Pattern, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    // Checar qué reglas AI matchean (sin ejecutar Claude)
                    var matchedAi = aiRules
                        .Where(r => content.Contains(r.Pattern, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    lock (lockObj)
                    {
                        plan.BacklogItems.AddRange(backlogItems);

                        foreach (var rule in matchedPure)
                        {
                            plan.PlannedChanges.Add(new PlannedChange
                            {
                                FilePath = csFile,
                                Description = rule.Description,
                                Confidence = HighRiskPatterns.Contains(rule.Pattern)
                                    ? ChangeConfidence.HighRisk
                                    : ChangeConfidence.Safe,
                                TransformationType = "PureRegex"
                            });
                        }

                        foreach (var rule in matchedAi)
                        {
                            plan.PlannedChanges.Add(new PlannedChange
                            {
                                FilePath = csFile,
                                Description = rule.Description,
                                Confidence = HighRiskPatterns.Contains(rule.Pattern)
                                    ? ChangeConfidence.HighRisk
                                    : ChangeConfidence.ReviewRequired,
                                TransformationType = "AI"
                            });
                        }

                        if (matchedPure.Any() || matchedAi.Any())
                            plan.TotalFilesWithChanges++;
                        else if (!backlogItems.Any())
                            plan.TotalFilesClean++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo escanear {File} en plan", csFile);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(scanTasks);

            // ── Calcular estimaciones ─────────────────────────────────────────
            plan.EstimatedManualItems = plan.BacklogItems.Count(b => b.Category == BacklogCategory.ManualRequired);
            plan.EstimatedReviewItems = plan.BacklogItems.Count(b => b.Category == BacklogCategory.NeedsReview);
            plan.EstimatedAutomatedChanges = plan.PlannedChanges
                .Count(c => c.Confidence == ChangeConfidence.Safe || c.Confidence == ChangeConfidence.ReviewRequired);

            var totalWork = plan.EstimatedAutomatedChanges + plan.EstimatedManualItems;
            plan.EstimatedAutomationPercentage = totalWork > 0
                ? (int)Math.Round((double)plan.EstimatedAutomatedChanges / totalWork * 100)
                : 100;

            // ── Generar roadmap ───────────────────────────────────────────────
            plan.Roadmap = BuildRoadmap(plan, tree.CsprojFiles.Count, tree.WebConfigFiles.Any());

            _logger.LogInformation(
                "Plan completado — {Files} archivos, {Automated} cambios automáticos, {Manual} manuales",
                plan.TotalFilesScanned, plan.EstimatedAutomatedChanges, plan.EstimatedManualItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando plan para {Url}", repoUrl);
        }

        return plan;
    }

    private static ChangeConfidence DetermineConfidence(string changeDescription, bool wasAiTransformed)
    {
        if (!wasAiTransformed) return ChangeConfidence.Safe;

        // Si la descripción menciona un patrón de alto riesgo → HighRisk
        foreach (var pattern in HighRiskPatterns)
        {
            if (changeDescription.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return ChangeConfidence.HighRisk;
        }

        return ChangeConfidence.ReviewRequired;
    }

    private static ProjectType DetectProjectType(List<string> allFiles, string firstCsprojContent)
    {
        var fileSet = allFiles.Select(f => f.ToLowerInvariant()).ToHashSet();

        // WCF: archivos .svc o patrones en csproj
        if (fileSet.Any(f => f.EndsWith(".svc")) ||
            firstCsprojContent.Contains("System.ServiceModel", StringComparison.OrdinalIgnoreCase))
            return ProjectType.WcfService;

        // WebForms: archivos .aspx o .ascx
        if (fileSet.Any(f => f.EndsWith(".aspx") || f.EndsWith(".ascx")))
            return ProjectType.WebForms;

        // Tests: nombre del proyecto o paquetes de test
        if (firstCsprojContent.Contains("MSTest", StringComparison.OrdinalIgnoreCase) ||
            firstCsprojContent.Contains("NUnit", StringComparison.OrdinalIgnoreCase) ||
            firstCsprojContent.Contains("xunit", StringComparison.OrdinalIgnoreCase))
            return ProjectType.TestProject;

        // ASP.NET MVC: buscar references a System.Web.Mvc
        if (firstCsprojContent.Contains("System.Web.Mvc", StringComparison.OrdinalIgnoreCase) ||
            firstCsprojContent.Contains("Microsoft.AspNet.Mvc", StringComparison.OrdinalIgnoreCase))
            return ProjectType.AspNetMvc;

        // Web API: buscar System.Web.Http
        if (firstCsprojContent.Contains("System.Web.Http", StringComparison.OrdinalIgnoreCase) ||
            firstCsprojContent.Contains("Microsoft.AspNet.WebApi", StringComparison.OrdinalIgnoreCase))
            return ProjectType.WebApi;

        // Console / Worker: OutputType Exe
        if (firstCsprojContent.Contains("<OutputType>Exe</OutputType>", StringComparison.OrdinalIgnoreCase))
        {
            return firstCsprojContent.Contains("Worker", StringComparison.OrdinalIgnoreCase)
                ? ProjectType.WorkerService
                : ProjectType.ConsoleApp;
        }

        // Si hay archivos web pero no es MVC/WebForms, asumir WebApi
        if (fileSet.Any(f => f.EndsWith("web.config")))
            return ProjectType.WebApi;

        return ProjectType.ClassLibrary;
    }

    private static List<RoadmapStep> BuildRoadmap(MigrationPlan plan, int csprojCount, bool hasWebConfig)
    {
        var steps = new List<RoadmapStep>();
        var order = 1;

        // Paso 1: Siempre — archivos de proyecto
        steps.Add(new RoadmapStep
        {
            Order = order++,
            Title = "Actualizar archivos de proyecto",
            Description = $"Convertir {csprojCount} .csproj al formato SDK style y apuntar a {plan.TargetFramework}",
            Risk = "Low",
            IsAutomatable = true,
            EstimatedFilesAffected = csprojCount
        });

        // Paso 2: Paquetes NuGet deprecados
        var packageChanges = plan.PlannedChanges.Count(c => c.TransformationType == "Framework");
        if (packageChanges > 0)
        {
            steps.Add(new RoadmapStep
            {
                Order = order++,
                Title = "Reemplazar paquetes NuGet deprecados",
                Description = "Actualizar referencias a paquetes con equivalentes modernos y compatibles con .NET",
                Risk = "Low",
                IsAutomatable = true,
                EstimatedFilesAffected = csprojCount
            });
        }

        // Paso 3: Configuración (si hay Web.config)
        if (hasWebConfig)
        {
            steps.Add(new RoadmapStep
            {
                Order = order++,
                Title = "Migrar configuración",
                Description = "Convertir Web.config a appsettings.json y generar Program.cs desde Global.asax",
                Risk = "Low",
                IsAutomatable = true,
                EstimatedFilesAffected = 2
            });
        }

        // Paso 4: Transformaciones de código automáticas
        var autoCodeChanges = plan.PlannedChanges.Count(c =>
            c.TransformationType == "PureRegex" && c.Confidence == ChangeConfidence.Safe);
        if (autoCodeChanges > 0)
        {
            steps.Add(new RoadmapStep
            {
                Order = order++,
                Title = "Transformaciones de código automáticas",
                Description = $"Reemplazar {autoCodeChanges} patrones legacy deterministas (namespaces, atributos HTTP, clases base)",
                Risk = "Low",
                IsAutomatable = true,
                EstimatedFilesAffected = plan.PlannedChanges
                    .Where(c => c.TransformationType == "PureRegex")
                    .Select(c => c.FilePath).Distinct().Count()
            });
        }

        // Paso 5: Transformaciones AI (si hay)
        var aiChanges = plan.PlannedChanges.Count(c => c.TransformationType == "AI");
        if (aiChanges > 0)
        {
            steps.Add(new RoadmapStep
            {
                Order = order++,
                Title = "Transformaciones asistidas por IA",
                Description = $"Roslyn migrará {aiChanges} patrones complejos (HttpContext.Current, OWIN, System.Drawing). Revisar resultado.",
                Risk = "Medium",
                IsAutomatable = true,
                EstimatedFilesAffected = plan.PlannedChanges
                    .Where(c => c.TransformationType == "AI")
                    .Select(c => c.FilePath).Distinct().Count()
            });
        }

        // Paso X: WCF (si aplica)
        if (plan.ProjectType == ProjectType.WcfService ||
            plan.BacklogItems.Any(b => b.Title.Contains("WCF")))
        {
            steps.Add(new RoadmapStep
            {
                Order = order++,
                Title = "Migrar servicios WCF",
                Description = "WCF no existe en .NET moderno. Opciones: gRPC (recomendado) o CoreWCF. Requiere decisión arquitectural.",
                Risk = "High",
                IsAutomatable = false,
                EstimatedFilesAffected = plan.BacklogItems
                    .Where(b => b.Title.Contains("WCF"))
                    .Select(b => b.FilePath).Distinct().Count()
            });
        }

        // Paso X: WebForms (si aplica)
        if (plan.ProjectType == ProjectType.WebForms ||
            plan.BacklogItems.Any(b => b.Title.Contains("Web Forms")))
        {
            steps.Add(new RoadmapStep
            {
                Order = order++,
                Title = "Reescribir UI de Web Forms",
                Description = "Web Forms no tiene equivalente en .NET moderno. La lógica puede rescatarse; la UI debe reescribirse en Blazor o Razor Pages.",
                Risk = "High",
                IsAutomatable = false,
                EstimatedFilesAffected = plan.BacklogItems
                    .Where(b => b.Title.Contains("Web Forms"))
                    .Select(b => b.FilePath).Distinct().Count()
            });
        }

        // Paso final: revision manual
        if (plan.EstimatedManualItems > 0 || plan.EstimatedReviewItems > 0)
        {
            steps.Add(new RoadmapStep
            {
                Order = order,
                Title = "Revisión manual y tests",
                Description = $"Revisar {plan.EstimatedReviewItems} items marcados con REVIEW y resolver {plan.EstimatedManualItems} items manuales. Correr suite de tests.",
                Risk = plan.EstimatedManualItems > 5 ? "High" : "Medium",
                IsAutomatable = false,
                EstimatedFilesAffected = plan.EstimatedManualItems + plan.EstimatedReviewItems
            });
        }

        return steps;
    }
}