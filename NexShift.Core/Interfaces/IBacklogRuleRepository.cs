using NexShift.Core.Entities;

namespace NexShift.Core.Interfaces;

public interface IBacklogRuleRepository
{
    Task<IReadOnlyList<BacklogRule>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<BacklogRule?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<BacklogRule> CreateAsync(BacklogRule rule, CancellationToken cancellationToken = default);
    Task<BacklogRule> UpdateAsync(BacklogRule rule, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    void InvalidateCache();
}