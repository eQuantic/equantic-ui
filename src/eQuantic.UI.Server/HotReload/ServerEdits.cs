using System.Reflection.Metadata;

namespace eQuantic.UI.Server.HotReload;

/// <summary>
/// The edits this server has taken while it runs: the changes dotnet watch applies to the running
/// process when a source is saved, counted as .NET reports them (<see cref="ServerEditsHandler"/>).
/// <para>
/// A rebuild reads it to say what the page it reloads will be served: markup the server rendered
/// with the edit, which the new modules can hydrate, or markup from before it, which they must draw
/// over. Each save used to reload the page twice, once by dotnet watch with the edited server and the
/// old modules and once by the rebuild, and the page ran code that did not match what it showed in
/// between (#1275).
/// </para>
/// </summary>
internal sealed class ServerEdits
{
    /// <summary>
    /// How long a save's edit takes to reach this server at the most: dotnet watch compiles and applies
    /// it in a few hundred milliseconds, measured at 140ms on a machine at a load average of 22 (#900)
    /// and at 1.4s on the same machine at 70 (#1275). Missing it costs the hydration, since the page
    /// then draws over markup it cannot trust. Waiting for it costs only a save that brings no edit
    /// (one that changes nothing .NET compiles), which has nothing new to show, counted from the save.
    /// </summary>
    internal static readonly TimeSpan Within = TimeSpan.FromSeconds(5);

    /// <summary>
    /// This process's. Every save brings it its edit when dotnet watch runs it and .NET can apply edits
    /// to it, and only then: a debugger applies them when the person asks, never on a save.
    /// </summary>
    internal static ServerEdits OfThisProcess { get; } = new(
        MetadataUpdater.IsSupported && Environment.GetEnvironmentVariable("DOTNET_WATCH") == "1", Within);

    private readonly object _gate = new();
    private DateTime _lastTakenUtc = DateTime.MinValue;

    /// <summary>The edits of a process, which a test plays.</summary>
    internal ServerEdits(bool arriveOnSave, TimeSpan within)
    {
        ArriveOnSave = arriveOnSave;
        ArrivalLimit = within;
    }

    /// <summary>Whether every save brings this process its edit, which a rebuild then waits for.</summary>
    internal bool ArriveOnSave { get; }

    /// <summary>How long after a save its edit may still arrive.</summary>
    internal TimeSpan ArrivalLimit { get; }

    private void Wake()
    {
        lock (_gate) Monitor.PulseAll(_gate);
    }

    /// <summary>
    /// Why a rebuild of the save written at <paramref name="writtenUtc"/> found no edit of it, said on the
    /// line the server writes when the page mounts: a failure of the edit-to-view measurement used to say
    /// only that the page drew over, and not whether the edit came late or never (#1373).
    /// </summary>
    internal string WhyNotSince(DateTime writtenUtc)
    {
        if (!ArriveOnSave) return "no save brings this server its edit, since dotnet watch does not run it";
        DateTime last;
        lock (_gate) last = _lastTakenUtc;
        return last == DateTime.MinValue
            ? $"the save was written at {writtenUtc:HH:mm:ss.fff}, and no edit has reached this server since it started"
            : $"the save was written at {writtenUtc:HH:mm:ss.fff}, and the last edit reached this server at {last:HH:mm:ss.fff}, {(writtenUtc - last).TotalMilliseconds:F0}ms before it";
    }

    /// <summary>One more edit was applied to this process, now.</summary>
    internal void Take()
    {
        lock (_gate)
        {
            _lastTakenUtc = DateTime.UtcNow;
            Monitor.PulseAll(_gate);
        }
    }

    /// <summary>
    /// Whether an edit was applied to this process at or after <paramref name="writtenUtc"/>, when the
    /// files a save changed were last written. A save's edit is compiled from what it wrote, so it is
    /// applied after that, whenever this server hears of the save: a count taken when the server's own
    /// watcher saw the save missed an edit dotnet watch had applied first, which it does in a few tens
    /// of milliseconds, and the page drew over markup that held the edit (#1275). When every save brings
    /// one, it is waited for until <see cref="ArrivalLimit"/> after the write, or until
    /// <paramref name="stopping"/> is cancelled: an app that stops is not held by a wait for an edit it
    /// will never render.
    /// </summary>
    internal bool ArrivedSince(DateTime writtenUtc, CancellationToken stopping)
    {
        using var woken = stopping.Register(Wake);
        lock (_gate)
        {
            while (_lastTakenUtc < writtenUtc)
            {
                if (!ArriveOnSave || stopping.IsCancellationRequested) return false;
                var left = writtenUtc + ArrivalLimit - DateTime.UtcNow;
                if (left <= TimeSpan.Zero) return false;
                Monitor.Wait(_gate, left);
            }
            return true;
        }
    }
}
