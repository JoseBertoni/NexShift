namespace NexShift.Core.Entities
{
    public class NugetPackage
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? LatestVersion { get; set; }
        public bool IsDeprecated { get; set; }
        public string? SuggestedReplacement { get; set; }
        public string? Reason { get; set; }
        public DateTime LastChecked { get; set; } = DateTime.UtcNow;
        public bool IsManuallyAdded { get; set; } = false;
    }
}
