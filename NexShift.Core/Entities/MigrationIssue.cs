namespace NexShift.Core.Entities
{
    public class MigrationIssue
    {
        public Guid Id { get; set; }
        public Guid MigrationJobId { get; set; }
        public string FileName { get; set; } = string.Empty;   // Qué archivo tuvo el problema
        public string Description { get; set; } = string.Empty; // Qué pasó
        public string? Suggestion { get; set; }                 // Qué debería hacer el usuario
        public IssueSeverity Severity { get; set; }

        // Navegación
        public MigrationJob MigrationJob { get; set; } = null!;
    }

    public enum IssueSeverity
    {
        Info,     // Solo informativo
        Warning,  // Puede funcionar pero hay que revisar
        Error     // Requiere intervención manual obligatoria
    }
}
