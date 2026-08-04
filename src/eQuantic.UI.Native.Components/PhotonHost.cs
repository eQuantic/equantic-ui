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
        _instances.InstanceRetained += retained =>
            retained.StateInvalidated += () => NeedsRender = true;
    }

    public ThemeMode Mode { get; set; }
    public float Width { get; private set; }
    public float Height { get; private set; }

    /// <summary>True when state changed since the last <see cref="RenderFrame"/> (starts true), or
    /// when the last frame carried running loop motion — animated frames keep the loop hot.</summary>
    public bool NeedsRender { get; private set; } = true;

    /// <summary>
    /// Something OUTSIDE the tree changed and the next frame will differ — the system switched to
    /// dark, reduce-motion came on, a font finished loading. The tree is the same; what it paints
    /// is not, and nothing inside it would know to say so.
    /// </summary>
    public void Invalidate() => NeedsRender = true;

    /// <summary>The OS "Reduce Motion" accessibility setting (spec §06): loop movement renders at
    /// rest and stops requesting frames. Platform shells (W5) feed the real setting.</summary>
    public bool ReducedMotion { get; set; }

    /// <summary>
    /// W5 resize: adopts a new viewport WITHOUT recreating the host — component instances,
    /// transitions, scroll offsets and presence clocks all survive; the next frame lays out
    /// against the new size (size-class changes resolve naturally, S6).
    /// </summary>
    public void Resize(float width, float height)
    {
        if (width == Width && height == Height) return;
        Width = width;
        Height = height;
        NeedsRender = true;
    }

    /// <summary>W4: the platform text service (null = deterministic placeholder bars).</summary>
    public Framework.ITextRasterizer? TextRasterizer { get; set; }

    /// <summary>W4: the platform icon service (null = disc placeholders).</summary>
    public Framework.IIconRasterizer? IconRasterizer { get; set; }

    private readonly TextRasterCache _textCache = new();

    /// <summary>W4: the platform image service (null = SurfaceSubtle placeholder boxes).</summary>
    public Framework.IImageLoader? ImageLoader { get; set; }

    private readonly Dictionary<string, TextureData?> _imageCache = new();
    private readonly IconRasterCache _iconCache = new();

    /// <summary>Device pixels per dp (the shell's backingScaleFactor). Layout, input and hit
    /// regions stay in dp; the emitted commands are wrapped in one root scale so the GPU
    /// rasters at native resolution (retina).</summary>
    public float RenderScale { get; set; } = 1f;

    /// <summary>
    /// The margins the DISPLAY owns — notch, status bar, home indicator. A desktop window has none,
    /// so the default of zero is the correct answer there; a phone shell fills these from the
    /// platform and every <see cref="SafeArea"/> in the tree insets without the app knowing a number.
    /// </summary>
    public EdgeInsets SafeAreaInsets { get; set; }

    /// <summary>The NAVIGATION seam (write-once Link): a tap no pressable claims, landing on a link
    /// region, reports the href here — the platform shell maps it to a page (the native router's
    /// future home). Null = links are inert (visuals only).
    /// <para>Setting it also installs <see cref="Navigator"/>'s handler, so a component that
    /// navigates PROGRAMMATICALLY (a command palette's ↵) reaches the same shell. One surface owns
    /// the seam — the last host to take it wins, which is the desktop/mobile shape.</para></summary>
    public Action<string>? NavigationRequested
    {
        get => _navigationRequested;
        set
        {
            _navigationRequested = value;
            Navigator.Handler = value;
        }
    }

    private Action<string>? _navigationRequested;

    /// <summary>
    /// Builds one frame: clears to the theme background, then lays out and lowers the root via
    /// <see cref="PhotonRealizer"/>. <paramref name="timeMs"/> is the frame clock loop motion samples
    /// (injected by the platform shell — frames stay a pure function of it). Returns the realized
    /// frame (layout tree + hit regions).
    /// </summary>
    public RealizeResult RenderFrame(DisplayListBuilder builder, float timeMs = 0)
    {
        builder.Clear(_theme.Background.Resolve(Mode));
        if (RenderScale != 1f) builder.PushTransform(Engine.Matrix2D.Scale(RenderScale, RenderScale));
        _lastTimeMs = timeMs;
        _lastFrame = PhotonRealizer.Realize(_root, Width, Height, _theme, Mode, builder, _measurer, _typeScale, _pressed, _focused, _hovered, _instances, timeMs, ReducedMotion, _transitions, _scrolls, _presences, _drags, TextRasterizer, _textCache, RenderScale, IconRasterizer, _iconCache, ImageLoader, _imageCache, SafeAreaInsets);
        if (RenderScale != 1f) builder.Pop();
        NeedsRender = _lastFrame.HasActiveMotion;
        return _lastFrame;
    }

    /// <summary>
    /// Dispatches a tap against the LAST rendered frame: the topmost containing hit region (paint
    /// order — last registered wins, matching Stack semantics) receives the press; disabled regions
    /// swallow the tap without firing (they still exist for accessibility). Returns whether any
    /// region was hit.
    /// </summary>
    /// <summary>The positional reconciler: nested stateful components retain identity (and state)
    /// across parent rebuilds — see ComponentInstanceStore.</summary>
    private readonly ComponentInstanceStore _instances = new();

    private Pressable? _pressed;

    /// <summary>Where the pressed node was, so the release finds it in whatever frame is current by
    /// then. The object itself does not survive a Build; the path does.</summary>
    private string? _pressedPath;
    private VisualNode? _hovered;
    private readonly TransitionStore _transitions = new();
    private readonly ScrollStore _scrolls = new();
    private readonly PresenceStore _presences = new();
    private readonly DragStore _drags = new();

    /// <summary>The in-flight drag candidate: armed on press inside a drag region, ACTIVATED once
    /// the pointer travels past the slop (which cancels the pressable press), resolved on release.</summary>
    // The armed gesture surface. `Node` is a DragDismiss or a Draggable — the rules live on it, so
    // the host only tracks where the finger started and whether the slop has been passed.
    private (string Path, VisualNode Node, float StartX, float StartY, float Extent, bool Active)? _drag;

    /// <summary>The clock of the last rendered frame — glide-backs anchor to it on release.</summary>
    private float _lastTimeMs;
    private Pressable? _focused;

    /// <summary>The Pressable holding keyboard focus (the §01 double ring renders while set).</summary>
    public Pressable? Focused => _focused;

    /// <summary>
    /// Moves focus to the next ENABLED pressable in paint order (wrapping; spec: traversal = child
    /// order, depth-first — hit regions register in exactly that order). Returns false when the
    /// frame has no focusable region. (v1: forward-only; Shift+Tab reversal joins the key system.)
    /// </summary>
    public bool FocusNext()
    {
        var regions = _lastFrame?.HitRegions;
        if (regions is null || regions.Count == 0) return false;

        var start = 0;
        if (_focused is not null)
        {
            for (var i = 0; i < regions.Count; i++)
            {
                if (ReferenceEquals(regions[i].Node, _focused)) { start = i + 1; break; }
            }
        }
        for (var offset = 0; offset < regions.Count; offset++)
        {
            var region = regions[(start + offset) % regions.Count];
            if (region.Node.Disabled) continue;
            _focused = region.Node;
            NeedsRender = true;
            return true;
        }
        return false;
    }

    /// <summary>Clears keyboard focus (pointer interaction, escape).</summary>
    public void ClearFocus()
    {
        if (_focused is null) return;
        _focused = null;
        NeedsRender = true;
    }

    /// <summary>The Pressable currently held down (pressed visuals render while set).</summary>
    public Pressable? Pressed => _pressed;

    /// <summary>Spec S5: the node under the pointer — its Box applies its Hover diff on the next
    /// frame. Set by the platform shell's pointer tracking (the gesture slice owns the wiring).</summary>
    public VisualNode? Hovered => _hovered;

    /// <summary>Updates the hover target (pointer-over). Null clears it (pointer left / touch).</summary>
    public void SetHovered(VisualNode? node) => _hovered = node;

    /// <summary>
    /// Gestures v1 — pointer tracking: resolves the TOPMOST hover-reactive region under the pointer
    /// (paint order, last-contains wins) and re-renders when the target changes. Feed it from the
    /// platform shell's pointer-move events; it is a no-op until a frame has been rendered.
    /// </summary>
    public void PointerMove(float x, float y)
    {
        // Gestures v2: a pressed pointer travelling inside a drag surface becomes a DRAG once it
        // passes the slop — the pressable press cancels (spec §08's cancel rule) and the subtree
        // follows the pointer (downward only) until release.
        if (_drag is { } drag)
        {
            // The travel that COUNTS is the one along the gesture's own axis: a sideways swipe must
            // not arm on a vertical scroll, and a sheet must not arm on a sideways one.
            var horizontal = drag.Node is Draggable { Axis: DragAxis.Horizontal };
            var travel = horizontal ? x - drag.StartX : y - drag.StartY;
            var armed = drag.Node is Draggable
                ? Math.Abs(travel) > Touch.PressCancelSlop
                : travel > Touch.PressCancelSlop;

            if (!drag.Active && armed)
            {
                _drag = drag with { Active = true };
                _pressed = null;
                _pressedPath = null;
                drag = _drag.Value;
            }
            if (drag.Active)
            {
                // A DragDismiss only travels one way; a Draggable clamps to the caller's limits.
                if (drag.Node is Draggable draggable)
                {
                    var reported = Math.Clamp(Report(draggable, travel, drag.Extent),
                        draggable.Min, draggable.Max);
                    // The PAINT offset is always dp; only what the caller HEARS is normalised.
                    if (draggable.Follows)
                    {
                        _drags.Drag(drag.Path, draggable.Normalized
                            ? reported * drag.Extent
                            : reported);
                    }
                    draggable.OnMoved?.Invoke(reported);
                }
                else
                {
                    _drags.Drag(drag.Path, Math.Max(0, travel));
                }
                NeedsRender = true;
                return;
            }
        }

        var regions = _lastFrame?.HoverRegions;
        VisualNode? target = null;
        if (regions is not null)
        {
            for (var i = regions.Count - 1; i >= 0; i--)
            {
                if (regions[i].Bounds.Contains(new Point(x, y))) { target = regions[i].Node; break; }
            }
        }
        if (!ReferenceEquals(target, _hovered))
        {
            // S5 programmable hover: Hoverable regions get their transition callbacks — leave
            // BEFORE enter, the order every pointer model guarantees.
            if (_hovered is Hoverable left) left.OnChanged(false);
            _hovered = target;
            if (target is Hoverable entered) entered.OnChanged(true);
            NeedsRender = true;
        }
    }

    /// <summary>
    /// Scroll compositor v1 — wheel/drag input: routes <paramref name="delta"/> (dp toward the
    /// content end) to the TOPMOST scrollable viewport under the pointer, clamped to the frame's
    /// measured extent. Returns true (and marks the frame dirty) when the offset changed.
    /// </summary>
    public bool ScrollBy(float x, float y, float delta)
    {
        var regions = _lastFrame?.ScrollRegions;
        if (regions is null) return false;
        for (var i = regions.Count - 1; i >= 0; i--)
        {
            var region = regions[i];
            if (!region.Bounds.Contains(new Point(x, y))) continue;
            if (!_scrolls.ScrollBy(region.Path, delta, region.MaxOffset, region.Fallback)) return false;
            NeedsRender = true;
            return true;
        }
        return false;
    }

    /// <summary>The pointer left the window (or the input is touch) — hover clears.</summary>
    public void PointerLeave()
    {
        if (_hovered is null) return;
        if (_hovered is Hoverable left) left.OnChanged(false); // S5 programmable hover
        _hovered = null;
        NeedsRender = true;
    }

    /// <summary>
    /// Begins a press: the topmost enabled hit region under the point becomes the pressed node and
    /// the next frame renders its pressed token swap. Returns whether a region captured the press.
    /// (v1 fence: drag-slop/cancel and fling join the gesture system.)
    /// </summary>
    public bool PressDown(float x, float y)
    {
        if (_lastFrame is null) return false;
        var point = new Point(x, y);

        // Gestures v2: arm the topmost drag surface under the point as a CANDIDATE — it only
        // becomes a drag once the pointer travels past the slop (taps inside keep working).
        _drag = null;
        var dragRegions = _lastFrame.DragRegions;
        for (var i = dragRegions.Count - 1; i >= 0; i--)
        {
            if (!dragRegions[i].Bounds.Contains(point)) continue;
            // The surface's own extent along the axis — what a NORMALIZED gesture divides by.
            var horizontalSurface = dragRegions[i].Node is Draggable { Axis: DragAxis.Horizontal };
            var extent = horizontalSurface ? dragRegions[i].Bounds.Width : dragRegions[i].Bounds.Height;
            _drag = (dragRegions[i].Path, dragRegions[i].Node, x, y, extent, false);
            break;
        }

        var regions = _lastFrame.HitRegions;
        for (var i = regions.Count - 1; i >= 0; i--)
        {
            var region = regions[i];
            if (!region.Bounds.Contains(point)) continue;
            if (region.Node.Disabled) return true; // swallowed, no visual
            _pressed = region.Node;
            _pressedPath = region.Path;
            NeedsRender = true;
            return true;
        }
        return _drag is not null;
    }

    /// <summary>
    /// The system took the gesture away without ever lifting the finger — UIKit's
    /// <c>touchesCancelled</c>, a window losing key, a call arriving. Nothing was decided, so
    /// nothing is reported: the press drops and the surface glides back to where the caller last
    /// put it. The web controller answers <c>pointercancel</c> the same way.
    /// </summary>
    public void PointerCancel()
    {
        if (_drag is { Active: true } drag)
        {
            var rest = drag.Node switch
            {
                Draggable { Follows: false } => 0f,
                Draggable { Normalized: true } d => d.RestOffset * drag.Extent,
                Draggable d => d.RestOffset,
                _ => 0f,
            };
            _drags.Release(drag.Path, _lastTimeMs, rest);
        }

        _drag = null;
        _pressed = null;
        NeedsRender = true;
    }

    /// <summary>
    /// Where the gesture now IS, as the caller wants to hear it: measured from the rest it started
    /// at — a row already open reports where it ends up, not how far this one drag went — in dp, or
    /// as a fraction of the surface's own extent.
    /// </summary>
    private static float Report(Draggable draggable, float travel, float extent) =>
        draggable.RestOffset + (draggable.Normalized && extent > 0 ? travel / extent : travel);

    /// <summary>
    /// Ends a press: fires <c>OnPressed</c> when the release lands inside the pressed node's region
    /// (release outside cancels), clears the pressed visual either way.
    /// </summary>
    public bool PressUp(float x, float y)
    {
        // Gestures v2: an ACTIVE drag resolves here — past the threshold it dismisses (state then
        // removes the subtree and the presence EXIT completes from the dragged position); short of
        // it, the offset glides back over Motion.Base. Either way the press was already cancelled.
        if (_drag is { Active: true } drag)
        {
            _drag = null;

            // A Draggable REPORTS and lets the caller decide; the glide target is whatever RestOffset
            // the next build carries, so a row that stays open and one that springs back are the same
            // code with a different answer.
            if (drag.Node is Draggable draggable)
            {
                var horizontal = draggable.Axis == DragAxis.Horizontal;
                var raw = horizontal ? x - drag.StartX : y - drag.StartY;
                var travel = Math.Clamp(Report(draggable, raw, drag.Extent),
                    draggable.Min, draggable.Max);
                _drags.Release(drag.Path, _lastTimeMs,
                    draggable.Normalized ? draggable.RestOffset * drag.Extent : draggable.RestOffset);
                draggable.OnReleased?.Invoke(travel);
                NeedsRender = true;
                return true;
            }

            var dismiss = (DragDismiss)drag.Node;
            var dy = y - drag.StartY;
            if (dy >= DragDismiss.ThresholdDp)
            {
                _drags.Drop(drag.Path);
                dismiss.OnDismiss?.Invoke();
            }
            else
            {
                _drags.Release(drag.Path, _lastTimeMs);
            }
            NeedsRender = true;
            return true;
        }
        _drag = null;

        var pressed = _pressed;
        var pressedPath = _pressedPath;
        if (pressed is null) return ResolveLink(x, y);
        _pressed = null;
        _pressedPath = null;
        NeedsRender = true;

        var point = new Point(x, y);
        var regions = _lastFrame?.HitRegions;
        if (regions is null) return false;
        for (var i = regions.Count - 1; i >= 0; i--)
        {
            var region = regions[i];
            // The SAME pressable, found by where it sits. Matching on object identity looked right
            // and failed for every press that outlived its frame — which is every real one, because
            // showing the pressed state repaints and the next Build hands back new nodes.
            if (!IsTheSamePressable(region, pressed, pressedPath)) continue;
            if (!region.Bounds.Contains(point)) return false; // canceled by releasing outside
            region.Node.OnPressed?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>Whether a region is the one the press began on: the same object while the frame
    /// survives, and otherwise the same PLACE in the tree.</summary>
    private static bool IsTheSamePressable(HitRegion region, Pressable pressed, string? pressedPath) =>
        ReferenceEquals(region.Node, pressed)
        || (!string.IsNullOrEmpty(pressedPath) && region.Path == pressedPath);

    /// <summary>A tap that no pressable claimed: the TOPMOST link region under the point navigates
    /// through the host's seam (a Pressable INSIDE a link wins — checked before this).</summary>
    private bool ResolveLink(float x, float y)
    {
        var regions = _lastFrame?.LinkRegions;
        if (regions is null || _navigationRequested is null) return false;
        var point = new Point(x, y);
        for (var i = regions.Count - 1; i >= 0; i--)
        {
            if (!regions[i].Bounds.Contains(point)) continue;
            _navigationRequested(regions[i].Node.Href);
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

    /// <summary>
    /// Spec S8 — a key press from the shell (desktop windows have a real keyboard): fires the LAST
    /// matching <see cref="Shortcut"/> of the current frame (the dialog on top wins the chord) and
    /// returns whether it was handled, so the shell can stop propagating it. Being on screen IS the
    /// subscription — an unmounted dialog's Esc simply is not in the frame any more.
    /// <para>
    /// <paramref name="modifiers"/> carries <see cref="KeyModifiers.Command"/> for the PLATFORM's
    /// command key (⌘ on macOS, Ctrl elsewhere) — the shell resolves that, exactly like the browser
    /// twin, so one authored chord is right everywhere.
    /// </para>
    /// </summary>
    public bool KeyDown(string key, KeyModifiers modifiers = KeyModifiers.None)
    {
        var bindings = _lastFrame?.Shortcuts;
        if (bindings is null) return false;
        for (var i = bindings.Count - 1; i >= 0; i--)
        {
            var chord = bindings[i].Chord;
            if (chord.Modifiers != modifiers) continue;
            if (!string.Equals(chord.Key, key, StringComparison.OrdinalIgnoreCase)) continue;
            bindings[i].OnPressed();
            NeedsRender = true;
            return true;
        }
        return false;
    }
}
