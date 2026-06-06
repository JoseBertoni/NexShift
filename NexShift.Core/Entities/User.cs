namespace NexShift.Core.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public long GitHubId { get; set; }
        public string Login { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Email { get; set; }
        public bool IsAdmin { get; set; } = false;
        public string? AvatarUrl { get; set; }
        public string GitHubAccessToken { get; set; } = string.Empty;
        public PlanType Plan { get; set; } = PlanType.Free;
        public int ReposUsed { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
        public ICollection<Repository> Repositories { get; set; } = new List<Repository>();
    }

    public enum PlanType
    {
        Free,
        Developer,
        Team,
        Enterprise
    }
}
