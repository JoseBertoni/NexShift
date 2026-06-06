namespace NexShift.Core.Entities
{
    public class Repository
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? DetectedFramework { get; set; }  // "net472", "net48", etc
        public int MigrationScore { get; set; }          // 0-100
        public RepositoryStatus Status { get; set; } = RepositoryStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? AnalysisResultJson { get; set; } // JSON del análisis completo
        public bool IsLegacy { get; set; }
        public int TotalCsFiles { get; set; }
        public int TotalProjects { get; set; }
        public int TotalPackages { get; set; }
        public int DeprecatedPackages { get; set; }
        public string? IpAddress { get; set; }

        // Navegación
        public User? User { get; set; } = null!;
        public ICollection<MigrationJob> MigrationJobs { get; set; } = new List<MigrationJob>();
    }

    public enum RepositoryStatus
    {
        Pending,    // Recién ingresado
        Analyzing,  // El Analyzer está corriendo
        Analyzed,   // Análisis terminado, esperando que el usuario decida
        Migrating,  // El Migrator está corriendo
        Completed,  // Todo listo, ZIP disponible
        Failed      // Algo salió mal
    }
}
