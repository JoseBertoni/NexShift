using System.Threading.Channels;
using NexShift.Core.Interfaces;

namespace NexShift.Infrastructure.Services;

public class MigrationQueue : IMigrationQueue
{
    private readonly Channel<MigrationJobRequest> _channel =
        Channel.CreateUnbounded<MigrationJobRequest>(
            new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(MigrationJobRequest request) =>
        _channel.Writer.TryWrite(request);

    public async Task<MigrationJobRequest> DequeueAsync(CancellationToken cancellationToken) =>
        await _channel.Reader.ReadAsync(cancellationToken);
}
