using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// The v1 frame driver for a Photon surface: holds a RETAINED root node/component, rebuilds when its
/// state invalidates, and dispatches taps to the realized hit regions. This is the minimal loop that
/// proves Build → SetState → rebuild end-to-end; the platform shells (W5) wrap it with vsync pacing
/// and real input, and the reconciler (W6) replaces full rebuilds with diffing. State persists on the
/// retained root (and any component instances the tree retains) — see the
/// <see cref="StatefulComponent"/> v1 caveat.
/// </summary>
public sealed class PhotonHost
{
    private readonly VisualNode _root;
    private readonly IAppTheme _theme;
    private readonly ITextMeasurer? _measurer;
    private readonly float _typeScale;
    private RealizeResult? _lastFrame;

    public PhotonHost(VisualNode root, IAppTheme theme, ThemeMode mode, float width, float height,
        ITextMeasurer? measurer = null, float typeScale = 1f)
    {
        _root = root;
        _theme = theme;
        Mode = mode;
        Width = width;
        Height = height;
        _measurer = measurer;
        _typeScale = typeScale;

        if (root is StatefulComponent stateful)
            stateful.StateInvalidated += () => NeedsRender = true;
    }

    public ThemeMode Mode { get; set; }
    public float Width { get; }
    public float Height { get; }

    /// <summary>True when state changed since the last <see cref="RenderFrame"/> (starts true).</summary>
    public bool NeedsRender { get; private set; } = true;

    /// <summary>
    /// Builds one frame: clears to the theme background, then lays out and lowers the root via
    /// <see cref="PhotonRealizer"/>. Returns the realized frame (layout tree + hit regions).
    /// </summary>
    public RealizeResult RenderFrame(DisplayListBuilder builder)
    {
        builder.Clear(_theme.Background.Resolve(Mode));
        _lastFrame = PhotonRealizer.Realize(_root, Width, Height, _theme, Mode, builder, _measurer, _typeScale, _pressed);
        NeedsRender = false;
        return _lastFrame;
    }

    /// <summary>
    /// Dispatches a tap against the LAST rendered frame: the topmost containing hit region (paint
    /// order — last registered wins, matching Stack semantics) receives the press; disabled regions
    /// swallow the tap without firing (they still exist for accessibility). Returns whether any
    /// region was hit.
    /// </summary>
    private Pressable? _pressed;

    /// <summary>The Pressable currently held down (pressed visuals render while set).</summary>
    public Pressable? Pressed => _pressed;

    /// <summary>
    /// Begins a press: the topmost enabled hit region under the point becomes the pressed node and
    /// the next frame renders its pressed token swap. Returns whether a region captured the press.
    /// (v1 fence: drag-slop/cancel and fling join the gesture system.)
    /// </summary>
    public bool PressDown(float x, float y)
    {
        if (_lastFrame is null) return false;
        var point = new Point(x, y);
        var regions = _lastFrame.HitRegions;
        for (var i = regions.Count - 1; i >= 0; i--)
        {
            var region = regions[i];
            if (!region.Bounds.Contains(point)) continue;
            if (region.Node.Disabled) return true; // swallowed, no visual
            _pressed = region.Node;
            NeedsRender = true;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Ends a press: fires <c>OnPressed</c> when the release lands inside the pressed node's region
    /// (release outside cancels), clears the pressed visual either way.
    /// </summary>
    public bool PressUp(float x, float y)
    {
        var pressed = _pressed;
        if (pressed is null) return false;
        _pressed = null;
        NeedsRender = true;

        var point = new Point(x, y);
        var regions = _lastFrame?.HitRegions;
        if (regions is null) return false;
        for (var i = regions.Count - 1; i >= 0; i--)
        {
            var region = regions[i];
            if (!ReferenceEquals(region.Node, pressed)) continue;
            if (!region.Bounds.Contains(point)) return false; // canceled by releasing outside
            pressed.OnPressed?.Invoke();
            return true;
        }
        return false;
    }

    public bool Tap(float x, float y)
    {
        if (_lastFrame is null) return false;

        var point = new Point(x, y);
        var regions = _lastFrame.HitRegions;
        for (var i = regions.Count - 1; i >= 0; i--)
        {
            var region = regions[i];
            if (!region.Bounds.Contains(point)) continue;
            if (!region.Node.Disabled) region.Node.OnPressed?.Invoke();
            return true;
        }
        return false;
    }
}
