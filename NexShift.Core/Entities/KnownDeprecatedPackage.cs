namespace NexShift.Core.Entities;

public class KnownDeprecatedPackage
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// "Deprecated" | "SecurityVulnerability" | "RemoveOnly"
    /// RemoveOnly = package should be dropped with no replacement (e.g. WebGrease, Antlr)
    /// </summary>
    public string Category { get; set; } = "Deprecated";

    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// The modern replacement package name.
    /// Null/empty for RemoveOnly or when replacement is framework-native (no PackageReference needed).
    /// </summary>
    public string? SuggestedReplacement { get; set; }

    /// <summary>
    /// Version to pin in the PackageReference. Empty = let NuGet resolve latest / framework-included.
    /// </summary>
    public string? ReplacementVersion { get; set; }

    /// <summary>
    /// For SecurityVulnerability entries: link to the advisory (e.g. GHSA-xxxx).
    /// </summary>
    public string? AdvisoryUrl { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
