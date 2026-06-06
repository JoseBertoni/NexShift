using NexShift.Core.Entities;

namespace NexShift.Core.Interfaces
{
    public interface IProjectAnalyzer
    {
        Task<ProjectAnalysisResult> AnalyzeAsync(string repoUrl, CancellationToken cancellationToken = default);
    }

    public class ProjectAnalysisResult
    {
        public string DetectedFramework { get; set; } = string.Empty;
        public bool IsLegacy { get; set; }
        public int MigrationScore { get; set; }
        public ProjectType ProjectType { get; set; }
        public List<string> CsprojFiles { get; set; } = new();
        public List<string> CsFiles { get; set; } = new();
        public List<PackageInfo> Packages { get; set; } = new();
        public CodeFindings Findings { get; set; } = new();
        public List<ScoreFactor> ScoreBreakdown { get; set; } = new();
    }

    public class PackageInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public bool IsDeprecated { get; set; }
        public string? SuggestedReplacement { get; set; }
    }

    /// <summary>
    /// Hallazgos obtenidos escaneando hasta 50 archivos .cs representativos.
    /// Usados para el Migration Score y el Score Breakdown detallado.
    /// </summary>
    public class CodeFindings
    {
        public int FilesWithSystemWeb { get; set; }
        public int FilesWithWcf { get; set; }
        public int FilesWithWebForms { get; set; }
        public int FilesWithBinaryFormatter { get; set; }
        public int FilesWithAppDomain { get; set; }
        public int FilesWithRemoting { get; set; }
        public int FilesWithWindowsOnlyApis { get; set; }
        public int FilesWithLegacyAuth { get; set; }
        public int FilesWithHttpContextCurrent { get; set; }
        public int FilesWithConfigurationManager { get; set; }
        public int FilesWithThreadAbort { get; set; }
        public int TotalFilesScanned { get; set; }
        public bool HasAspxFiles { get; set; }
        public bool HasSvcFiles { get; set; }
    }

    /// <summary>
    /// Un factor individual del Migration Score.
    /// Impact negativo = penalización, positivo = bonus.
    /// </summary>
    public class ScoreFactor
    {
        public string Factor { get; set; } = string.Empty;
        public int Impact { get; set; }
        public string Details { get; set; } = string.Empty;
    }
}
