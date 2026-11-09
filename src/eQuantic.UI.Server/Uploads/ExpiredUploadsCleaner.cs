using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace eQuantic.UI.Server;

/// <summary>
/// Removes the uploads past their expiry, finished or not, their bytes and their records, when the app
/// starts and every <see cref="Interval"/> after. The upload folder is a staging area: an app takes a
/// file in its Server Action and stores it where it keeps its data.
/// </summary>
internal sealed class ExpiredUploadsCleaner(UploadStore store, TimeProvider time, ILogger<ExpiredUploadsCleaner> logger)
    : BackgroundService
{
    /// <summary>How often the folder is swept.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        do
        {
            try
            {
                var removed = await store.RemoveExpiredAsync(time.GetUtcNow(), stoppingToken);
                if (removed > 0) logger.LogInformation("Removed {Count} expired uploads", removed);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // One sweep that failed (a file held open, a folder briefly gone) is tried again at the next.
                logger.LogWarning(exception, "Could not remove the expired uploads");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
