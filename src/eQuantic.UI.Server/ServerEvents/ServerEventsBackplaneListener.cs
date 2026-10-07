using Microsoft.Extensions.Hosting;

namespace eQuantic.UI.Server;

/// <summary>Subscribes this instance's connections to the backplane while the app runs, so an event any
/// instance publishes reaches the pages connected here.</summary>
internal sealed class ServerEventsBackplaneListener(IServerEventBackplane backplane, ServerEventConnections connections)
    : IHostedService
{
    private IAsyncDisposable? _subscription;

    public async Task StartAsync(CancellationToken cancellationToken) =>
        _subscription = await backplane.SubscribeAsync(connections.DeliverAsync, cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_subscription is not null) await _subscription.DisposeAsync();
    }
}
