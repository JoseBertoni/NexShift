using NexShift.Core.Entities;

namespace NexShift.Core.Interfaces;

public interface IBacklogDetector
{
    Task<List<BacklogItem>> DetectAsync(string filePath, string content, CancellationToken cancellationToken = default);
}