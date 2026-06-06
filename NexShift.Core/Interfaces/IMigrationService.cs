using NexShift.Core.Entities;

namespace NexShift.Core.Interfaces;

public interface IMigrationService
{
    Task<MigrationResult> MigrateAsync(
        string repoUrl,
        string targetFramework = "net8.0",
        Dictionary<string, string>? decisions = null,
        CancellationToken cancellationToken = default);

    Task<MigrationPlan> PlanAsync(
        string repoUrl,
        string targetFramework = "net8.0",
        CancellationToken cancellationToken = default);
}