namespace eQuantic.UI.Native.Engine;

/// <summary>
/// The render targets a frame draws its own passes into: a group's layer, a backdrop's content, the
/// levels of its blur (#1952). They used to be created at the surface's size and disposed in every
/// frame, which on a 2880 by 1800 window is 20.7 MB of allocation a frame for a single layer, and the
/// window drew none of them at all.
/// <para>
/// A target is RENTED for as long as a pass may still draw into it or read it, and given back when
/// the frame is done with it, so the next pass or the next frame of the same size takes it again. The
/// pool keeps what the frames draw: a target no frame has rented for
/// <see cref="RhiRenderer.RetainFrames"/> frames is released, as a raster is, and so is every target
/// when the surface changes size, since every size a frame asks for comes from the surface's. A
/// window being resized therefore holds one frame's targets, not thirty frames of sizes it has left.
/// </para>
/// <para>
/// Released while work may still read it, as every <see cref="IRhiTexture"/> is: the backend keeps a
/// target alive until the work already submitted that reads it is done.
/// </para>
/// </summary>
internal sealed class RhiTargetPool : IDisposable
{
    private readonly IRhiDevice _device;
    private readonly List<Entry> _entries = new();
    private (int Width, int Height) _surface;
    private long _frame;

    private sealed class Entry(IRhiRenderTarget target, long frame)
    {
        public readonly IRhiRenderTarget Target = target;
        public bool Rented = true;
        public long LastFrame = frame;
    }

    public RhiTargetPool(IRhiDevice device) => _device = device;

    /// <summary>
    /// A frame of <paramref name="width"/> by <paramref name="height"/> starts. Every target is back in
    /// the pool, whatever the frame before did with it, and the ones this frame cannot use are released:
    /// all of them when the surface changed size, otherwise those no frame rented for longer than they
    /// are kept.
    /// </summary>
    public void BeginFrame(long frame, int width, int height)
    {
        _frame = frame;
        var resized = (width, height) != _surface;
        _surface = (width, height);
        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            entry.Rented = false;
            // One rented at frame f was away for (frame - f - 1) frames, counted as rasters are.
            if (!resized && frame - entry.LastFrame - 1 <= RhiRenderer.RetainFrames) continue;
            // Out of the list first: a release that throws must not leave an entry whose target is gone.
            _entries.RemoveAt(i);
            entry.Target.Dispose();
        }
    }

    /// <summary>A target of this size no pass of the frame holds, created only when the pool has none.</summary>
    public IRhiRenderTarget Rent(int width, int height)
    {
        foreach (var entry in _entries)
        {
            if (entry.Rented || entry.Target.Width != width || entry.Target.Height != height) continue;
            entry.Rented = true;
            entry.LastFrame = _frame;
            return entry.Target;
        }
        var target = _device.CreateRenderTarget(width, height);
        _entries.Add(new Entry(target, _frame));
        return target;
    }

    /// <summary>Gives <paramref name="target"/> back: no pass of this frame draws into it or reads it any more.</summary>
    public void Return(IRhiRenderTarget target)
    {
        foreach (var entry in _entries)
        {
            if (!ReferenceEquals(entry.Target, target)) continue;
            entry.Rented = false;
            return;
        }
    }

    public void Dispose()
    {
        foreach (var entry in _entries) entry.Target.Dispose();
        _entries.Clear();
    }
}
