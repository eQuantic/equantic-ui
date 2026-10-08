using System.Globalization;
using eQuantic.UI.Primitives;
using eQuantic.UI.Server;

namespace eQuantic.Console;

/// <summary>
/// The server's half of the live screen: a quote every second, published to the topic the page
/// subscribes to, with its sequence as the event's id. <c>[ServerOnly]</c>, because no page builds
/// it: it sits in the app's project beside the screens, and the compiler emits a module for a class
/// unless it is told the class never runs in a browser.
/// </summary>
[ServerOnly]
public sealed class LiveQuotePublisher(IServerEventPublisher publisher) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var price = 100m;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            for (long sequence = 1; await timer.WaitForNextTickAsync(stoppingToken); sequence++)
            {
                price = Math.Max(1m, Math.Round(price + (decimal)(Random.Shared.NextDouble() - 0.5), 2));
                await publisher.PublishAsync(LiveScreen.Quotes, new LiveQuote("EQ", price, sequence),
                    new ServerEventPublishOptions { Id = sequence.ToString(CultureInfo.InvariantCulture) },
                    stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The app is stopping.
        }
    }
}
