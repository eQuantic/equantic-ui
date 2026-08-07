using System.Reflection.Metadata;

[assembly: MetadataUpdateHandler(typeof(eQuantic.UI.Native.Components.PhotonHotReload))]

namespace eQuantic.UI.Native.Components;

/// <summary>
/// The seam .NET Hot Reload pulls when `dotnet watch` lands an edit in a RUNNING Photon app.
/// <para>
/// Everything else was already in place, which is why this file is small: the tree is re-expanded
/// from the retained root on every frame (LayoutEngine calls <c>Build</c> during measure), and
/// component state lives in a path-keyed store rather than in the objects an edit replaces — so a
/// patched method body simply takes effect on the next frame, state intact. What was missing was
/// the next frame ITSELF: an idle window presents nothing, an idle phone ticks its vsync and skips,
/// and a metadata update arrives with no input event attached. The edit applied, and nothing moved
/// until the mouse did — which reads as "hot reload doesn't work" while it is working perfectly.
/// </para>
/// <para>
/// So this does two things. It wakes every live host (a bool the render loops already poll — the
/// macOS loop within 50ms, the phones on their next vsync), and it bumps a GENERATION the host
/// compares on its render thread to drop the content caches once. The caches are content-keyed, so
/// most edits would miss them naturally — but a patched rasterizer, an edited icon path, or a
/// changed static feeding a cached key are exactly the edits a person makes while tuning visuals,
/// and serving yesterday's pixels for them turns a working reload into a haunted one. Nothing is
/// mutated from THIS thread: the handler runs wherever the runtime applies the delta, and a bool
/// write plus an interlocked increment are the only things safe to do from there.
/// </para>
/// <para>
/// Rude edits (a new type, a changed signature) are the runtime's to refuse — `dotnet watch`
/// answers those with a restart, which is the correct behaviour and needs nothing from us.
/// </para>
/// </summary>
public static class PhotonHotReload
{
    private static readonly List<WeakReference<PhotonHost>> Hosts = [];
    private static int _generation;

    /// <summary>Bumped once per applied edit. A host that sees a number it has not seen before
    /// drops its content caches — on its own render thread, never on this one.</summary>
    public static int Generation => Volatile.Read(ref _generation);

    /// <summary>Every live host registers at construction; the reference is WEAK, so a host that
    /// belonged to a closed window (or a finished test) costs nothing and pins nothing.</summary>
    internal static void Register(PhotonHost host)
    {
        lock (Hosts)
        {
            Hosts.RemoveAll(reference => !reference.TryGetTarget(out _));
            Hosts.Add(new WeakReference<PhotonHost>(host));
        }
    }

    /// <summary>Runtime contract: caches keyed on updated metadata may be stale. The work happens
    /// per-host on the render thread; here the generation just moves.</summary>
    public static void ClearCache(Type[]? updatedTypes) => Interlocked.Increment(ref _generation);

    /// <summary>Runtime contract: the update is applied — make it VISIBLE. Wakes every live host;
    /// the loops present within a tick and the new bodies draw with all state intact.</summary>
    public static void UpdateApplication(Type[]? updatedTypes)
    {
        lock (Hosts)
        {
            Hosts.RemoveAll(reference => !reference.TryGetTarget(out _));
            foreach (var reference in Hosts)
                if (reference.TryGetTarget(out var host)) host.Invalidate();
        }
    }
}
