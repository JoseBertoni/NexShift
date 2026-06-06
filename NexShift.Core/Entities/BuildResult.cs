namespace NexShift.Core.Entities;

public class BuildResult
{
    public bool Success { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public List<BuildDiagnostic> Errors { get; set; } = new();
    public List<BuildDiagnostic> Warnings { get; set; } = new();
    public string? RawOutput { get; set; }
    public TimeSpan Duration { get; set; }

    // Si no se pudo correr el build (ej: SDK no instalado)
    public bool WasExecuted { get; set; }
    public string? ExecutionError { get; set; }
}

public class BuildDiagnostic
{
    public string FilePath { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
    public string Code { get; set; } = string.Empty;   // "CS0246", "CS1002", etc.
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // "error" | "warning"
}
