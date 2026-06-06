namespace NexShift.Core.Entities;

public class MigrationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public byte[]? ZipBytes { get; set; }
    public List<MigrationChange> Changes { get; set; } = new();
    public List<BacklogItem> BacklogItems { get; set; } = new();
    public int MigrationPercentage { get; set; }
    public int AutomatedCount { get; set; }
    public int ManualCount { get; set; }
    public int ReviewCount { get; set; }

    // before/after por archivo — usado para el diff en el reporte
    public Dictionary<string, FileDiff> FileDiffs { get; set; } = new();

    // Resultado de dotnet build sobre los archivos transformados
    public BuildResult? BuildResult { get; set; }
}

/// <summary>
/// Contenido original y transformado de un archivo .cs modificado.
/// </summary>
public record FileDiff(string Original, string Transformed);

public class MigrationChange
{
    public string FilePath { get; set; } = string.Empty;
    public ChangeType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public MigrationCategory Category { get; set; }
    public ChangeConfidence Confidence { get; set; } = ChangeConfidence.Safe;
}

public class BacklogItem
{
    public string FilePath { get; set; } = string.Empty;
    public BacklogCategory Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public enum ChangeType { Modified, Created, Deleted }

public enum MigrationCategory
{
    Automated,      // ✅ Lo hice solo
    NeedsReview,    // ⚠️  Revisá
    ManualRequired  // ❌ Requiere humano
}

public enum BacklogCategory
{
    NeedsReview,    // ⚠️  Transformado pero revisá
    ManualRequired  // ❌ No se puede automatizar
}

public enum ChangeConfidence
{
    Safe,           // Regex determinista — sin ambigüedad
    ReviewRequired, // Transformado por IA — puede haber edge cases
    HighRisk        // Patrón crítico: auth, threading, serialización, COM
}
