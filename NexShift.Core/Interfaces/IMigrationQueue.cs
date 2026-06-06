namespace NexShift.Core.Interfaces;

public record MigrationJobRequest(
    Guid JobId,
    Guid RepositoryId,
    string RepoUrl,
    string TargetFramework);

public interface IMigrationQueue
{
    void Enqueue(MigrationJobRequest request);
    Task<MigrationJobRequest> DequeueAsync(CancellationToken cancellationToken);
}
