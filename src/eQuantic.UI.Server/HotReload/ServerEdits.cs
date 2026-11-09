using System.Diagnostics;
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
    private long _taken;

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

    /// <summary>How many edits this process has taken.</summary>
    internal long Taken
    {
        get
        {
            lock (_gate) return _taken;
        }
    }

    private void Wake()
    {
        lock (_gate) Monitor.PulseAll(_gate);
    }

    /// <summary>One more edit was applied to this process.</summary>
    internal void Take()
    {
        lock (_gate)
        {
            _taken++;
            Monitor.PulseAll(_gate);
        }
    }

    /// <summary>
    /// Whether an edit arrived after <paramref name="seen"/> had been taken. When every save brings
    /// one, it is waited for until <see cref="ArrivalLimit"/> after <paramref name="savedAt"/>, a
    /// <see cref="Stopwatch"/> timestamp, or until <paramref name="stopping"/> is cancelled: an app
    /// that stops is not held by a wait for an edit it will never render.
    /// </summary>
    internal bool ArrivedAfter(long seen, long savedAt, CancellationToken stopping)
    {
        using var woken = stopping.Register(Wake);
        lock (_gate)
        {
            while (_taken <= seen)
            {
                if (!ArriveOnSave || stopping.IsCancellationRequested) return false;
                var left = ArrivalLimit - Stopwatch.GetElapsedTime(savedAt);
                if (left <= TimeSpan.Zero) return false;
                Monitor.Wait(_gate, left);
            }
            return true;
        }
    }
}
