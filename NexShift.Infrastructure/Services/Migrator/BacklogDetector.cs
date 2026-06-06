using Microsoft.Extensions.Logging;
using NexShift.Core.Entities;
using NexShift.Core.Interfaces;

namespace NexShift.Infrastructure.Services.Migrator;

public class BacklogDetector : IBacklogDetector
{
    private readonly IBacklogRuleRepository _repository;
    private readonly ILogger<BacklogDetector> _logger;

    public BacklogDetector(
        IBacklogRuleRepository repository,
        ILogger<BacklogDetector> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<List<BacklogItem>> DetectAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken = default)
    {
        var items = new List<BacklogItem>();

        // Cargar reglas — viene de caché si está disponible, BD si no
        var rules = await _repository.GetAllActiveAsync(cancellationToken);

        foreach (var rule in rules)
        {
            if (!content.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase))
                continue;

            // Evitar duplicados — si ya agregamos este título para este archivo, skip
            if (items.Any(i => i.Title == rule.Title && i.FilePath == filePath))
                continue;

            var category = rule.Category == "ManualRequired"
                ? BacklogCategory.ManualRequired
                : BacklogCategory.NeedsReview;

            items.Add(new BacklogItem
            {
                FilePath = filePath,
                Category = category,
                Title = rule.Title,
                Description = rule.Description,
                Reason = rule.Reason
            });

            _logger.LogDebug("Backlog item detectado: {Title} en {File}", rule.Title, filePath);
        }

        return items;
    }
}