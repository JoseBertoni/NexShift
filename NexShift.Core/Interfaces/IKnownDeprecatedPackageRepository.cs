using NexShift.Core.Entities;

namespace NexShift.Core.Interfaces;

public interface IKnownDeprecatedPackageRepository
{
    Task<IReadOnlyList<KnownDeprecatedPackage>> GetAllActiveAsync(CancellationToken ct = default);
    Task<KnownDeprecatedPackage?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<KnownDeprecatedPackage> AddAsync(KnownDeprecatedPackage package, CancellationToken ct = default);
    Task UpdateAsync(KnownDeprecatedPackage package, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task InvalidateCacheAsync();
}