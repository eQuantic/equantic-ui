using System.Collections.Concurrent;

namespace eQuantic.UI.Server.Tests.ServerEvents;

/// <summary>
/// A backplane two app instances in one test share, standing for the Redis or the bus a real
/// deployment plugs in: what either publishes is delivered to both.
/// </summary>
internal sealed class SharedBackplane : IServerEventBackplane
{
    private static readonly ConcurrentDictionary<Guid, Func<ServerEventEnvelope, ValueTask>> Receivers = new();

    public async ValueTask PublishAsync(ServerEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        foreach (var receive in Receivers.Values) await receive(envelope);
    }

    public ValueTask<IAsyncDisposable> SubscribeAsync(Func<ServerEventEnvelope, ValueTask> receive,
        CancellationToken cancellationToken = default)
    {
        var key = Guid.NewGuid();
        Receivers[key] = receive;
        return ValueTask.FromResult<IAsyncDisposable>(new Release(key));
    }

    private sealed class Release(Guid key) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            Receivers.TryRemove(key, out _);
            return ValueTask.CompletedTask;
        }
    }
}
