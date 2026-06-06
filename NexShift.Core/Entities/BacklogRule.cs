namespace NexShift.Core.Entities;

public class BacklogRule
{
    public int Id { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;  // "NeedsReview" | "ManualRequired"
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}