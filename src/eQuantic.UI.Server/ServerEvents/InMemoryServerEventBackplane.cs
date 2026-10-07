using System.Collections.Immutable;

namespace eQuantic.UI.Server;

/// <summary>
/// The default backplane: one process, so publishing is delivering. It keeps the order events were
/// published in, per topic. An app that runs several instances registers its own.
/// </summary>
internal sealed class InMemoryServerEventBackplane : IServerEventBackplane
{
    private ImmutableArray<Func<ServerEventEnvelope, ValueTask>> _receivers = [];

    public async ValueTask PublishAsync(ServerEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        foreach (var receive in _receivers) await receive(envelope);
    }

    public ValueTask<IAsyncDisposable> SubscribeAsync(Func<ServerEventEnvelope, ValueTask> receive,
        CancellationToken cancellationToken = default)
    {
        ImmutableInterlocked.Update(ref _receivers, receivers => receivers.Add(receive));
        return ValueTask.FromResult<IAsyncDisposable>(new Subscription(this, receive));
    }

    private sealed class Subscription(InMemoryServerEventBackplane backplane, Func<ServerEventEnvelope, ValueTask> receive)
        : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            ImmutableInterlocked.Update(ref backplane._receivers, receivers => receivers.Remove(receive));
            return ValueTask.CompletedTask;
        }
    }
}
