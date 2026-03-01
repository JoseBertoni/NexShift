namespace NexShift.Core.Interfaces
{
    public interface IProjectAnalyzer
    {
        Task<ProjectAnalysisResult> AnalyzeAsync(string localPath, CancellationToken cancellationToken = default);
    }

    public class ProjectAnalysisResult
    {
        public string DetectedFramework { get; set; } = string.Empty;  // "net472", "net48", etc
        public bool IsLegacy { get; set; }                              // true si es .NET Framework
        public int MigrationScore { get; set; }                        // 0-100
        public List<string> CsprojFiles { get; set; } = new();
        public List<string> CsFiles { get; set; } = new();
        public List<PackageInfo> Packages { get; set; } = new();
    }

    public class PackageInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public bool IsDeprecated { get; set; }
        public string? SuggestedReplacement { get; set; }
    }
}
