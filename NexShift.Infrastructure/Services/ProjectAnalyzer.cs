using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using NexShift.Core.Interfaces;

namespace NexShift.Infrastructure.Services;

public class ProjectAnalyzer : IProjectAnalyzer
{
    private readonly IGitHubService _gitHub;
    private readonly ILogger<ProjectAnalyzer> _logger;

    private static readonly Dictionary<string, string> DeprecatedPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Newtonsoft.Json", "System.Text.Json (nativo en .NET)" },
        { "AutoMapper", "Mapperly o mapeo manual" },
        { "log4net", "Microsoft.Extensions.Logging + Serilog" },
        { "NLog", "Microsoft.Extensions.Logging + Serilog" },
        { "Unity", "Microsoft.Extensions.DependencyInjection" },
        { "Ninject", "Microsoft.Extensions.DependencyInjection" },
        { "Castle.Windsor", "Microsoft.Extensions.DependencyInjection" },
        { "WebActivatorEx", "Program.cs con WebApplication.CreateBuilder" },
        { "Microsoft.AspNet.WebApi", "Microsoft.AspNetCore" },
        { "Microsoft.AspNet.Mvc", "Microsoft.AspNetCore.Mvc" },
        { "EntityFramework", "Microsoft.EntityFrameworkCore" },
        { "System.Web.Http", "Microsoft.AspNetCore.Mvc" },
    };

    public ProjectAnalyzer(IGitHubService gitHub, ILogger<ProjectAnalyzer> logger)
    {
        _gitHub = gitHub;
        _logger = logger;
    }

    public async Task<ProjectAnalysisResult> AnalyzeAsync(string repoUrl, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analizando repo {Url}", repoUrl);

        // 1. Obtener tree sin bajar nada
        var tree = await _gitHub.GetRepositoryTreeAsync(repoUrl, cancellationToken);

        var result = new ProjectAnalysisResult
        {
            CsprojFiles = tree.CsprojFiles,
            CsFiles = tree.CsFiles
        };

        // 2. Bajar y parsear solo los .csproj y packages.config
        var allPackages = new List<PackageInfo>();

        foreach (var csprojPath in tree.CsprojFiles)
        {
            try
            {
                var content = await _gitHub.GetFileContentAsync(repoUrl, csprojPath, cancellationToken);
                var (packages, framework) = ParseCsproj(content);
                allPackages.AddRange(packages);

                // Tomar el framework más legacy que encuentre
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
                var content = await _gitHub.GetFileContentAsync(repoUrl, configPath, cancellationToken);

                if (configPath.EndsWith("packages.config"))
                {
                    var packages = ParsePackagesConfig(content);
                    allPackages.AddRange(packages);
                }
                else
                {
                    var (packages, framework) = ParseDirectoryBuildProps(content);
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

        // 3. Deduplicar por nombre — fix del bug anterior
        result.Packages = allPackages
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(p => p.IsDeprecated)
            .ThenBy(p => p.Name)
            .ToList();

        // 4. Calcular score
        result.MigrationScore = CalculateScore(result);

        _logger.LogInformation("Análisis completo. Score: {Score}, Deprecated: {Deprecated}",
            result.MigrationScore,
            result.Packages.Count(p => p.IsDeprecated));
        _logger.LogInformation("CsprojFiles: {Files}", string.Join(", ", tree.CsprojFiles));
        _logger.LogInformation("PackagesConfigFiles: {Files}", string.Join(", ", tree.PackagesConfigFiles));
        return result;
    }

    private (List<PackageInfo> packages, string framework) ParseCsproj(string content)
    {
        var xml = XDocument.Parse(content);
        var packages = new List<PackageInfo>();
        var framework = string.Empty;

        // Manejar namespace del formato viejo de .NET Framework
        var ns = xml.Root?.Name.Namespace ?? XNamespace.None;

        // Formato viejo — <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
        var legacyFramework = xml.Descendants(ns + "TargetFrameworkVersion").FirstOrDefault()?.Value;
        if (!string.IsNullOrEmpty(legacyFramework))
        {
            // Convierte "v4.5" → "net45", "v4.7.2" → "net472"
            framework = "net" + legacyFramework.Replace("v", "").Replace(".", "");
        }

        // Formato nuevo — <TargetFramework>net8.0</TargetFramework>
        if (string.IsNullOrEmpty(framework))
        {
            framework = xml.Descendants(ns + "TargetFramework").FirstOrDefault()?.Value
                     ?? xml.Descendants(ns + "TargetFrameworks").FirstOrDefault()?.Value?.Split(';').First()
                     ?? string.Empty;
        }

        // SDK style packages
        packages.AddRange(xml.Descendants(ns + "PackageReference")
            .Select(p => CreatePackageInfo(
                p.Attribute("Include")?.Value ?? string.Empty,
                p.Attribute("Version")?.Value
                ?? p.Element(ns + "Version")?.Value
                ?? string.Empty)));

        return (packages.Where(p => !string.IsNullOrEmpty(p.Name)).ToList(), framework);
    }

    private List<PackageInfo> ParsePackagesConfig(string content)
    {
        var xml = XDocument.Parse(content);

        return xml.Descendants("package")
            .Select(p => CreatePackageInfo(
                p.Attribute("id")?.Value ?? string.Empty,
                p.Attribute("version")?.Value ?? string.Empty))
            .Where(p => !string.IsNullOrEmpty(p.Name))
            .ToList();
    }

    private PackageInfo CreatePackageInfo(string name, string version)
    {
        var isDeprecated = DeprecatedPackages.TryGetValue(name, out var replacement);
        return new PackageInfo
        {
            Name = name,
            Version = version,
            IsDeprecated = isDeprecated,
            SuggestedReplacement = replacement
        };
    }

    private static int CalculateScore(ProjectAnalysisResult result)
    {
        var score = 100;
        if (result.IsLegacy) score -= 30;
        var deprecatedCount = result.Packages.Count(p => p.IsDeprecated);
        score -= deprecatedCount * 10;
        if (result.CsFiles.Count > 100) score -= 10;
        if (result.CsFiles.Count > 500) score -= 10;
        return Math.Max(0, Math.Min(100, score));
    }
    private static bool IsMoreLegacy(string candidate, string current)
    {
        if (string.IsNullOrEmpty(candidate)) return false;
        if (candidate.StartsWith("net4")) return true;
        return false;
    }

    private (List<PackageInfo> packages, string framework) ParseDirectoryBuildProps(string content)
    {
        var xml = XDocument.Parse(content);
        var packages = new List<PackageInfo>();

        var framework = xml.Descendants("TargetFramework").FirstOrDefault()?.Value
                     ?? xml.Descendants("TargetFrameworks").FirstOrDefault()?.Value?.Split(';').First()
                     ?? string.Empty;

        packages.AddRange(xml.Descendants("PackageVersion")
            .Select(p => CreatePackageInfo(
                p.Attribute("Include")?.Value ?? string.Empty,
                p.Attribute("Version")?.Value ?? string.Empty))
            .Where(p => !string.IsNullOrEmpty(p.Name)));

        return (packages, framework);
    }
}