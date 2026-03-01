namespace NexShift.Core.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public string GitHubId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public PlanType Plan { get; set; } = PlanType.Free;
        public int ReposUsed { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navegación
        public ICollection<Repository> Repositories { get; set; } = new List<Repository>();
    }

    public enum PlanType
    {
        Free,       // 2 repos gratis
        Developer,  // $19/mes, ilimitado
        Team,       // $99/mes, 5 seats
        Enterprise  // $499/mes, self-hosted
    }
}
