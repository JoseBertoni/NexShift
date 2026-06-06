using NexShift.Core.Entities;

namespace NexShift.Core.Interfaces;

public interface ITransformationRuleRepository
{
    Task<IReadOnlyList<TransformationRule>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<TransformationRule?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<TransformationRule> CreateAsync(TransformationRule rule, CancellationToken cancellationToken = default);
    Task<TransformationRule> UpdateAsync(TransformationRule rule, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    void InvalidateCache();
}