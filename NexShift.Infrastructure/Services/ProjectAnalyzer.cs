using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using NexShift.Core.Entities;
using NexShift.Core.Interfaces;

namespace NexShift.Infrastructure.Services;

public class ProjectAnalyzer : IProjectAnalyzer
{
    private readonly IGitHubService _gitHub;
    private readonly ILogger<ProjectAnalyzer> _logger;
    private readonly INugetService _nugetService;

    public ProjectAnalyzer(IGitHubService gitHub, ILogger<ProjectAnalyzer> logger, INugetService nugetService)
    {
        _gitHub = gitHub;
        _logger = logger;
        _nugetService = nugetService;
    }

    public async Task<ProjectAnalysisResult> AnalyzeAsync(string repoUrl, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analizando repo {Url}", repoUrl);

        var tree = await _gitHub.GetRepositoryTreeAsync(repoUrl, cancellationToken);

        var result = new ProjectAnalysisResult
        {
            CsprojFiles = tree.CsprojFiles,
            CsFiles = tree.CsFiles
        };

        var allPackages = new List<PackageInfo>();
        string firstCsprojContent = string.Empty;

        // ── 1. Parsear .csproj y packages.config ─────────────────────────────
        foreach (var csprojPath in tree.CsprojFiles)
        {
            try
            {
                var content = await _gitHub.GetFileContentAsync(repoUrl, csprojPath, cancellationToken);

                if (string.IsNullOrEmpty(firstCsprojContent))
                    firstCsprojContent = content;

                var (packages, framework) = await ParseCsproj(content);
                allPackages.AddRange(packages);

                if (string.IsNullOrEmpty(result.DetectedFramework) || IsMoreLegacy(framework, result.DetectedFramework))
                {
                    result.DetectedFramework = framework;
                    result.IsLegacy = framework.StartsWith("net4") || framework.StartsWith("netframework");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo parsear {File}", csprojPath);
            }
        }

        foreach (var configPath in tree.PackagesConfigFiles)
        {
            try
            {
                if (configPath.EndsWith("global.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                var content = await _gitHub.GetFileContentAsync(repoUrl, configPath, cancellationToken);

                if (configPath.EndsWith("packages.config"))
                {
                    var packages = await ParsePackagesConfig(content);
                    allPackages.AddRange(packages);
                }
                else
                {
                    var (packages, framework) = await ParseDirectoryBuildProps(content);
                    allPackages.AddRange(packages);

                    if (!string.IsNullOrEmpty(framework) && string.IsNullOrEmpty(result.DetectedFramework))
                    {
                        result.DetectedFramework = framework;
                        result.IsLegacy = framework.StartsWith("net4") || framework.StartsWith("netframework");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo parsear {File}", configPath);
            }
        }

        result.Packages = allPackages
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(p => p.IsDeprecated)
            .ThenBy(p => p.Name)
            .ToList();

        // ── 2. Detectar tipo de proyecto ──────────────────────────────────────
        result.ProjectType = DetectProjectType(tree.AllFiles, firstCsprojContent);

        // ── 3. Escanear .cs para patrones legacy (hasta 50 archivos) ─────────
        result.Findings = await ScanCsFilesAsync(repoUrl, tree.CsFiles, tree.AllFiles, cancellationToken);

        // ── 4. Calcular score con breakdown detallado ─────────────────────────
        var (score, breakdown) = CalculateScore(result);
        result.MigrationScore = score;
        result.ScoreBreakdown = breakdown;

        _logger.LogInformation(
            "Análisis completo. Score: {Score} | Framework: {Framework} | " +
            "Deprecated: {Deprecated}/{Total} | System.Web: {SW} files | WCF: {Wcf} files",
            result.MigrationScore, result.DetectedFramework,
            result.Packages.Count(p => p.IsDeprecated), result.Packages.Count,
            result.Findings.FilesWithSystemWeb, result.Findings.FilesWithWcf);

        return result;
    }

    // ── Detectar tipo de proyecto desde el árbol de archivos y el csproj ─────
    private static ProjectType DetectProjectType(List<string> allFiles, string csprojContent)
    {
        var fileSet = allFiles.Select(f => f.ToLowerInvariant()).ToHashSet();

        if (fileSet.Any(f => f.EndsWith(".svc")) ||
            csprojContent.Contains("System.ServiceModel", StringComparison.OrdinalIgnoreCase))
            return ProjectType.WcfService;

        if (fileSet.Any(f => f.EndsWith(".aspx") || f.EndsWith(".ascx")))
            return ProjectType.WebForms;

        if (csprojContent.Contains("MSTest", StringComparison.OrdinalIgnoreCase) ||
            csprojContent.Contains("NUnit", StringComparison.OrdinalIgnoreCase) ||
            csprojContent.Contains("xunit", StringComparison.OrdinalIgnoreCase))
            return ProjectType.TestProject;

        if (csprojContent.Contains("System.Web.Mvc", StringComparison.OrdinalIgnoreCase) ||
            csprojContent.Contains("Microsoft.AspNet.Mvc", StringComparison.OrdinalIgnoreCase))
            return ProjectType.AspNetMvc;

        if (csprojContent.Contains("System.Web.Http", StringComparison.OrdinalIgnoreCase) ||
            csprojContent.Contains("Microsoft.AspNet.WebApi", StringComparison.OrdinalIgnoreCase))
            return ProjectType.WebApi;

        if (csprojContent.Contains("<OutputType>Exe</OutputType>", StringComparison.OrdinalIgnoreCase))
        {
            return csprojContent.Contains("Worker", StringComparison.OrdinalIgnoreCase)
                ? ProjectType.WorkerService
                : ProjectType.ConsoleApp;
        }

        if (fileSet.Any(f => f.EndsWith("web.config")))
            return ProjectType.WebApi;

        return ProjectType.ClassLibrary;
    }

    // ── Escanear hasta 50 archivos .cs representativos del repo ──────────────
    private async Task<CodeFindings> ScanCsFilesAsync(
        string repoUrl,
        List<string> csFiles,
        List<string> allFiles,
        CancellationToken cancellationToken)
    {
        var findings = new CodeFindings
        {
            HasAspxFiles = allFiles.Any(f =>
                f.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".ascx", StringComparison.OrdinalIgnoreCase)),
            HasSvcFiles = allFiles.Any(f => f.EndsWith(".svc", StringComparison.OrdinalIgnoreCase))
        };

        if (!csFiles.Any()) return findings;

        // Priorizar archivos más representativos: Controllers, Services, Global.asax, etc.
        var filesToScan = csFiles
            .OrderByDescending(f =>
                f.Contains("Controller", StringComparison.OrdinalIgnoreCase) ? 4 :
                f.Contains("Global", StringComparison.OrdinalIgnoreCase) ? 3 :
                f.Contains("Service", StringComparison.OrdinalIgnoreCase) ? 2 :
                f.Contains("Repository", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .Take(50)
            .ToList();

        findings.TotalFilesScanned = filesToScan.Count;

        var lockObj = new object();
        var semaphore = new SemaphoreSlim(10, 10);

        var tasks = filesToScan.Select(async file =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var content = await _gitHub.GetFileContentAsync(repoUrl, file, cancellationToken);

                lock (lockObj)
                {
                    // System.Web (excluir archivos que ya migraron a AspNetCore)
                    if (content.Contains("System.Web", StringComparison.OrdinalIgnoreCase) &&
                        !content.Contains("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase))
                        findings.FilesWithSystemWeb++;

                    if (content.Contains("ServiceContract", StringComparison.OrdinalIgnoreCase) ||
                        content.Contains("System.ServiceModel", StringComparison.OrdinalIgnoreCase))
                        findings.FilesWithWcf++;

                    if (content.Contains("System.Web.UI", StringComparison.OrdinalIgnoreCase) ||
                        content.Contains("Page_Load", StringComparison.OrdinalIgnoreCase))
                        findings.FilesWithWebForms++;

                    if (content.Contains("BinaryFormatter", StringComparison.OrdinalIgnoreCase))
                        findings.FilesWithBinaryFormatter++;

                    if (content.Contains("AppDomain", StringComparison.OrdinalIgnoreCase))
                        findings.FilesWithAppDomain++;

                    if (content.Contains("System.Runtime.Remoting", StringComparison.OrdinalIgnoreCase))
                        findings.FilesWithRemoting++;

                    if (content.Contains("Microsoft.Win32.Registry", StringComparison.OrdinalIgnoreCase) ||
                        content.Contains("RegistryKey", StringComparison.OrdinalIgnoreCase) ||
                        content.Contains("System.Diagnostics.EventLog", StringComparison.OrdinalIgnoreCase) ||
                        content.Contains("DllImport", StringComparison.OrdinalIgnoreCase))
                        findings.FilesWithWindowsOnlyApis++;

                    if (content.Contains("FormsAuthentication", StringComparison.OrdinalIgnoreCase) ||
                        content.Contains("WindowsIdentity", StringComparison.OrdinalIgnoreCase))
                        findings.FilesWithLegacyAuth++;

                    if (content.Contains("HttpContext.Current", StringComparison.OrdinalIgnoreCase))
                        findings.FilesWithHttpContextCurrent++;

                    if (content.Contains("ConfigurationManager", StringComparison.OrdinalIgnoreCase))
                        findings.FilesWithConfigurationManager++;

                    if (content.Contains("Thread.Abort", StringComparison.OrdinalIgnoreCase))
                        findings.FilesWithThreadAbort++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo escanear {File}", file);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        _logger.LogInformation(
            "Scan de .cs completado. {Scanned}/{Total} archivos | System.Web: {SW} | WCF: {Wcf} | BinaryFormatter: {BF}",
            findings.TotalFilesScanned, csFiles.Count,
            findings.FilesWithSystemWeb, findings.FilesWithWcf, findings.FilesWithBinaryFormatter);

        return findings;
    }

    // ── Algoritmo de score mejorado con breakdown detallado ───────────────────
    private static (int score, List<ScoreFactor> breakdown) CalculateScore(ProjectAnalysisResult result)
    {
        var score = 100;
        var breakdown = new List<ScoreFactor>();

        void Apply(string factor, int impact, string details)
        {
            if (impact == 0) return;
            score += impact;
            breakdown.Add(new ScoreFactor { Factor = factor, Impact = impact, Details = details });
        }

        // ── Penalizaciones por framework ──────────────────────────────────────
        if (result.IsLegacy)
            Apply("Framework legacy", -20, $"Detectado: {result.DetectedFramework} — requiere actualización del proyecto");

        // ── Penalizaciones por arquitectura (los más costosos de migrar) ──────
        if (result.Findings.FilesWithSystemWeb > 0)
            Apply("System.Web dependencies", -15,
                $"{result.Findings.FilesWithSystemWeb} archivo(s) con referencias a System.Web — el mayor bloqueador de migración");

        if (result.Findings.FilesWithWcf > 0 || result.Findings.HasSvcFiles)
            Apply("WCF Services detectados", -10,
                $"{result.Findings.FilesWithWcf} archivo(s) con ServiceContract/System.ServiceModel — requiere migración a gRPC o CoreWCF");

        if (result.Findings.FilesWithWebForms > 0 || result.Findings.HasAspxFiles)
            Apply("ASP.NET Web Forms detectados", -10,
                $"{result.Findings.FilesWithWebForms} archivo(s){(result.Findings.HasAspxFiles ? " + archivos .aspx" : "")} — UI requiere reescritura");

        // ── Penalización por paquetes NuGet deprecados ────────────────────────
        var deprecatedCount = result.Packages.Count(p => p.IsDeprecated);
        var totalPackages = result.Packages.Count;
        if (totalPackages > 0 && deprecatedCount > 0)
        {
            var ratio = (double)deprecatedCount / totalPackages;
            var pkgPenalty = -(int)(ratio * 25);
            Apply("Paquetes NuGet deprecados", pkgPenalty,
                $"{deprecatedCount}/{totalPackages} paquetes deprecados ({ratio * 100:F0}%) — reemplazo parcialmente automático");
        }

        // ── Penalizaciones por APIs peligrosas/eliminadas ─────────────────────
        if (result.Findings.FilesWithBinaryFormatter > 0)
            Apply("BinaryFormatter (vulnerabilidad de seguridad)", -5,
                $"{result.Findings.FilesWithBinaryFormatter} archivo(s) — migrar a System.Text.Json o protobuf");

        if (result.Findings.FilesWithAppDomain > 0 || result.Findings.FilesWithRemoting > 0)
            Apply("AppDomain / .NET Remoting", -5,
                "APIs eliminadas en .NET moderno — requieren refactoring manual");

        if (result.Findings.FilesWithWindowsOnlyApis > 0)
            Apply("APIs Windows-only", -5,
                $"{result.Findings.FilesWithWindowsOnlyApis} archivo(s) con Registry/EventLog/DllImport — rompe cross-platform");

        if (result.Findings.FilesWithLegacyAuth > 0)
            Apply("Autenticación legacy", -3,
                $"{result.Findings.FilesWithLegacyAuth} archivo(s) con FormsAuthentication/WindowsIdentity — decisión arquitectural requerida");

        if (result.Findings.FilesWithThreadAbort > 0)
            Apply("Thread.Abort (eliminado)", -3,
                $"{result.Findings.FilesWithThreadAbort} archivo(s) — reemplazar por CancellationToken");

        // ── Penalizaciones por complejidad ────────────────────────────────────
        var csFiles = result.CsFiles.Count;
        if (csFiles > 500)       Apply("Codebase muy grande", -9, $"{csFiles} archivos .cs");
        else if (csFiles > 300)  Apply("Codebase grande", -6, $"{csFiles} archivos .cs");
        else if (csFiles > 150)  Apply("Codebase mediano", -3, $"{csFiles} archivos .cs");
        else if (csFiles > 50)   Apply("Tamaño de codebase", -2, $"{csFiles} archivos .cs");

        var projects = result.CsprojFiles.Count;
        if (projects > 10)      Apply("Solución muy grande", -5, $"{projects} proyectos (.csproj)");
        else if (projects > 5)  Apply("Solución multi-proyecto", -3, $"{projects} proyectos (.csproj)");

        // ── Bonus: todo moderno ───────────────────────────────────────────────
        var modernCount = result.Packages.Count(p => !p.IsDeprecated);
        if (modernCount > 10 && deprecatedCount == 0 && !result.IsLegacy &&
            result.Findings.FilesWithSystemWeb == 0)
            Apply("Paquetes modernos y sin deuda técnica", +5,
                $"{modernCount} paquetes actualizados, sin System.Web, sin framework legacy");

        return (Math.Max(0, Math.Min(100, score)), breakdown);
    }

    private async Task<(List<PackageInfo> packages, string framework)> ParseCsproj(string content)
    {
        var xml = XDocument.Parse(content);
        var packages = new List<PackageInfo>();
        var framework = string.Empty;

        var ns = xml.Root?.Name.Namespace ?? XNamespace.None;

        var legacyFramework = xml.Descendants(ns + "TargetFrameworkVersion").FirstOrDefault()?.Value;
        if (!string.IsNullOrEmpty(legacyFramework))
            framework = "net" + legacyFramework.Replace("v", "").Replace(".", "");

        if (string.IsNullOrEmpty(framework))
        {
            framework = xml.Descendants(ns + "TargetFramework").FirstOrDefault()?.Value
                     ?? xml.Descendants(ns + "TargetFrameworks").FirstOrDefault()?.Value?.Split(';').First()
                     ?? string.Empty;
        }

        foreach (var p in xml.Descendants(ns + "PackageReference"))
        {
            var pkg = await CreatePackageInfoAsync(
                p.Attribute("Include")?.Value ?? string.Empty,
                p.Attribute("Version")?.Value
                ?? p.Element(ns + "Version")?.Value
                ?? string.Empty);
            if (!string.IsNullOrEmpty(pkg.Name))
                packages.Add(pkg);
        }

        return (packages.Where(p => !string.IsNullOrEmpty(p.Name)).ToList(), framework);
    }

    private async Task<List<PackageInfo>> ParsePackagesConfig(string content)
    {
        var xml = XDocument.Parse(content);
        var packages = new List<PackageInfo>();

        foreach (var p in xml.Descendants("package"))
        {
            var pkg = await CreatePackageInfoAsync(
                p.Attribute("id")?.Value ?? string.Empty,
                p.Attribute("version")?.Value ?? string.Empty);
            if (!string.IsNullOrEmpty(pkg.Name))
                packages.Add(pkg);
        }

        return packages;
    }

    private async Task<PackageInfo> CreatePackageInfoAsync(string name, string version)
    {
        var dbPackage = await _nugetService.GetPackageInfoAsync(name);

        if (dbPackage != null)
        {
            return new PackageInfo
            {
                Name = name,
                Version = version,
                IsDeprecated = dbPackage.IsDeprecated,
                SuggestedReplacement = dbPackage.SuggestedReplacement
            };
        }

        await _nugetService.CheckAndUpdatePackageAsync(name);
        var newPackage = await _nugetService.GetPackageInfoAsync(name);

        return new PackageInfo
        {
            Name = name,
            Version = version,
            IsDeprecated = newPackage?.IsDeprecated ?? false,
            SuggestedReplacement = newPackage?.SuggestedReplacement
        };
    }

    private async Task<(List<PackageInfo> packages, string framework)> ParseDirectoryBuildProps(string content)
    {
        var xml = XDocument.Parse(content);
        var packages = new List<PackageInfo>();

        var framework = xml.Descendants("TargetFramework").FirstOrDefault()?.Value
                     ?? xml.Descendants("TargetFrameworks").FirstOrDefault()?.Value?.Split(';').First()
                     ?? string.Empty;

        foreach (var p in xml.Descendants("PackageVersion"))
        {
            var pkg = await CreatePackageInfoAsync(
                p.Attribute("Include")?.Value ?? string.Empty,
                p.Attribute("Version")?.Value ?? string.Empty);
            if (!string.IsNullOrEmpty(pkg.Name))
                packages.Add(pkg);
        }

        return (packages.Where(p => !string.IsNullOrEmpty(p.Name)).ToList(), framework);
    }

    private static bool IsMoreLegacy(string candidate, string current)
    {
        if (string.IsNullOrEmpty(candidate)) return false;

        var order = new[] { "net40", "net45", "net451", "net452", "net46", "net461", "net462", "net47", "net471", "net472", "net48", "net481" };

        var candidateIndex = Array.IndexOf(order, candidate.ToLower());
        var currentIndex = Array.IndexOf(order, current.ToLower());

        if (candidateIndex >= 0 && currentIndex >= 0)
            return candidateIndex < currentIndex;

        if (candidate.StartsWith("net4") && !current.StartsWith("net4"))
            return true;

        return false;
    }
}
