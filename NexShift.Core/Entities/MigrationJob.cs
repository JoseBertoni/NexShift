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
