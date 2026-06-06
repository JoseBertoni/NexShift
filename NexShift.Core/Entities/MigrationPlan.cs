namespace NexShift.Core.Entities;

public class MigrationPlan
{
    public string RepoUrl { get; set; } = string.Empty;
    public string TargetFramework { get; set; } = string.Empty;
    public ProjectType ProjectType { get; set; }
    public string DetectedFramework { get; set; } = string.Empty;

    // Lo que pasaría si se ejecutara la migración
    public List<PlannedChange> PlannedChanges { get; set; } = new();
    public List<BacklogItem> BacklogItems { get; set; } = new();

    // Estimaciones
    public int EstimatedAutomatedChanges { get; set; }
    public int EstimatedManualItems { get; set; }
    public int EstimatedReviewItems { get; set; }
    public int EstimatedAutomationPercentage { get; set; }

    // Stats de archivos
    public int TotalFilesScanned { get; set; }
    public int TotalFilesWithChanges { get; set; }
    public int TotalFilesClean { get; set; }

    // Pasos sugeridos ordenados
    public List<RoadmapStep> Roadmap { get; set; } = new();
}

public enum ProjectType
{
    AspNetMvc,
    WebApi,
    WcfService,
    WebForms,
    ConsoleApp,
    ClassLibrary,
    WorkerService,
    TestProject,
    Unknown
}

public class PlannedChange
{
    public string FilePath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ChangeConfidence Confidence { get; set; }
    // "Framework" | "Config" | "PureRegex" | "AI" | "Manual"
    public string TransformationType { get; set; } = string.Empty;
}

public class RoadmapStep
{
    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Risk { get; set; } = string.Empty;  // "Low" | "Medium" | "High"
    public bool IsAutomatable { get; set; }
    public int EstimatedFilesAffected { get; set; }
}
