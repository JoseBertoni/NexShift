using System.Text;
using System.Xml.Linq;
using NexShift.Core.Entities;

namespace NexShift.Infrastructure.Services.Migrator;

public class CsprojTransformer
{
    // Loaded from DB — injected via SetKnownPackages() before use
    private IReadOnlyList<KnownDeprecatedPackage> _knownPackages = Array.Empty<KnownDeprecatedPackage>();

    /// <summary>
    /// Called once by MigrationService after WarmUpAsync, before any Transform calls.
    /// </summary>
    public void SetKnownPackages(IReadOnlyList<KnownDeprecatedPackage> packages)
    {
        _knownPackages = packages;
    }

    public (string content, List<string> changes) Transform(
        string csprojContent,
        string targetFramework,
        List<string> packagesFromConfig)
    {
        var changes = new List<string>();
        var xml = XDocument.Parse(csprojContent);
        var ns = xml.Root?.Name.Namespace ?? XNamespace.None;

        var isLegacy = xml.Descendants(ns + "TargetFrameworkVersion").Any();
        var sdkType = DetectSdkType(csprojContent, packagesFromConfig);

        return isLegacy
            ? TransformLegacy(csprojContent, targetFramework, packagesFromConfig, changes, sdkType)
            : TransformModern(xml, ns, targetFramework, changes, sdkType);
    }

    /// <summary>
    /// Detects the correct SDK type based on project content.
    /// Microsoft.NET.Sdk.Web  → web apps (MVC, WebAPI, WebForms)
    /// Microsoft.NET.Sdk      → class libraries, console apps, test projects
    /// </summary>
    private static string DetectSdkType(string csprojContent, List<string> packages)
    {
        // Test projects → plain SDK
        if (csprojContent.Contains("xunit", StringComparison.OrdinalIgnoreCase) ||
            csprojContent.Contains("MSTest", StringComparison.OrdinalIgnoreCase) ||
            csprojContent.Contains("NUnit", StringComparison.OrdinalIgnoreCase) ||
            packages.Any(p => p.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) ||
                              p.StartsWith("MSTest", StringComparison.OrdinalIgnoreCase) ||
                              p.StartsWith("NUnit", StringComparison.OrdinalIgnoreCase)))
            return "Microsoft.NET.Sdk";

        // Explicit library output → plain SDK
        if (csprojContent.Contains("<OutputType>Library</OutputType>", StringComparison.OrdinalIgnoreCase))
            return "Microsoft.NET.Sdk";

        // Web indicators → Web SDK
        if (csprojContent.Contains("System.Web", StringComparison.OrdinalIgnoreCase) ||
            csprojContent.Contains("Microsoft.AspNet", StringComparison.OrdinalIgnoreCase) ||
            csprojContent.Contains("WebApi", StringComparison.OrdinalIgnoreCase) ||
            csprojContent.Contains("Web.config", StringComparison.OrdinalIgnoreCase) ||
            csprojContent.Contains("Global.asax", StringComparison.OrdinalIgnoreCase) ||
            packages.Any(p => p.StartsWith("Microsoft.AspNet", StringComparison.OrdinalIgnoreCase)))
            return "Microsoft.NET.Sdk.Web";

        // No web indicators → assume library
        return "Microsoft.NET.Sdk";
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool ShouldRemove(string packageName)
        => _knownPackages.Any(p =>
            p.Name.Equals(packageName, StringComparison.OrdinalIgnoreCase) &&
            p.IsActive &&
            p.Category == "RemoveOnly");

    private KnownDeprecatedPackage? GetReplacement(string packageName)
        => _knownPackages.FirstOrDefault(p =>
            p.Name.Equals(packageName, StringComparison.OrdinalIgnoreCase) &&
            p.IsActive &&
            p.Category == "Deprecated");

    // ── Legacy .csproj → SDK style ────────────────────────────────────────────

