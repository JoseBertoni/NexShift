using NexShift.Core.Entities;

namespace NexShift.Core.Interfaces
{
    public interface IJwtService
    {
        string GenerateToken(User user);
        Guid? ValidateToken(string token);
    }
}
