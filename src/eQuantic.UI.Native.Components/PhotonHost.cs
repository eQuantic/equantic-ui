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

        // Hot reload reaches a running app through a static seam — every live host signs up, so an
        // applied edit wakes this one too. Weakly: a host is not kept alive by having existed.
        PhotonHotReload.Register(this);
        _hotReloadGeneration = PhotonHotReload.Generation;
    }

    /// <summary>The last hot-reload generation THIS host has adopted. Compared on the render
    /// thread, which is the only place the caches may be touched.</summary>
    private int _hotReloadGeneration;

    public ThemeMode Mode { get; set; }

    /// <summary>
    /// How tight this host wants its controls. A window driven by a MOUSE is Compact — the
    /// toolbars, sidebars and status rails of a desktop app; a touch screen stays Comfortable.
    /// The shell decides, and no page ever asks.
    /// </summary>
    public Density Density { get; set; } = Density.Comfortable;
    public float Width { get; private set; }
    public float Height { get; private set; }

    /// <summary>True when state changed since the last <see cref="RenderFrame"/> (starts true), or
    /// when the last frame carried running loop motion — animated frames keep the loop hot.</summary>
    public bool NeedsRender { get; private set; } = true;

    /// <summary>When the next frame is due even though nothing is dirty — the caret's next blink
    /// transition, in the same clock <see cref="RenderFrame"/> is fed. Null = nothing scheduled.</summary>
    private float? _nextFrameDueMs;

    /// <summary>
    /// Whether a SCHEDULED frame has come due — the blink's half-period elapsed. The shells' idle
    /// ticks (the macOS 120Hz timer, the phones' vsync callbacks) ask this alongside
    /// <see cref="NeedsRender"/>: a caret still blinks, at two presents a second instead of the
    /// display's whole refresh rate.
    /// </summary>
    public bool IsFrameDue(float nowMs) => _nextFrameDueMs is { } due && nowMs >= due;

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

    /// <summary>The platform clipboard (null = the copy keys do nothing, which is the honest
    /// answer on a host that has none).</summary>
    public ITextClipboard? Clipboard { get; set; }

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
        // An edit landed since the last frame: drop the content caches BEFORE realizing, on this
        // thread, where they are owned. Most edits would miss them anyway (the keys are content),
        // but a patched rasterizer or an edited icon path re-rasters fresh — once.
        if (_hotReloadGeneration != PhotonHotReload.Generation)
        {
            _hotReloadGeneration = PhotonHotReload.Generation;
            _textCache.Clear();
            _iconCache.Clear();
            _imageCache.Clear();
        }

        builder.Clear(_theme.Background.Resolve(Mode));
        if (RenderScale != 1f) builder.PushTransform(Engine.Matrix2D.Scale(RenderScale, RenderScale));
        _lastTimeMs = timeMs;
        // A gliding scroll advances BEFORE the frame is realized, so this frame paints where it
        // moved to; while anything is still gliding the host keeps asking for frames.
        // Resolved HERE, not when it was set: ReducedMotion can arrive from the system at any
        // point, and it always wins.
        _scrolls.Smooth = SmoothScroll && !ReducedMotion;
        var gliding = _scrolls.Advance(timeMs);
        _lastFrame = PhotonRealizer.Realize(_root, Width, Height, _theme, Mode, builder, _measurer, _typeScale, _pressed, _focusVisible ? _focused : null, _hovered, _instances, timeMs, ReducedMotion, _transitions, _scrolls, _presences, _drags, TextRasterizer, _textCache, RenderScale, IconRasterizer, _iconCache, ImageLoader, _imageCache, SafeAreaInsets, _pressedPath,
            _focusVisible ? _focusedPath : null, _textPath, CaretIndex, CaretVisible,
            Selection.Start, Selection.End, density: Density,
            scrollOffset: path => _scrolls.Get(path), markedText: _marked, pathCache: _pathCache);
        if (RenderScale != 1f) builder.Pop();
        AdoptAutofocus();
        NeedsRender = _lastFrame.HasActiveMotion || gliding;

        // The caret is 2Hz motion, not vsync motion. Holding NeedsRender for it pinned the whole
        // loop to the display's refresh — 120 presents a second on a ProMotion panel, measured, to
        // flip a rectangle twice a second. So an editing caret SCHEDULES instead: the next frame is
        // due at the next blink transition, and the shells' idle ticks ask IsFrameDue alongside
        // NeedsRender. While a drag draws a selection the caret does not blink at all (it is forced
        // solid), and the pointer is what drives frames — nothing to schedule.
        _nextFrameDueMs = _textPath is not null && !NeedsRender && !_dragSelecting && !_codeDragging
            ? _caretPhaseMs + (MathF.Floor((_lastTimeMs - _caretPhaseMs) / CaretBlinkMs) + 1) * CaretBlinkMs
            : null;
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

    /// <summary>An inert control consumed the press — the release must not fall through to links.</summary>
    private bool _pressSwallowed;

    /// <summary>Where the pressed node was, so the release finds it in whatever frame is current by
    /// then. The object itself does not survive a Build; the path does.</summary>
    private string? _pressedPath;
    private VisualNode? _hovered;
    private readonly TransitionStore _transitions = new();
    private readonly ScrollStore _scrolls = new();

    /// <summary>
    /// A finger on a scrollable surface. Armed on press and only ACTIVE once it has travelled past
    /// the slop along the view's own axis — so a tap inside a list is still a tap, and a sideways
    /// swipe never scrolls a vertical list.
    /// </summary>
    private (string Path, ScrollAxis Axis, float MaxOffset, float Start, float FromOffset,
        float LastAt, float LastTimeMs, float Velocity, bool Active)? _pan;

    /// <summary>
    /// Whether a wheel or drag GLIDES to where it was sent instead of jumping there. On by default
    /// — a jump reads as a redraw rather than as movement, and the eye loses its place. Reduce
    /// Motion turns it off whatever the app said: it is movement, and that setting means it.
    /// </summary>
    public bool SmoothScroll { get; set; } = true;
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
    private string? _focusedPath;

    /// <summary>
    /// Whether the focus should be SEEN. Focus and the ring around it are not the same thing: a
    /// mouse user who clicks a button knows perfectly well what they clicked, and a ring left behind
    /// on every click reads as a rendering bug. A keyboard user has nothing else to go on. So the
    /// ring follows the keyboard, exactly as `:focus-visible` does on web — same rule, same reason.
    /// </summary>
    private bool _focusVisible;

    /// <summary>The Pressable holding keyboard focus (the §01 double ring renders while set).</summary>
    public Pressable? Focused => _focused;

    /// <summary>
    /// Moves focus to the next ENABLED pressable in paint order (wrapping; spec: traversal = child
    /// order, depth-first — hit regions register in exactly that order). Returns false when the
    /// frame has no focusable region.
    /// </summary>
    public bool FocusNext() => MoveFocus(1);

    /// <summary>The same walk backwards — Shift+Tab. Going back matters more than it sounds: it is
    /// how someone who overshot a field returns to it without cycling through the whole form.</summary>
    public bool FocusPrevious() => MoveFocus(-1);

    private bool MoveFocus(int step)
    {
        var stops = _lastFrame?.FocusStops;
        if (stops is null || stops.Count == 0) return false;

        // Where the focus is NOW, found by path: the tree has been rebuilt on every keystroke since
        // it was set, so the node it once pointed at is long gone. Reference identity here meant Tab
        // silently restarting from the first control every time the app called SetState.
        var here = _textPath ?? _focusedPath;
        var current = -1;
        if (here is { Length: > 0 })
        {
            for (var i = 0; i < stops.Count; i++)
            {
                if (stops[i].Path != here) continue;
                current = i;
                break;
            }
        }

        var start = current < 0 ? (step > 0 ? 0 : stops.Count - 1) : current + step;
        var index = ((start % stops.Count) + stops.Count) % stops.Count;
        var stop = stops[index];

        // Bring it into view BEFORE it takes focus: a caret blinking somewhere off screen is the
        // same as no caret at all.
        ScrollIntoView(stop);

        if (stop.Entry is not null)
        {
            // Landing on a field starts editing it — Tab into a field and type is the whole point.
            _focused = null;
            _focusedPath = null;
            BeginEditing(stop.Entry, stop.Path);
            return true;
        }

        EndEditing();
        if (stop.Adjustable is not null)
        {
            // A slider under focus: the ring shows, Enter does nothing, and the ARROWS are the
            // whole point — dispatched from KeyDown against the current frame's stop.
            _focused = null;
            _focusedPath = stop.Path;
            _focusVisible = true;
            NeedsRender = true;
            return true;
        }
        _focused = stop.Pressable;
        _focusedPath = stop.Path;
        _focusVisible = true;   // arrived by Tab: this is exactly who the ring is for
        NeedsRender = true;
        return true;
    }

    /// <summary>
    /// Scrolls whatever contains this stop until the stop is inside it.
    /// <para>
    /// The container is found by PATH: a scroll region whose path is a prefix of the stop's is an
    /// ancestor of it, and the longest such prefix is the innermost one. The tree already says who
    /// contains whom — asking it is cheaper and truer than keeping a second map that can disagree.
    /// </para>
    /// </summary>
    private void ScrollIntoView(FocusStop stop)
    {
        if (_lastFrame is null || stop.Bounds.Height <= 0) return;

        var regions = _lastFrame.ScrollRegions;
        var best = -1;
        for (var i = 0; i < regions.Count; i++)
        {
            if (regions[i].MaxOffset <= 0) continue;
            if (!IsAncestorPath(regions[i].Path, stop.Path)) continue;
            if (best < 0 || regions[i].Path.Length > regions[best].Path.Length) best = i;
        }
        if (best < 0) return;

        var viewport = regions[best].Bounds;
        var horizontal = regions[best].Axis == ScrollAxis.Horizontal;
        var (start, end, viewStart, viewEnd) = horizontal
            ? (stop.Bounds.X, stop.Bounds.X + stop.Bounds.Width, viewport.X, viewport.X + viewport.Width)
            : (stop.Bounds.Y, stop.Bounds.Y + stop.Bounds.Height, viewport.Y, viewport.Y + viewport.Height);

        // Scrolled to just inside the near edge, with a control's worth of margin, rather than to
        // the exact edge: a field flush against the top of a viewport looks like the first one, and
        // there is no way to tell there is more above it.
        var margin = MathF.Min(ScrollIntoViewMargin, (viewEnd - viewStart) / 4);
        var delta = 0f;
        if (start < viewStart + margin) delta = start - viewStart - margin;
        else if (end > viewEnd - margin) delta = end - viewEnd + margin;
        if (delta == 0) return;

        _scrolls.ScrollBy(regions[best].Path, delta, regions[best].MaxOffset, regions[best].Fallback);
        NeedsRender = true;
    }

    private const float ScrollIntoViewMargin = 24f;

    /// <summary>Whether <paramref name="ancestor"/> names a node this path sits under. Compared on
    /// SEGMENT boundaries: "r/1" must not be read as an ancestor of "r/10".</summary>
    private static bool IsAncestorPath(string ancestor, string path) =>
        path.Length > ancestor.Length
        && path.StartsWith(ancestor, StringComparison.Ordinal)
        && path[ancestor.Length] == '/';

    private bool IsFocused(HitRegion region) =>
        _focusedPath is { Length: > 0 }
            ? region.Path == _focusedPath
            : _focused is not null && ReferenceEquals(region.Node, _focused);

    /// <summary>Gives focus to a specific region — what a pointer press does, so that releasing over
    /// a control and then pressing Enter goes on working on the control the user just touched.</summary>
    public void Focus(Pressable node, string? path = null, bool visible = true)
    {
        _focused = node;
        _focusedPath = path;
        _focusVisible = visible;
        NeedsRender = true;
    }

    /// <summary>True when the focus arrived by keyboard, and the ring is therefore drawn.</summary>
    public bool FocusVisible => _focusVisible;

    // ── Text editing ──────────────────────────────────────────────────────────────────────────
    //
    // The app owns the string: it hands the field a Value and gets an OnChanged back, exactly as on
    // web. What lives HERE is the part the app does not have — which field is being edited and where
    // the caret sits — because both must survive the rebuild that each keystroke causes.

    private string? _textPath;

    /// <summary>Path strings survive frames here — a path is identity, and the tree's shape barely
    /// changes, so steady state re-uses the same strings instead of re-concatenating them.</summary>
    private readonly Dictionary<(string Parent, int Index), string> _pathCache = new();
    private int _caret;

    /// <summary>
    /// Where the caret is, as something you ASSIGN — because every assignment restarts the blink.
    /// <para>
    /// With a free-running phase, a caret that just moved is invisible for up to half a second: press
    /// ⇧→ and the one thing you are looking for — which end of the range you are holding — may not be
    /// on screen when you look. Every desktop text stack restarts the phase on activity, and this is
    /// the single place all activity passes through.
    /// </para>
    /// </summary>
    private int Caret
    {
        get => _caret;
        set
        {
            _caret = value;
            _caretPhaseMs = _lastTimeMs;
        }
    }

    /// <summary>When the current blink cycle started — the last time the caret moved.</summary>
    private float _caretPhaseMs;

    /// <summary>
    /// Where a selection STARTED, or null when there is none. The caret is the moving end, this is
    /// the fixed one — which is what makes shift-arrow extend in both directions and a drag reverse
    /// on itself without the range flipping inside out.
    /// </summary>
    private int? _anchor;

    /// <summary>A press that began inside a field: the pointer is drawing a selection until it lifts.</summary>
    private bool _dragSelecting;

    /// <summary>The selected range, low end first. Empty when nothing is selected.</summary>
    public (int Start, int End) Selection
    {
        get
        {
            if (_anchor is not { } anchor) return (CaretIndex, CaretIndex);
            var caret = CaretIndex;
            var length = TextTarget?.Value.Length ?? 0;
            anchor = Math.Clamp(anchor, 0, length);
            return anchor <= caret ? (anchor, caret) : (caret, anchor);
        }
    }

    public bool HasSelection => Selection.Start != Selection.End;

    /// <summary>
    /// The caret position a click at <paramref name="localX"/> dp into the field lands on — the
    /// character boundary NEAREST the point, so clicking the right half of a letter puts the caret
    /// after it, which is what every text field does and what makes clicking feel aimed rather
    /// than approximate.
    /// </summary>
    private int IndexAt(TextEntry entry, float localX)
    {
        var value = entry.Value;
        if (value.Length == 0 || localX <= 0) return 0;
        var measurer = _measurer ?? ApproximateTextMeasurer.Instance;
        var style = _theme.Type(entry.Role);

        float Width(int count) => count <= 0 ? 0
            : measurer.Measure(Shown(entry, value[..count]), style, _typeScale, float.PositiveInfinity, 1)
                .Lines is { Count: > 0 } lines ? lines[0].Width : 0;

        if (localX >= Width(value.Length)) return value.Length;

        // Binary search over prefix widths: proportional glyphs make "iii" and "WWW" different
        // widths at equal length, so a ratio of the total would land in the wrong place.
        var low = 0;
        var high = value.Length;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (Width(mid + 1) <= localX) low = mid + 1;
            else high = mid;
        }
        // Nearest boundary rather than the one before: half a glyph either way.
        var before = Width(low);
        var after = Width(Math.Min(low + 1, value.Length));
        return localX - before > after - localX && low < value.Length ? low + 1 : low;
    }

    /// <summary>Selects the word the caret is in — both edges found by the same rule, so a click
    /// between two words picks the one the nearest boundary belongs to.</summary>
    private void SelectWordAt(TextEntry entry)
    {
        var value = entry.Value;
        if (value.Length == 0) return;
        var at = Math.Clamp(CaretIndex, 0, value.Length);
        // On a boundary, prefer the word BEHIND the caret — the one the click was visually inside.
        if (at > 0 && (at == value.Length || !IsWord(value[at]))) at--;
        if (!IsWord(value[at])) { _anchor = at; Caret = at + 1; return; }   // a run of spaces
        var start = at;
        while (start > 0 && IsWord(value[start - 1])) start--;
        var end = at;
        while (end < value.Length && IsWord(value[end])) end++;
        _anchor = start;
        Caret = end;
    }

    private static bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>
    /// What ⌘C puts on the clipboard. A password field copies NOTHING — the dots are what is drawn,
    /// and the secret behind them is not the field's to hand out.
    /// </summary>
    private static string Copyable(TextEntry entry, int start, int end) =>
        entry.Obscure ? "" : entry.Value[start..end];

    /// <summary>What is actually DRAWN for a value — a password shows dots, and a click has to land
    /// where the dots are.</summary>
    private static string Shown(TextEntry entry, string value) =>
        entry.Obscure ? new string('•', value.Length) : value;

    /// <summary>The field being edited in the CURRENT frame, or null. Resolved by path every time
    /// rather than remembered: the node handed out last frame is already stale.</summary>
    public TextEntry? TextTarget
    {
        get
        {
            if (_textPath is null || _lastFrame is null) return null;
            var fields = _lastFrame.TextRegions;
            for (var i = 0; i < fields.Count; i++)
                if (fields[i].Path == _textPath) return fields[i].Entry;
            return null;
        }
    }

    /// <summary>
    /// The CODE surface being edited in the current frame, or null. Resolved by path every frame for
    /// the same reason the text field is: the node handed out last frame is already a corpse, but
    /// the CONTROLLER it points at is the component's own and outlives every rebuild.
    /// </summary>
    public CodeSurface? CodeTarget
    {
        get
        {
            if (_textPath is null || _lastFrame is null) return null;
            var regions = _lastFrame.CodeRegions;
            for (var i = 0; i < regions.Count; i++)
                if (regions[i].Path == _textPath) return regions[i].Surface;
            return null;
        }
    }

    /// <summary>A press that began in a code surface is drawing a selection until it lifts.</summary>
    private bool _codeDragging;

    /// <summary>The SPREADSHEET surface being edited, resolved by path every frame like the code
    /// surface: the node is a corpse, the CONTROLLER outlives every rebuild.</summary>
    public SheetSurface? SheetTarget
    {
        get
        {
            if (_textPath is null || _lastFrame is null) return null;
            var regions = _lastFrame.SheetRegions;
            for (var i = 0; i < regions.Count; i++)
                if (regions[i].Path == _textPath) return regions[i].Surface;
            return null;
        }
    }

    /// <summary>A press that began in a sheet is dragging a range until it lifts.</summary>
    private bool _sheetDragging;

    /// <summary>A press that grabbed the fill handle is dragging a fill preview until it lifts.</summary>
    private bool _sheetFilling;

    /// <summary>The bottom-right corner of the selection's bottom-right CELL, in host space —
    /// where Excel's little square sits. Prefix-sum arithmetic, the inverse of CellAt.</summary>
    private static Point FillHandleCorner(SheetRegion region, SheetRange selection)
    {
        var surface = region.Surface;
        var document = surface.Controller.Document;
        var x = region.Bounds.X + surface.HeaderWidth;
        for (var c = surface.FirstCol; c <= selection.RightCol; c++) x += document.ColWidth(c);
        var y = region.Bounds.Y + surface.HeaderHeight;
        for (var r = surface.FirstRow; r <= selection.BottomRow; r++) y += document.RowHeight(r);
        return new Point(x, y);
    }

    private const float FillHandleGrab = 8f;

    /// <summary>The cell a point lands on: prefix-sum arithmetic from the window's origin,
    /// clamped into the grid so a drag past the edge holds the edge.</summary>
    private static CellRef CellAt(SheetRegion region, float x, float y)
    {
        var surface = region.Surface;
        var document = surface.Controller.Document;
        var gridX = x - region.Bounds.X - surface.HeaderWidth;
        var gridY = y - region.Bounds.Y - surface.HeaderHeight;

        var col = surface.FirstCol;
        var acc = 0f;
        while (col < document.Cols - 1 && acc + document.ColWidth(col) <= gridX)
        {
            acc += document.ColWidth(col);
            col++;
        }
        var row = surface.FirstRow;
        acc = 0f;
        while (row < document.Rows - 1 && acc + document.RowHeight(row) <= gridY)
        {
            acc += document.RowHeight(row);
            row++;
        }
        return document.Clamp(new CellRef(row, col));
    }

    /// <summary>
    /// The (line, column) a point lands on. With a fixed pitch this is division, not a search — and
    /// the column ROUNDS to the nearest boundary, so clicking the right half of a character puts the
    /// caret after it, which is what makes clicking feel aimed rather than approximate.
    /// </summary>
    private static CodePosition PositionIn(CodeRegion region, float x, float y)
    {
        var surface = region.Surface;
        var line = (int)MathF.Floor((y - region.Bounds.Y - surface.ContentTop) / surface.LineHeight);
        var column = (int)MathF.Round((x - region.Bounds.X - surface.ContentLeft) / surface.ColumnWidth);
        return surface.Editor.Document.Clamp(new CodePosition(Math.Max(0, line), Math.Max(0, column)));
    }

    private void BeginCodeEditing(CodeRegion region)
    {
        var changed = _textPath != region.Path;
        if (changed) EndEditing();
        _textPath = region.Path;
        _focused = null;
        _focusedPath = null;
        NeedsRender = true;
    }

    /// <summary>Restarts the blink — anything that moved the caret, including a code command.</summary>
    private void RestartBlink() => _caretPhaseMs = _lastTimeMs;

    /// <summary>Where the caret sits, clamped to the text that is actually there — the app may have
    /// replaced the value with something shorter (a formatter, a reset) between keystrokes.</summary>
    public int CaretIndex => Math.Clamp(_caret, 0, TextTarget?.Value.Length ?? 0);

    /// <summary>
    /// Half-second on, half-second off, off the frame clock — the rate every desktop uses. A caret
    /// that does not blink reads as a rendering artifact rather than a place to type.
    /// <para>
    /// The phase is measured from the last caret MOVE, not from the epoch, so a caret that just
    /// arrived somewhere is solid the instant it gets there. While a drag is drawing a selection it
    /// does not blink at all: the end following your pointer is the one thing you are watching.
    /// </para>
    /// </summary>
    public bool CaretVisible => _textPath is null || _dragSelecting || _codeDragging
        || (int)((_lastTimeMs - _caretPhaseMs) / CaretBlinkMs) % 2 == 0;

    private const float CaretBlinkMs = 500f;

    /// <summary>
    /// A field that asked for the caret gets it — the search box in a palette that just opened, the
    /// first field of a form. The web realization of the same tree has honoured `Autofocus` all
    /// along; native ignored it, so the ⌘K panel opened ready to type in a browser and dead in a
    /// window.
    /// <para>
    /// Honoured ONCE per field. Without remembering, leaving the field with Escape would hand it
    /// straight back on the very next frame, and the field could never be left at all.
    /// </para>
    /// </summary>
    private void AdoptAutofocus()
    {
        if (_lastFrame is null) return;
        var fields = _lastFrame.TextRegions;
        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            if (!field.Entry.Autofocus || field.Entry.Disabled) continue;
            if (!_autofocused.Add(field.Path)) continue;
            if (_textPath is not null) continue;   // the user is already typing somewhere: leave them alone
            BeginEditing(field);
            return;
        }
    }

    private readonly HashSet<string> _autofocused = [];

    private void BeginEditing(TextRegion field, float? atX = null) =>
        BeginEditing(field.Entry, field.Path, atX is { } x ? IndexAt(field.Entry, x - field.Bounds.X) : null);

    private void BeginEditing(TextEntry entry, string path, int? caret = null)
    {
        var field = new TextRegion(default, entry, path);
        if (field.Entry.Disabled) return;
        var changed = _textPath != field.Path;
        // Tell the field being LEFT that it lost focus before telling the next one it gained it.
        // Skipping this left every field the user had passed through still wearing its focus ring —
        // a form where four boxes all look like the active one.
        if (changed) EndEditing();
        _textPath = field.Path;
        // Where the pointer fell, or the end of the text when it arrived by Tab.
        Caret = caret ?? field.Entry.Value.Length;
        _anchor = caret is null ? null : _caret;
        _focused = null;
        _focusedPath = null;
        if (changed) field.Entry.OnFocusChanged?.Invoke(true);
        NeedsRender = true;
    }

    private void EndEditing()
    {
        if (_textPath is null) return;
        TextTarget?.OnFocusChanged?.Invoke(false);
        _textPath = null;
        _marked = "";
        Caret = 0;
        _anchor = null;
        NeedsRender = true;
    }

    /// <summary>
    /// Text the platform decided the user typed — one character, a pasted paragraph, or the result
    /// of a dead key or an input method. It arrives as a STRING for that reason: what a keystroke
    /// produces is the platform's business, and "á" may be one key or three.
    /// </summary>
    public bool TextInput(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        // A focused SHEET types Excel's way: the first character REPLACES the cell (a fresh draft
        // seeded with it); the rest append. The document only changes on commit.
        if (SheetTarget is { } sheetTarget)
        {
            var sheet = sheetTarget.Controller;
            SheetKeymap.Type(sheet, text);
            sheetTarget.OnChanged?.Invoke();
            NeedsRender = true;
            return true;
        }
        // A code surface types through its controller, which is where auto-closing pairs, the
        // "step over the closer" rule and undo coalescing all live.
        if (CodeTarget is { } surface)
        {
            var typed = false;
            foreach (var c in text) typed |= surface.Editor.Type(c);
            if (!typed) return false;
            RestartBlink();
            surface.OnChanged?.Invoke();
            NeedsRender = true;
            return true;
        }
        if (TextTarget is not { } entry || entry.Disabled) return false;
        // Typing over a selection REPLACES it — the one behaviour that makes selecting worth doing.
        var (start, end) = Selection;
        var value = entry.Value;
        Commit(entry, value[..start] + text + value[end..], start + text.Length, clearAnchor: true);
        return true;
    }

    // ---- IME composition ------------------------------------------------------------------------
    //
    // A dead key, an accent, a CJK candidate: the platform composes TEXT over several keystrokes,
    // and until it commits, the field shows the composition INLINE (underlined, caret at its end)
    // without the value changing. The value only ever changes through CommitText — cancelling a
    // composition leaves the field exactly as it was.

    private string _marked = "";

    /// <summary>Whether a field or code surface holds the caret — the shell's cue to hand key
    /// events to the platform's input context instead of interpreting them itself.</summary>
    public bool HasTextFocus => _textPath is not null;

    /// <summary>The last realized frame — the shells' read-only window onto regions and semantics
    /// (probes, bridges). Null before the first render.</summary>
    public RealizeResult? LastFrame => _lastFrame;

    /// <summary>The composition in flight for the focused field ("" when none).</summary>
    public string MarkedText => _textPath is null ? "" : _marked;

    public bool HasMarkedText => MarkedText.Length > 0;

    /// <summary>
    /// Replaces the composition in flight ("" cancels it). Composing over a selection replaces the
    /// selection first — the same rule typing has — and that edit is real: cancelling the
    /// composition afterwards does not resurrect what typing over it would also have destroyed.
    /// </summary>
    public bool SetMarkedText(string text)
    {
        if (_textPath is null) return false;
        // A code surface has no composition VISUAL yet (its face lives in its child's render) —
        // the state still tracks, so commit lands and cancel is clean.
        if (TextTarget is { } entry && !entry.Disabled)
        {
            var (start, end) = Selection;
            if (end > start)
                Commit(entry, entry.Value[..start] + entry.Value[end..], start, clearAnchor: true);
        }
        if (_marked == text) return true;
        _marked = text;
        RestartBlink();
        NeedsRender = true;
        return true;
    }

    /// <summary>The platform finished composing: the text enters the field at the caret, through
    /// the same door every keystroke uses, and the composition clears.</summary>
    public bool CommitText(string text)
    {
        if (_marked.Length > 0)
        {
            _marked = "";
            NeedsRender = true;
        }
        return TextInput(text);
    }

    /// <summary>The selected range of the focused field, as (start, length) — what
    /// NSTextInputClient's <c>selectedRange</c> answers.</summary>
    public (int Start, int Length) SelectionRange
    {
        get
        {
            var (start, end) = Selection;
            return (start, end - start);
        }
    }

    /// <summary>The composition's range in the field, or (-1, 0) when nothing is marked.</summary>
    public (int Start, int Length) MarkedRange =>
        HasMarkedText ? (CaretIndex, _marked.Length) : (-1, 0);

    /// <summary>
    /// Where the caret sits, in WINDOW coordinates — the anchor for the platform's candidate
    /// window (the CJK picker appears under the text being composed, not in a corner). Null when
    /// nothing is focused or the frame has not rendered.
    /// </summary>
    public Rect? CaretRect()
    {
        if (_lastFrame is null || _textPath is null) return null;

        var fields = _lastFrame.TextRegions;
        for (var i = 0; i < fields.Count; i++)
        {
            if (fields[i].Path != _textPath) continue;
            var bounds = fields[i].Bounds;
            var entry = fields[i].Entry;
            var style = _theme.Type(entry.Role);
            var caretInValue = Math.Min(CaretIndex, entry.Value.Length);
            var composed = _marked.Length > 0 ? entry.Value.Insert(caretInValue, _marked) : entry.Value;
            var upTo = Math.Min(caretInValue + _marked.Length, composed.Length);
            var advance = 0f;
            if (upTo > 0 && TextRasterizer is { } rasterizer)
            {
                var raster = (_textCache ?? TextRasterCache.Shared).Get(
                    rasterizer, composed[..upTo], style, _typeScale, float.MaxValue, 1, RenderScale);
                if (raster is not null) advance = raster.Texture.Width / RenderScale;
            }
            // The same window-follows-caret clamp the realizer applies while editing.
            advance = MathF.Min(advance, bounds.Width - 8f);
            return new Rect(bounds.X + MathF.Max(advance, 0), bounds.Y, 2f, style.LineHeight);
        }

        var surfaces = _lastFrame.CodeRegions;
        for (var i = 0; i < surfaces.Count; i++)
        {
            if (surfaces[i].Path != _textPath) continue;
            var surface = surfaces[i].Surface;
            var caret = surface.Editor.Selection.Focus;
            return new Rect(
                surfaces[i].Bounds.X + surface.ContentLeft + caret.Column * surface.ColumnWidth,
                surfaces[i].Bounds.Y + surface.ContentTop + caret.Line * surface.LineHeight,
                2f, surface.LineHeight);
        }
        return null;
    }

    /// <summary>The editing keys — what Backspace and the arrows mean inside a field. Returns false
    /// for anything it does not claim, so Tab and Escape go on meaning what they mean everywhere.</summary>
    private bool EditKey(TextEntry entry, string key, KeyModifiers modifiers)
    {
        if (entry.Disabled) return false;
        var value = entry.Value;
        var caret = CaretIndex;
        var (start, end) = Selection;
        var selecting = modifiers.HasFlag(KeyModifiers.Shift);
        var command = modifiers.HasFlag(KeyModifiers.Command);

        // Select all, then the movements. A movement WITH shift extends from the anchor; without
        // it, an existing selection collapses to the edge you moved towards rather than to the
        // caret — press Right with three words selected and you land after them, not in the middle.
        if (command && key.Equals("a", StringComparison.OrdinalIgnoreCase))
        {
            _anchor = 0;
            Caret = value.Length;
            NeedsRender = true;
            return true;
        }

        // Copy, cut, paste. Claimed even with no clipboard behind them: letting ⌘V fall through
        // would type a "v" into the field, which is worse than nothing happening.
        if (command && key.Length == 1)
        {
            switch (char.ToLowerInvariant(key[0]))
            {
                case 'c':
                    if (start != end && Copyable(entry, start, end) is { Length: > 0 } copied)
                        Clipboard?.Write(copied);
                    return true;

                case 'x':
                    if (start == end) return true;
                    // An obscured field cuts by DELETING only: writing its dots' secret out is not
                    // its call, and writing "" would clear what the user already had there.
                    if (Copyable(entry, start, end) is { Length: > 0 } cut) Clipboard?.Write(cut);
                    Commit(entry, value.Remove(start, end - start), start, clearAnchor: true);
                    return true;

                case 'v':
                    if (Clipboard?.Read() is not { Length: > 0 } pasted) return true;
                    // A pasted newline would end the line rather than extend it: this is a
                    // single-line field, and the paste is flattened to match what it can show.
                    TextInput(pasted.ReplaceLineEndings(" "));
                    return true;
            }
        }

        switch (key)
        {
            case "Backspace" when start != end:
            case "Delete" when start != end:
                Commit(entry, value.Remove(start, end - start), start, clearAnchor: true);
                return true;

            case "Backspace" when caret > 0:
                Commit(entry, value.Remove(caret - 1, 1), caret - 1, clearAnchor: true);
                return true;
            case "Backspace":
                return true; // claimed at the start of the field: it must not fall through to Back

            case "Delete" when caret < value.Length:
                Commit(entry, value.Remove(caret, 1), caret, clearAnchor: true);
                return true;
            case "Delete":
                return true;

            case "ArrowLeft":
                MoveCaret(selecting ? Math.Max(0, caret - 1) : start != end ? start : Math.Max(0, caret - 1), selecting);
                return true;
            case "ArrowRight":
                MoveCaret(selecting ? Math.Min(value.Length, caret + 1) : start != end ? end : Math.Min(value.Length, caret + 1), selecting);
                return true;

            case "Home" or "ArrowUp":
                MoveCaret(0, selecting);
                return true;
            case "End" or "ArrowDown":
                MoveCaret(value.Length, selecting);
                return true;

            case "Enter":
                // Submit, then leave: a form that stays in the field after Enter makes the user
                // wonder whether anything happened.
                entry.OnSubmit?.Invoke();
                EndEditing();
                return true;

            case "Escape":
                EndEditing();
                return true;

            // Tab belongs to the FORM, not the field — falling through is what moves to the next one.
            default:
                return false;
        }
    }

    /// <summary>Moves the caret, keeping the anchor when the movement is EXTENDING a selection and
    /// dropping it when it is not.</summary>
    private void MoveCaret(int to, bool selecting)
    {
        if (selecting) _anchor ??= Caret;
        else _anchor = null;
        Caret = to;
        NeedsRender = true;
    }

    private void Commit(TextEntry entry, string value, int caret, bool clearAnchor = false)
    {
        Caret = caret;
        if (clearAnchor) _anchor = null;
        NeedsRender = true;
        // The caret moves whether or not the app takes the change: a field with no OnChanged is a
        // read-only field, and pretending the character landed would be a lie the next frame undoes.
        entry.OnChanged?.Invoke(value);
    }

    /// <summary>Clears keyboard focus (pointer interaction, escape).</summary>
    public void ClearFocus()
    {
        if (_focused is null && _focusedPath is null) return;
        _focused = null;
        _focusedPath = null;
        NeedsRender = true;
    }

    /// <summary>
    /// Excel's keyboard over the focused sheet. Selection commands repaint without announcing;
    /// data commands go through the controller, whose Changed event the composing component
    /// already subscribes. Answers false for keys the sheet does not claim.
    /// </summary>
    private bool SheetKey(SheetSurface surface, string key, KeyModifiers modifiers)
    {
        var sheet = surface.Controller;
        var command = modifiers.HasFlag(KeyModifiers.Command);
        var shift = modifiers.HasFlag(KeyModifiers.Shift);

        // The SHARED keymap first — the web surface calls the same one, so they cannot drift.
        if (SheetKeymap.Handle(sheet, key, command, shift))
        {
            surface.OnChanged?.Invoke();
            return true;
        }

        // What stays platform-side: leaving the sheet, and the clipboard.
        if (key == "Escape")
        {
            EndEditing();
            return true;
        }
        if (command && key.Length == 1)
        {
            switch (char.ToLowerInvariant(key[0]))
            {
                case 'c':
                    if (Clipboard is { } copyBoard) copyBoard.Write(sheet.CopyTsv());
                    return true;
                case 'x':
                    if (Clipboard is { } cutBoard) cutBoard.Write(sheet.CopyTsv());
                    sheet.ClearSelection();
                    return true;
                case 'v':
                    if (Clipboard?.Read() is { Length: > 0 } pasted) sheet.PasteTsv(pasted);
                    return true;
            }
        }
        return false;
    }

    /// <summary>Runs the focused control, the way Enter and Space do everywhere else. Answers false
    /// when nothing holds focus, so the shell can let the key travel on.</summary>
    public bool ActivateFocused()
    {
        var regions = _lastFrame?.HitRegions;
        if (regions is null) return false;
        for (var i = 0; i < regions.Count; i++)
        {
            var region = regions[i];
            if (!IsFocused(region) || region.Node.Disabled) continue;
            // Resolved out of THIS frame's regions rather than the remembered node: same reason as
            // the press — the handler on a node from three rebuilds ago closes over dead state.
            region.Node.OnPressed?.Invoke();
            NeedsRender = true;
            return true;
        }
        return false;
    }

    /// <summary>
    /// The frame's semantics tree, in reading order — what a platform accessibility bridge hands
    /// to VoiceOver/TalkBack. Derived from the last realized frame; empty before the first render.
    /// </summary>
    public IReadOnlyList<SemanticNode> Semantics() =>
        _lastFrame is null ? Array.Empty<SemanticNode>() : SemanticsTree.Collect(_lastFrame);

    /// <summary>
    /// Runs the control at <paramref name="path"/>, the way a screen reader's activate action
    /// does. Resolved out of THIS frame's regions — same reason as every press: the handler on a
    /// node from an old rebuild closes over dead state. Answers false when the path holds nothing
    /// pressable.
    /// </summary>
    public bool ActivatePath(string path)
    {
        var regions = _lastFrame?.HitRegions;
        if (regions is null) return false;
        for (var i = 0; i < regions.Count; i++)
        {
            var region = regions[i];
            if (region.Path != path || region.Node.Disabled) continue;
            region.Node.OnPressed?.Invoke();
            NeedsRender = true;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Assistive tech adjusts a slider-role element (VoiceOver's swipe up/down): +1 increments,
    /// −1 decrements — the same OnAdjust the keyboard's arrows drive, resolved by path.
    /// </summary>
    public bool AdjustPath(string path, int direction)
    {
        var stops = _lastFrame?.FocusStops;
        if (stops is null) return false;
        for (var i = 0; i < stops.Count; i++)
        {
            if (stops[i].Path != path || stops[i].Adjustable is not { } adjustable) continue;
            adjustable.OnAdjust(direction);
            NeedsRender = true;
            return true;
        }
        return false;
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
        // A fill drag: the preview follows the pointer, one dominant axis at a time.
        if (_sheetFilling && _lastFrame is not null)
        {
            var fillRegions = _lastFrame.SheetRegions;
            for (var i = 0; i < fillRegions.Count; i++)
            {
                if (fillRegions[i].Path != _textPath) continue;
                var sheet = fillRegions[i].Surface.Controller;
                sheet.UpdateFill(CellAt(fillRegions[i], x, y));
                fillRegions[i].Surface.OnChanged?.Invoke();
                NeedsRender = true;
                break;
            }
            return;
        }

        // A sheet drag: the anchor cell stays, the focus cell follows the pointer.
        if (_sheetDragging && _lastFrame is not null)
        {
            var sheetRegions = _lastFrame.SheetRegions;
            for (var i = 0; i < sheetRegions.Count; i++)
            {
                if (sheetRegions[i].Path != _textPath) continue;
                var sheet = sheetRegions[i].Surface.Controller;
                var cell = CellAt(sheetRegions[i], x, y);
                if (cell == sheet.Selection.Focus) break;
                sheet.Selection = new SheetRange(sheet.Selection.Anchor, cell);
                sheetRegions[i].Surface.OnChanged?.Invoke();
                NeedsRender = true;
                break;
            }
            return;
        }

        // The same, over a code surface: the anchor stays put and the FOCUS follows the pointer,
        // which is exactly what CodeRange already models.
        if (_codeDragging && _lastFrame is not null)
        {
            var surfaces = _lastFrame.CodeRegions;
            for (var i = 0; i < surfaces.Count; i++)
            {
                if (surfaces[i].Path != _textPath) continue;
                var editor = surfaces[i].Surface.Editor;
                var at = PositionIn(surfaces[i], x, y);
                if (at.Equals(editor.Selection.Focus)) break;
                editor.Selection = new CodeRange(editor.Selection.Anchor, at);
                surfaces[i].Surface.OnChanged?.Invoke();
                NeedsRender = true;
                break;
            }
            return;
        }

        // A press that began in a field is DRAWING a selection: the anchor stays where the press
        // landed and the caret follows the pointer, so dragging back past the start reverses the
        // range instead of collapsing it.
        if (_dragSelecting && TextTarget is { } editing && _lastFrame is not null)
        {
            var fields = _lastFrame.TextRegions;
            for (var i = 0; i < fields.Count; i++)
            {
                if (fields[i].Path != _textPath) continue;
                var index = IndexAt(editing, x - fields[i].Bounds.X);
                if (index == Caret) break;
                Caret = index;
                NeedsRender = true;
                break;
            }
            return;
        }

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

        // A finger on a list. The content is UNDER it and follows it exactly — no smoothing, which
        // is what makes a surface feel attached to the hand rather than chased by it.
        if (_pan is { } pan)
        {
            var along = pan.Axis == ScrollAxis.Horizontal ? x : y;
            var travelled = along - pan.Start;

            if (!pan.Active && MathF.Abs(travelled) > Touch.PressCancelSlop)
            {
                _pressed = null;
                _pressedPath = null;
                pan.Active = true;
            }

            if (pan.Active)
            {
                // Velocity from the LAST step only: a flick is what the hand was doing when it let
                // go, not the average of a long, wandering drag.
                var elapsed = _lastTimeMs - pan.LastTimeMs;
                if (elapsed > 0) pan.Velocity = (along - pan.LastAt) / elapsed;
                pan.LastAt = along;
                pan.LastTimeMs = _lastTimeMs;

                // The content moves OPPOSITE the finger: dragging up reveals what is below.
                if (_scrolls.ScrollTo(pan.Path, pan.FromOffset - travelled, pan.MaxOffset))
                    NeedsRender = true;
                _pan = pan;
                return;
            }

            _pan = pan;
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

    /// <summary>
    /// What the pointer should LOOK like where it is. The web has had this since it had a mouse —
    /// a hand over what you can press, a beam over what you can type into — and its absence is the
    /// kind of thing nobody reports and everybody feels: you end up clicking to find out whether
    /// something is a control.
    /// <para>
    /// Derived from the regions already registered rather than declared anywhere. A Pressable IS a
    /// hand and a field IS a beam; making the developer say so as well would be one more thing to
    /// forget, and the shared web lowering already answers it the same way, from the same tree.
    /// </para>
    /// </summary>
    public CursorShape CursorAt(float x, float y)
    {
        if (_lastFrame is null) return CursorShape.Default;
        var point = new Point(x, y);

        // Topmost first, exactly as dispatch does — a button drawn over a field is a button.
        // Explicit cursors (BoxStyle.Cursor) go first of all: a fill handle painted OVER a cell
        // says crosshair even though the cell underneath would say default.
        var cursors = _lastFrame.CursorRegions;
        for (var i = cursors.Count - 1; i >= 0; i--)
        {
            if (!cursors[i].Bounds.Contains(point)) continue;
            return cursors[i].Cursor switch
            {
                PointerCursor.Pointer => CursorShape.Pointer,
                PointerCursor.Text => CursorShape.Text,
                PointerCursor.NotAllowed => CursorShape.NotAllowed,
                PointerCursor.Crosshair => CursorShape.Crosshair,
                PointerCursor.ColResize => CursorShape.ColResize,
                PointerCursor.RowResize => CursorShape.RowResize,
                _ => CursorShape.Default,
            };
        }

        var fields = _lastFrame.TextRegions;
        var hits = _lastFrame.HitRegions;
        var links = _lastFrame.LinkRegions;

        for (var i = hits.Count - 1; i >= 0; i--)
        {
            if (!hits[i].Bounds.Contains(point)) continue;
            // A disabled control says so: the pointer is the only warning before the click that
            // does nothing.
            return hits[i].Node.Disabled ? CursorShape.NotAllowed : CursorShape.Pointer;
        }
        for (var i = fields.Count - 1; i >= 0; i--)
            if (fields[i].Bounds.Contains(point))
                return fields[i].Entry.Disabled ? CursorShape.NotAllowed : CursorShape.Text;
        for (var i = links.Count - 1; i >= 0; i--)
            if (links[i].Bounds.Contains(point)) return CursorShape.Pointer;

        return CursorShape.Default;
    }

    /// <summary>Where a scroll surface currently sits — the test seam for gesture regressions
    /// (a latched pan shows up here as an offset nobody asked for).</summary>
    public float ScrollOffsetOf(string path) => _scrolls.Get(path) ?? 0f;

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
    public bool PressDown(float x, float y, int clickCount = 1, KeyModifiers modifiers = KeyModifiers.None)
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

        // …and the topmost SCROLLABLE surface, the same way. A drag surface is the more specific
        // gesture and keeps priority; where there is none, a finger scrolls.
        _pan = null;
        if (_drag is null)
        {
            var scrollRegions = _lastFrame.ScrollRegions;
            for (var i = scrollRegions.Count - 1; i >= 0; i--)
            {
                var region = scrollRegions[i];
                if (!region.Bounds.Contains(point) || region.MaxOffset <= 0) continue;
                var along = region.Axis == ScrollAxis.Horizontal ? x : y;
                _pan = (region.Path, region.Axis, region.MaxOffset, along,
                    _scrolls.Get(region.Path) ?? region.Fallback, along, _lastTimeMs, 0, false);
                break;
            }
        }

        var regions = _lastFrame.HitRegions;
        _pressSwallowed = false;
        string? swallowedBy = null;
        for (var i = regions.Count - 1; i >= 0; i--)
        {
            var region = regions[i];
            if (!region.Bounds.Contains(point)) continue;
            if (region.Node.Disabled || region.Node.OnPressed is null)
            {
                // INERT — disabled, or enabled with nothing to run (a Button used as a Menu's
                // trigger visual has no handler of its own). Inert swallows against what is BEHIND
                // it, but not against the pressable that WRAPS it: the Menu's own press was being
                // eaten by its topmost inert trigger. An ancestor (its path is a prefix of the
                // inert one's) is the owner of the gesture; a stranger underneath stays protected.
                swallowedBy ??= region.Path;
                continue;
            }
            if (swallowedBy is not null && !IsAncestorPath(region.Path, swallowedBy))
            {
                _pressSwallowed = true;
                return true;
            }
            _pressed = region.Node;
            _pressedPath = region.Path;
            // Pressing a control takes focus off whatever had it — including a field being edited,
            // whose caret must not go on blinking somewhere the user is no longer looking.
            EndEditing();
            // Focused, but not RINGED: pressing Enter after clicking a button goes on working,
            // and the click leaves no ring behind it.
            Focus(region.Node, region.Path, visible: false);
            NeedsRender = true;
            return true;
        }

        if (swallowedBy is not null)
        {
            // An inert control was topmost and no wrapping pressable claimed the gesture: the
            // press dies here, and so does the LINK underneath — <a><button disabled> does not
            // navigate, and neither does this.
            _pressSwallowed = true;
            return true;
        }

        // No pressable claimed it: a field might. Fields register UNDER the buttons on purpose — a
        // button drawn over a search box is still a button.
        var fields = _lastFrame.TextRegions;
        for (var i = fields.Count - 1; i >= 0; i--)
        {
            if (!fields[i].Bounds.Contains(point)) continue;
            BeginEditing(fields[i], x);
            // Double-click: the word under the point. Triple: everything. The platform counts the
            // clicks (its double-click interval is a system setting, not ours to guess).
            if (clickCount >= 3) { _anchor = 0; Caret = fields[i].Entry.Value.Length; }
            else if (clickCount == 2) SelectWordAt(fields[i].Entry);
            _dragSelecting = clickCount < 2;   // a drag right after a double-click keeps the word
            NeedsRender = true;
            return true;
        }

        // …and so might a CODE surface. It registers under the fields for the same reason they
        // register under the buttons: whatever is drawn on top is what you meant to press.
        var surfaces = _lastFrame.CodeRegions;
        for (var i = surfaces.Count - 1; i >= 0; i--)
        {
            if (!surfaces[i].Bounds.Contains(point)) continue;
            BeginCodeEditing(surfaces[i]);
            var at = PositionIn(surfaces[i], x, y);
            var editor = surfaces[i].Surface.Editor;
            // Double-click: the word under the point. Triple: the line. The platform counts the
            // clicks — its double-click interval is a system setting, not ours to guess.
            if (clickCount >= 3) editor.SelectLine(at.Line);
            else if (clickCount == 2) editor.SelectWord(at);
            else editor.Selection = new CodeRange(at);
            _codeDragging = clickCount < 2;
            RestartBlink();
            surfaces[i].Surface.OnChanged?.Invoke();
            NeedsRender = true;
            return true;
        }

        // …and a SPREADSHEET surface, the same way: click selects the cell, drag grows the range.
        var sheetRegions = _lastFrame.SheetRegions;
        for (var i = sheetRegions.Count - 1; i >= 0; i--)
        {
            if (!sheetRegions[i].Bounds.Contains(point)) continue;
            var sheet = sheetRegions[i].Surface.Controller;
            var changed = _textPath != sheetRegions[i].Path;
            if (changed) EndEditing();
            _textPath = sheetRegions[i].Path;
            _focused = null;
            _focusedPath = null;
            var cell = CellAt(sheetRegions[i], x, y);
            if (sheet.Editing) sheet.CommitEdit();   // clicking away lands the draft, like Excel

            // Excel's little square: a grab near the selection's bottom-right corner starts a
            // FILL drag, not a new selection.
            var corner = FillHandleCorner(sheetRegions[i], sheet.Selection);
            if (Math.Abs(x - corner.X) <= FillHandleGrab && Math.Abs(y - corner.Y) <= FillHandleGrab)
            {
                sheet.BeginFill();
                _sheetFilling = true;
                sheetRegions[i].Surface.OnChanged?.Invoke();
                NeedsRender = true;
                return true;
            }

            if (modifiers.HasFlag(KeyModifiers.Shift))
                sheet.SelectTo(cell);   // the anchor stands, the selection stretches to the click
            else
                sheet.Selection = new SheetRange(cell);
            if (clickCount >= 2)
                sheet.BeginEdit(sheet.Document.GetCell(cell));
            _sheetDragging = clickCount < 2 && !modifiers.HasFlag(KeyModifiers.Shift);
            sheetRegions[i].Surface.OnChanged?.Invoke();
            NeedsRender = true;
            return true;
        }

        // A press on empty space ends editing, which is how every form on every platform behaves —
        // and is the only way to leave a field without Tab.
        EndEditing();
        ClearFocus();
        return _drag is not null || _pan is not null;
    }

    /// <summary>
    /// The system took the gesture away without ever lifting the finger — UIKit's
    /// <c>touchesCancelled</c>, a window losing key, a call arriving. Nothing was decided, so
    /// nothing is reported: the press drops and the surface glides back to where the caller last
    /// put it. The web controller answers <c>pointercancel</c> the same way.
    /// </summary>
    public void PointerCancel()
    {
        _pan = null;

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
        _dragSelecting = false;
_codeDragging = false;
        _sheetDragging = false;
        if (_sheetFilling && _lastFrame is not null)
        {
            // The fill drag lands: the source tiles across the preview as ONE undo step.
            _sheetFilling = false;
            var fillRegions = _lastFrame.SheetRegions;
            for (var i = 0; i < fillRegions.Count; i++)
            {
                if (fillRegions[i].Path != _textPath) continue;
                fillRegions[i].Surface.Controller.CommitFill();
                fillRegions[i].Surface.OnChanged?.Invoke();
                NeedsRender = true;
                break;
            }
            return true;
        }
        if (_pressSwallowed)
        {
            // Suppressed OUTCOME, not suppressed CLEANUP. The press-down armed the same gesture
            // candidates every press arms — the pan for a scrollable under the point above all —
            // and returning without clearing them left the pan latched: every mouse move after
            // releasing on an inert control kept scrolling, stuck to the pointer with no button
            // down. The release still ends the gesture; it just performs nothing.
            _pressSwallowed = false;
            _drag = null;
            _pan = null;
            _pressed = null;
            _pressedPath = null;
            return true;
        }

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

        // A flick carries on. The glide's own decay does the slowing, so the release only says how
        // much runway the speed bought.
        if (_pan is { Active: true } flick)
        {
            _pan = null;
            if (_scrolls.Fling(flick.Path, -flick.Velocity, flick.MaxOffset)) NeedsRender = true;
            return true;
        }
        _pan = null;

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
    /// <summary>
    /// WHERE the keyboard focus is, as the stable path the frame's <see cref="FocusStop"/>s carry
    /// — null when nothing holds it. Read-only and public because the host is not the only thing
    /// that needs the answer: a platform accessibility bridge has to report the focused element,
    /// and any harness driving this window has to know where Tab left off.
    /// <para>
    /// A field being EDITED holds the focus as much as a button wearing the ring does — the caret
    /// is simply that field's own indicator, which is why the ring state is tracked separately.
    /// Tab's own "where am I" already merges the two; reporting only the ring's half told the
    /// outside world that a form being typed into had focus nowhere.
    /// </para>
    /// </summary>
    public string? FocusedPath => _textPath ?? _focusedPath;

    public bool KeyDown(string key, KeyModifiers modifiers = KeyModifiers.None)
    {
        // An app's own chord wins: ⌘K is the app's, and a control holding focus has no claim on it.
        var bindings = _lastFrame?.Shortcuts;
        if (bindings is not null)
        {
            for (var i = bindings.Count - 1; i >= 0; i--)
            {
                var chord = bindings[i].Chord;
                if (chord.Modifiers != modifiers) continue;
                if (!string.Equals(chord.Key, key, StringComparison.OrdinalIgnoreCase)) continue;
                bindings[i].OnPressed();
                NeedsRender = true;
                return true;
            }
        }

        // A SPREADSHEET under the selection speaks Excel: arrows move (⌘ jumps to the data edge,
        // ⇧ extends), Tab/Enter walk, Delete clears, ⌘C/⌘V are TSV through the platform clipboard.
        if (SheetTarget is { } sheetSurface)
        {
            if (SheetKey(sheetSurface, key, modifiers))
            {
                NeedsRender = true;
                return true;
            }
        }

        // A CODE surface under the caret takes the key through the shared keymap — the same
        // function the web half calls, so the two never drift on what ⌥← or ⇧Tab mean.
        if (CodeTarget is { } surface)
        {
            if (CodeKeymap.Handle(surface.Editor, key, modifiers, Clipboard))
            {
                RestartBlink();
                surface.OnChanged?.Invoke();
                NeedsRender = true;
                return true;
            }
            // Escape LEAVES the editor rather than being swallowed: an editor that traps Escape is
            // an editor you cannot get out of without a mouse.
            if (key == "Escape") { EndEditing(); return true; }
        }

        // Then the keys every UI owes the user, whatever the app declared. Reaching a control
        // without a mouse and running it from the keyboard is not a nicety — for some people it is
        // the only way in, and it is also how anyone fills a form quickly.
        if (TextTarget is { } editing && EditKey(editing, key, modifiers)) return true;

        // An Adjustable under focus answers the arrows — the reason it exists. Resolved out of
        // THIS frame's stops by path: the node from the frame it was focused in is long gone.
        if (modifiers == KeyModifiers.None && _focusedPath is { Length: > 0 } focusedPath
            && (key is "ArrowLeft" or "ArrowRight" or "ArrowUp" or "ArrowDown")
            && _lastFrame?.FocusStops is { } stops)
        {
            for (var i = 0; i < stops.Count; i++)
            {
                if (stops[i].Path != focusedPath || stops[i].Adjustable is not { } adjustable) continue;
                adjustable.OnAdjust(key is "ArrowRight" or "ArrowUp" ? 1 : -1);
                NeedsRender = true;
                return true;
            }
        }

        switch (key)
        {
            case "Tab":
                return modifiers.HasFlag(KeyModifiers.Shift) ? FocusPrevious() : FocusNext();

            case "Enter" or " " or "Space" when modifiers == KeyModifiers.None:
                return ActivateFocused();

            case "Escape" when modifiers == KeyModifiers.None:
                if (Focused is null && _focusedPath is null) return false;
                ClearFocus();
                return true;

            // A blinking caret is a running animation: the frame clock has to keep turning for it,
            // and only while a field is actually being edited.
            
        }
        return false;
    }
}
