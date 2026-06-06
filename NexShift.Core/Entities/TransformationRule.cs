namespace NexShift.Core.Entities
{
    public class TransformationRule
    {
        public int Id { get; set; }
        public string Pattern { get; set; } = string.Empty;
        public string? Replacement { get; set; }
        public bool NeedsAI { get; set; } = false;
        public bool IsRegex { get; set; } = false;
        public int Priority { get; set; } = 0;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