    private (string content, List<string> changes) TransformLegacy(
        string originalContent,
        string targetFramework,
        List<string> packagesFromConfig,
        List<string> changes,
        string sdkType)
    {
        changes.Add("Converted from legacy MSBuild format to SDK style");
        changes.Add($"TargetFramework updated to {targetFramework}");

        var packageRefs = new StringBuilder();
        var addedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var packageName in packagesFromConfig)
        {
            if (ShouldRemove(packageName))
            {
                changes.Add($"Eliminated: {packageName} (not required in ASP.NET Core)");
                continue;
            }

            var replacement = GetReplacement(packageName);
            if (replacement != null)
            {
                // Empty SuggestedReplacement = native / framework-included, no PackageReference needed
                if (string.IsNullOrEmpty(replacement.SuggestedReplacement))
                {
                    changes.Add($"Eliminated: {packageName} (replaced by native functionality)");
                    continue;
                }

                if (!addedPackages.Contains(replacement.SuggestedReplacement))
                {
                    var versionAttr = string.IsNullOrEmpty(replacement.ReplacementVersion)
                        ? ""
                        : $" Version=\"{replacement.ReplacementVersion}\"";

                    packageRefs.AppendLine($"    <PackageReference Include=\"{replacement.SuggestedReplacement}\"{versionAttr} />");
                    addedPackages.Add(replacement.SuggestedReplacement);
                    changes.Add($"Reemplazado: {packageName} → {replacement.SuggestedReplacement}");
                }
            }
            else
            {
                // Unknown package — keep as-is
                if (!addedPackages.Contains(packageName))
                {
                    packageRefs.AppendLine($"    <PackageReference Include=\"{packageName}\" />");
                    addedPackages.Add(packageName);
                }
            }
        }

        var sdkCsproj = $@"<Project Sdk=""{sdkType}"">

  <PropertyGroup>
    <TargetFramework>{targetFramework}</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
  </PropertyGroup>

  <ItemGroup>
{packageRefs}  </ItemGroup>

</Project>";

        return (sdkCsproj, changes);
    }

    // ── SDK style modern — only update TargetFramework + PackageReferences ────

    private (string content, List<string> changes) TransformModern(
        XDocument xml,
        XNamespace ns,
        string targetFramework,
        List<string> changes,
        string sdkType)
    {
        // Update SDK type if needed
        var sdkAttr = xml.Root?.Attribute("Sdk");
        if (sdkAttr != null && sdkAttr.Value != sdkType)
        {
            changes.Add($"SDK updated: {sdkAttr.Value} → {sdkType}");
            sdkAttr.Value = sdkType;
        }
        var tfNode = xml.Descendants(ns + "TargetFramework").FirstOrDefault();
        if (tfNode != null && tfNode.Value != targetFramework)
        {
            changes.Add($"TargetFramework updated: {tfNode.Value} → {targetFramework}");
            tfNode.Value = targetFramework;
        }

        // Add GenerateAssemblyInfo=false to avoid CS0579 duplicates
        // when the project still has a legacy AssemblyInfo.cs
        var propGroup = xml.Descendants(ns + "PropertyGroup").FirstOrDefault();
        if (propGroup != null)
        {
            var hasGenAssembly = propGroup.Descendants(ns + "GenerateAssemblyInfo").Any();
            if (!hasGenAssembly)
                propGroup.Add(new XElement(ns + "GenerateAssemblyInfo", "false"));
        }

        var addedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pkgRef in xml.Descendants(ns + "PackageReference").ToList())
        {
            var name = pkgRef.Attribute("Include")?.Value ?? string.Empty;
            if (string.IsNullOrEmpty(name)) continue;

            if (ShouldRemove(name))
            {
                pkgRef.Remove();
                changes.Add($"Eliminated: {name}");
                continue;
            }

            var replacement = GetReplacement(name);
            if (replacement == null)
            {
                // Unknown package — keep it but ensure it has a Version attribute
                // (even empty is better than missing for SDK style)
                addedPackages.Add(name);
                continue;
            }

            if (string.IsNullOrEmpty(replacement.SuggestedReplacement))
            {
                pkgRef.Remove();
                changes.Add($"Eliminated: {name} (native functionality)");
            }
            else
            {
                var newName = replacement.SuggestedReplacement;

                // Avoid duplicates if the modern package was already added
                if (addedPackages.Contains(newName))
                {
                    pkgRef.Remove();
                    continue;
                }

                pkgRef.SetAttributeValue("Include", newName);
                if (!string.IsNullOrEmpty(replacement.ReplacementVersion))
                    pkgRef.SetAttributeValue("Version", replacement.ReplacementVersion);
                else
                    pkgRef.Attribute("Version")?.Remove(); // clean empty version attr

                addedPackages.Add(newName);
                changes.Add($"Reemplazado: {name} → {newName}");
            }
        }

        return (xml.ToString(), changes);
    }
}
