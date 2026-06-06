namespace NexShift.Core.Entities
{
    public class MigrationJob
    {
        public Guid Id { get; set; }
        public Guid RepositoryId { get; set; }
        public string TargetFramework { get; set; } = string.Empty; // "net8.0", "net9.0", "net10.0"
        public JobStatus Status { get; set; } = JobStatus.Queued;
        public string? ErrorMessage { get; set; }
        public string? ReportMarkdown { get; set; }   // El reporte de qué no se pudo migrar
        public string? OutputZipPath { get; set; }    // Path temporal del ZIP generado
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        // Progreso y estadísticas
        public int Progress { get; set; } = 0;
        public int MigrationPercentage { get; set; }
        public int AutomatedCount { get; set; }
        public int ManualCount { get; set; }
        public int ReviewCount { get; set; }

        // Build validation
        public string? BuildResultJson { get; set; }  // BuildResult serializado
        public bool? BuildSuccess { get; set; }        // null = no ejecutado aún
        public int BuildErrorCount { get; set; }
        public int BuildWarningCount { get; set; }

        // Navegación
        public Repository Repository { get; set; } = null!;
        public ICollection<MigrationIssue> Issues { get; set; } = new List<MigrationIssue>();
    }

    public enum JobStatus
    {
        Queued,      // En cola esperando procesarse
        Running,     // Procesándose ahora
        Completed,   // Terminó bien
        Failed       // Terminó mal
    }
}
