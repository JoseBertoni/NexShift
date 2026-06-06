using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NexShift.Core.Entities;
using NexShift.Core.Interfaces;
using NexShift.Infrastructure.Data;

namespace NexShift.Infrastructure.Repositories;

public class KnownDeprecatedPackageRepository : IKnownDeprecatedPackageRepository
{
    private readonly NexShiftDbContext _db;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "known_deprecated_packages";

    public KnownDeprecatedPackageRepository(NexShiftDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<IReadOnlyList<KnownDeprecatedPackage>> GetAllActiveAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<KnownDeprecatedPackage>? cached) && cached != null)
            return cached;

        var packages = await _db.KnownDeprecatedPackages
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        _cache.Set(CacheKey, (IReadOnlyList<KnownDeprecatedPackage>)packages,
            TimeSpan.FromHours(1));

        return packages;
    }

    public async Task<KnownDeprecatedPackage?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var all = await GetAllActiveAsync(ct);
        return all.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<KnownDeprecatedPackage> AddAsync(KnownDeprecatedPackage package, CancellationToken ct = default)
    {
        package.Id = Guid.NewGuid();
        package.CreatedAt = DateTime.UtcNow;
        package.UpdatedAt = DateTime.UtcNow;
        _db.KnownDeprecatedPackages.Add(package);
        await _db.SaveChangesAsync(ct);
        await InvalidateCacheAsync();
        return package;
    }

    public async Task UpdateAsync(KnownDeprecatedPackage package, CancellationToken ct = default)
    {
        package.UpdatedAt = DateTime.UtcNow;
        _db.KnownDeprecatedPackages.Update(package);
        await _db.SaveChangesAsync(ct);
        await InvalidateCacheAsync();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var package = await _db.KnownDeprecatedPackages.FindAsync([id], ct);
        if (package is null) return;
        package.IsActive = false;   // soft delete
        package.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await InvalidateCacheAsync();
    }

    public Task InvalidateCacheAsync()
    {
        _cache.Remove(CacheKey);
        return Task.CompletedTask;
    }
}