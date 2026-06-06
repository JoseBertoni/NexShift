using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NexShift.Core.Entities;
using NexShift.Core.Interfaces;
using NexShift.Infrastructure.Data;

namespace NexShift.Infrastructure.Repositories;

public class TransformationRuleRepository : ITransformationRuleRepository
{
    private readonly NexShiftDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TransformationRuleRepository> _logger;

    private const string CacheKey = "transformation_rules_active";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(1);

    public TransformationRuleRepository(
        NexShiftDbContext db,
        IMemoryCache cache,
        ILogger<TransformationRuleRepository> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TransformationRule>> GetAllActiveAsync(
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<TransformationRule>? cached) && cached != null)
        {
            _logger.LogDebug("TransformationRules loaded from cache ({Count} rules)", cached.Count);
            return cached;
        }

        _logger.LogInformation("TransformationRules not found in cache — querying DB");

        var rules = await _db.TransformationRules
            .Where(r => r.IsActive)
            .OrderBy(r => r.Priority)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var cacheOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(CacheExpiration)
            .SetPriority(CacheItemPriority.High);

        _cache.Set(CacheKey, (IReadOnlyList<TransformationRule>)rules, cacheOptions);

        _logger.LogInformation("TransformationRules loaded from DB and cached ({Count} rules)", rules.Count);

        return rules;
    }

    public async Task<TransformationRule?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.TransformationRules
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<TransformationRule> CreateAsync(TransformationRule rule, CancellationToken cancellationToken = default)
    {
        rule.CreatedAt = DateTime.UtcNow;
        rule.UpdatedAt = DateTime.UtcNow;

        _db.TransformationRules.Add(rule);
        await _db.SaveChangesAsync(cancellationToken);

        InvalidateCache();

        _logger.LogInformation("TransformationRule created: {Id} — {Pattern}", rule.Id, rule.Pattern);

        return rule;
    }

    public async Task<TransformationRule> UpdateAsync(TransformationRule rule, CancellationToken cancellationToken = default)
    {
        var existing = await _db.TransformationRules.FindAsync([rule.Id], cancellationToken)
            ?? throw new KeyNotFoundException($"TransformationRule {rule.Id} not found");

        existing.Pattern = rule.Pattern;
        existing.Replacement = rule.Replacement;
        existing.NeedsAI = rule.NeedsAI;
        existing.IsRegex = rule.IsRegex;
        existing.Priority = rule.Priority;
        existing.Description = rule.Description;
        existing.IsActive = rule.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        InvalidateCache();

        _logger.LogInformation("TransformationRule updated: {Id} — {Pattern}", rule.Id, rule.Pattern);

        return existing;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _db.TransformationRules.FindAsync([id], cancellationToken)
            ?? throw new KeyNotFoundException($"TransformationRule {id} not found");

        // Soft delete — never physically delete
        existing.IsActive = false;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        InvalidateCache();

        _logger.LogInformation("TransformationRule soft deleted: {Id}", id);
    }

    public void InvalidateCache()
    {
        _cache.Remove(CacheKey);
        _logger.LogInformation("TransformationRules cache invalidated");
    }
}