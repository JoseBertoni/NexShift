using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NexShift.Core.Entities;
using NexShift.Core.Interfaces;
using NexShift.Infrastructure.Data;

namespace NexShift.Infrastructure.Repositories;

public class BacklogRuleRepository : IBacklogRuleRepository
{
    private readonly NexShiftDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<BacklogRuleRepository> _logger;

    private const string CacheKey = "backlog_rules_active";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(1);

    public BacklogRuleRepository(
        NexShiftDbContext db,
        IMemoryCache cache,
        ILogger<BacklogRuleRepository> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BacklogRule>> GetAllActiveAsync(
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<BacklogRule>? cached) && cached != null)
        {
            _logger.LogDebug("BacklogRules obtenidas desde caché ({Count} reglas)", cached.Count);
            return cached;
        }

        _logger.LogInformation("BacklogRules no encontradas en caché — consultando BD");

        var rules = await _db.BacklogRules
            .Where(r => r.IsActive)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var cacheOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(CacheExpiration)
            .SetPriority(CacheItemPriority.High);

        _cache.Set(CacheKey, (IReadOnlyList<BacklogRule>)rules, cacheOptions);

        _logger.LogInformation("BacklogRules cargadas desde BD y cacheadas ({Count} reglas)", rules.Count);

        return rules;
    }

    public async Task<BacklogRule?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.BacklogRules
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<BacklogRule> CreateAsync(BacklogRule rule, CancellationToken cancellationToken = default)
    {
        rule.CreatedAt = DateTime.UtcNow;
        rule.UpdatedAt = DateTime.UtcNow;

        _db.BacklogRules.Add(rule);
        await _db.SaveChangesAsync(cancellationToken);

        InvalidateCache();

        _logger.LogInformation("BacklogRule creada: {Id} — {Pattern}", rule.Id, rule.Pattern);

        return rule;
    }

    public async Task<BacklogRule> UpdateAsync(BacklogRule rule, CancellationToken cancellationToken = default)
    {
        var existing = await _db.BacklogRules.FindAsync([rule.Id], cancellationToken)
            ?? throw new KeyNotFoundException($"BacklogRule {rule.Id} no encontrada");

        existing.Pattern = rule.Pattern;
        existing.Category = rule.Category;
        existing.Title = rule.Title;
        existing.Description = rule.Description;
        existing.Reason = rule.Reason;
        existing.IsActive = rule.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        InvalidateCache();

        _logger.LogInformation("BacklogRule actualizada: {Id} — {Pattern}", rule.Id, rule.Pattern);

        return existing;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _db.BacklogRules.FindAsync([id], cancellationToken)
            ?? throw new KeyNotFoundException($"BacklogRule {id} no encontrada");

        // Soft delete — nunca borramos físicamente
        existing.IsActive = false;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        InvalidateCache();

        _logger.LogInformation("BacklogRule desactivada (soft delete): {Id}", id);
    }

    public void InvalidateCache()
    {
        _cache.Remove(CacheKey);
        _logger.LogInformation("Caché de BacklogRules invalidada");
    }
}