using System.Threading;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// The frame driver for a Photon surface: keeps the tree of its root in a <see cref="BuildOwner"/>
/// across frames, draws a frame when a <c>SetState</c>, input or the system asks for one (building,
/// then laying out and painting), and dispatches input to the realized hit regions. The platform
/// shells wrap it with vsync pacing and real input. A stateful component's state lives on the
/// instance its position retains (see <see cref="StatefulComponent"/>).
/// </summary>
public sealed class PhotonHost
{
    private readonly VisualNode _root;
    private readonly IAppTheme _theme;
    private readonly ITextMeasurer? _measurer;
    private readonly float _typeScale;
    private RealizeResult? _lastFrame;
    private readonly LiveRegionAnnouncer _announcer = new();

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

        // ONCE, for the whole tree: a SetState marks its position in the owner, and the owner asks
        // for the frame. Nothing subscribes per instance, so nothing can subscribe twice (#935: the
        // instance store reported a root it already held as entering every frame, and the host
        // added a handler each time).
        _owner.BuildRequested += RequestFrame;

        // Work posted from a worker thread has to WAKE an idle window, or it runs whenever something
        // else next needs a frame — a scanner's results appearing on the next mouse move.
        // WEAKLY, like PhotonHotReload.Register below and for the same reason: the dispatcher is a
        // process static, and a host is not kept alive by having subscribed to one.
        PhotonDispatcher.Shared.WakeOnPost(this);

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
    public Density Density
    {
        get => _density;
        set
        {
            if (_density == value) return;
            _density = value;
            // Every control reads it while it builds, so the next frame builds them all again.
            RebuildAll();
        }
    }

    private Density _density = Density.Comfortable;
    public float Width { get; private set; }
    public float Height { get; private set; }

    /// <summary>True when state changed since the last <see cref="RenderFrame"/> (starts true), or
    /// when the last frame carried running loop motion — animated frames keep the loop hot.</summary>
    /// <remarks>
    /// VOLATILE, because <see cref="Invalidate"/> is now called from any thread — the dispatcher
    /// wakes an idle window when work is posted from a worker. A plain bool write is atomic but
    /// carries no barrier, so the render loop could keep reading a cached false and the frame the
    /// posted work is waiting for would arrive whenever something else happened to need one. That
    /// is not a crash; it is a scanner's results appearing on the next mouse move, which is the
    /// symptom this whole path exists to remove.
    /// </remarks>
    public bool NeedsRender
    {
        get => Volatile.Read(ref _needsRender);
        private set => Volatile.Write(ref _needsRender, value);
    }

    private bool _needsRender = true;

    /// <summary>Whether the next frame rebuilds every component rather than the marked ones — an
    /// int, so the frame can take it and clear it in one step while another thread sets it.</summary>
    private int _rebuildAll = 1;

    /// <summary>
    /// Asks for a frame that builds EVERY component, for what changes what every build reads: an
    /// invalidation the host is told of, a resize, a density change, a hot reload. Every other
    /// request builds what is marked, as on the web (#976): a <c>SetState</c> marks its own position
    /// (<see cref="RequestFrame"/>), input the host routes into a field or a surface is handed to
    /// the app, which says what it changed (<see cref="RebuildUnlessTold"/>), and the press, the
    /// focus, the hover, a caret and an offset are the host's own to paint, so a frame for them
    /// builds nothing.
    /// </summary>
    private void RebuildAll()
    {
        Volatile.Write(ref _rebuildAll, 1);
        NeedsRender = true;
    }

    /// <summary>
    /// After the host handed input it routed into a field or a surface to the app's handler, which
    /// ran while the owner's count of <c>SetState</c>s stood at <paramref name="marks"/>. The app says
    /// with <c>SetState</c> what it changed, as on the web, and the next frame builds what it marked
    /// (#976). A handler that called none leaves the host unable to see where the value lives,
    /// because nothing in the tree says so: a form can keep a field's value without a
    /// <c>SetState</c>, and the stateful component nearest above a field is not where its value lives
    /// when it is a field component of its own, as <c>TextInput</c> is. So that frame rebuilds every
    /// component, as every frame after input did before, and the key still shows.
    /// </summary>
    private void RebuildUnlessTold(int marks)
    {
        if (_owner.Marks == marks) RebuildAll();
        NeedsRender = true;
    }

    /// <summary>The surface took input into its model, and its component hears it through
    /// <paramref name="onChanged"/>, the cue its contract gives it to <c>SetState</c>
    /// (<see cref="RebuildUnlessTold"/>).</summary>
    private void SurfaceChanged(Action? onChanged)
    {
        var marks = _owner.Marks;
        onChanged?.Invoke();
        RebuildUnlessTold(marks);
    }

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
    public void Invalidate() => RebuildAll();

    /// <summary>A position in the tree was marked by a <c>SetState</c>: the next frame builds it, and
    /// only it (the frame's owner knows which).</summary>
    private void RequestFrame() => NeedsRender = true;

    /// <summary>Work was posted for the thread that draws (<see cref="PhotonDispatcher"/>): it runs
    /// at the top of the next frame, which builds what the work marks and nothing of its own. Safe
    /// from any thread, like <see cref="Invalidate"/>.</summary>
    internal void Wake() => NeedsRender = true;

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
        RebuildAll();
    }

    /// <summary>W4: the platform text service (null = deterministic placeholder bars).</summary>
    public Framework.ITextRasterizer? TextRasterizer { get; set; }

    /// <summary>W4: the platform icon service (null = disc placeholders).</summary>
    public Framework.IIconRasterizer? IconRasterizer { get; set; }

    private readonly TextRasterCache _textCache = new();

    /// <summary>The platform clipboard (null = the copy keys do nothing, which is the honest
    /// answer on a host that has none).</summary>
    public ITextClipboard? Clipboard { get; set; }

    /// <summary>
    /// Which keyboard tradition this window's users live in — what ⌘← or Ctrl+← MEANS inside a code
    /// surface (<see cref="Primitives.KeyboardConvention"/>). Derived from the operating system the
    /// process runs on, because that is the one fact a host knows and a model never should; settable
    /// so a test can drive either tradition from any machine.
    /// </summary>
    public KeyboardConvention KeyboardConvention { get; init; } =
        OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst()
            ? KeyboardConvention.Apple
            : KeyboardConvention.Standard;

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

    /// <summary>
    /// The part of the window something of the system's covers: a phone's keyboard, Flutter's
    /// <c>MediaQuery.viewInsets</c>. The tree lays out in what is left, as Flutter's
    /// <c>Scaffold</c> does by default, so a form ends above the keyboard instead of under it; the
    /// <see cref="SafeAreaInsets"/> the inset already covers count once, as Flutter's padding does
    /// under it; and when the keyboard rises, the field being edited is scrolled into what is left.
    /// Zero on a desktop, and on a phone with the keyboard down.
    /// </summary>
    public EdgeInsets ViewInsets
    {
        get => _viewInsets;
        set
        {
            if (value == _viewInsets) return;
            _viewInsets = value;
            NeedsRender = true;
        }
    }

    private EdgeInsets _viewInsets;

    /// <summary>The height the last frame laid out in. A frame that lays out in less reveals the
    /// field being edited.</summary>
    private float _laidHeight;

    /// <summary>
    /// How far the screen is drawn above where it was laid out, so that the field being edited
    /// clears the keyboard where nothing around it scrolls: a form that does not fit what the
    /// keyboard leaves, a phone turned on its side. Android's own <c>adjustPan</c>, which Android
    /// itself picks for a window with nothing to scroll; Flutter leaves the field covered there.
    /// Taken only while the edit has less room than the screen had before it, so it holds whether
    /// the keyboard arrives as an inset or as a window the system shortened for it, and a desktop
    /// window that was always too short for its form is not moved. Zero with nothing being edited,
    /// with the field in view, and for a field a scroll view holds: the scroll view brings it into
    /// view instead, and what the person scrolls away is theirs to scroll back. A pointer lands where
    /// the screen is drawn, and the caret and the semantics are reported where they are drawn.
    /// </summary>
    private float _lift;

    /// <summary>The height the screen laid out in before the edit began, or the most it has had since:
    /// a frame laid out in less while something is edited lost that room to the keyboard, as an
    /// inset or as a shorter window, or to a phone turned on its side.</summary>
    private float _roomBeforeEdit;

    /// <summary>
    /// The corner the system's WINDOW CONTROLS float in under <see cref="WindowChrome.Unified"/> —
    /// Start on a Mac, End on Windows, Top the strip's height — for the one <see cref="SafeArea"/>
    /// that asks for <see cref="SafeEdges.WindowControls"/>: the toolbar that is the title bar. Zero
    /// on a phone, under a standard title bar, and anywhere else the window has a bar of its own.
    /// </summary>
    public EdgeInsets WindowControlsInsets { get; set; }

    /// <summary>The NAVIGATION seam (write-once Link): a tap no pressable claims, landing on a link
    /// region, reports the destination here — the platform shell maps it to a page (the native router's
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
        // FIRST, before anything is read: this thread is the one that draws, and whatever a worker
        // thread posted runs here, where mutating a component's fields races nothing. Both halves
        // belong at the top of the frame — the binding because the thread that renders is the only
        // one whose identity matters, the drain because this is the moment nothing is being built.
        PhotonDispatcher.Shared.BindToCurrentThread();
        PhotonDispatcher.Shared.Drain();

        // Then the frame clock: whoever moves every frame gets this frame's time BEFORE the tree is
        // built, so what they SetState here is what this frame draws. After the drain, so a worker's
        // posted result and the tick that reads it land in the same frame, in that order.
        PhotonFrameTicker.Shared.Fire(timeMs);

        // An edit landed since the last frame: drop the content caches BEFORE realizing, on this
        // thread, where they are owned. Most edits would miss them anyway (the keys are content),
        // but a patched rasterizer or an edited icon path re-rasters fresh — once.
        if (_hotReloadGeneration != PhotonHotReload.Generation)
        {
            _hotReloadGeneration = PhotonHotReload.Generation;
            _textCache.Clear();
            _iconCache.Clear();
            _imageCache.Clear();
            // A patched Build has to run to be seen, in every component that has one.
            Volatile.Write(ref _rebuildAll, 1);
        }

        // What was typed since the last frame is in the tree now, as the app took it: the frame's
        // value is the field's again. The caret is placed first, against the value it was moved in:
        // read after, it clamped to the last frame's shorter text and drew a character behind.
        var caretIndex = CaretIndex;
        var selection = Selection;
        _edited = null;

        builder.Clear(_theme.Background.Resolve(Mode));
        if (RenderScale != 1f) builder.PushTransform(Matrix2D.Scale(RenderScale, RenderScale));
        if (_lift != 0) builder.PushTransform(Matrix2D.Translation(0, -_lift));
        _lastTimeMs = timeMs;
        // The tree lays out above what the system covers, and a SafeArea at the bottom pads only
        // what the keyboard leaves of the home indicator: the keyboard is already in its way.
        var height = MathF.Max(0, Height - _viewInsets.Bottom);
        var shorter = height < _laidHeight;
        _laidHeight = height;
        if (_textAt is null || height > _roomBeforeEdit) _roomBeforeEdit = height;
        var safeArea = _viewInsets.Bottom > 0
            ? SafeAreaInsets with { Bottom = MathF.Max(0, SafeAreaInsets.Bottom - _viewInsets.Bottom) }
            : SafeAreaInsets;
        // A gliding scroll advances BEFORE the frame is realized, so this frame paints where it
        // moved to; while anything is still gliding the host keeps asking for frames.
        // Resolved HERE, not when it was set: ReducedMotion can arrive from the system at any
        // point, and it always wins.
        _scrolls.Smooth = SmoothScroll && !ReducedMotion;
        var gliding = _scrolls.Advance(timeMs);
        // A table's glide and fling move on the same clock: its scroll is the table's own.
        if (AdvanceTables(timeMs)) gliding = true;
        var previousFrame = _lastFrame;
        // Taken and cleared as late as possible: whatever the drain, the frame ticker or an edit
        // asked for before this point is in this frame, and anything after it is in the next.
        var rebuildAll = Interlocked.Exchange(ref _rebuildAll, 0) == 1;
        _lastFrame = PhotonRealizer.Realize(_root, Width, height, _theme, Mode, builder, _measurer, _typeScale, _pressed, _focusVisible ? _focused : null, _hovered, _owner, timeMs, ReducedMotion, _transitions, _presences, _drags, TextRasterizer, _textCache, RenderScale, IconRasterizer, _iconCache, ImageLoader, _imageCache, safeArea, _pressedAt,
            _focusVisible ? _focusedId : 0, _textAt, caretIndex, CaretVisible,
            selection.Start, selection.End, density: Density,
            markedText: _marked, composing: _composing,
            hoveredAt: _hoverAt,
            nodePool: RecycleFrames ? _nodePool : null,
            windowControlsInsets: WindowControlsInsets, sizedLike: previousFrame, rebuildAll: rebuildAll, log: Log);
        if (_lift != 0) builder.Pop();
        if (RenderScale != 1f) builder.Pop();
        // The frame shows what the app made of the keyboard's edit. A composition in a text the app
        // did not keep is over (see _composing): the keyboard is about to be told that text.
        if (_composing is { } composing && TextTarget?.Value != composing.Text) _composing = null;
        // The frame we just replaced is OURS to discard — nobody else holds production frames.
        // (Opt-in: anything that retains RealizeResults — tests, tooling — leaves this off and
        // every tree stays untouched forever.)
        if (RecycleFrames && previousFrame is not null)
        {
            _nodePool.RecycleTree(previousFrame.Root);
            foreach (var overlay in previousFrame.OverlayRoots) _nodePool.RecycleTree(overlay);
        }
        // A control whose position left the tree took the focus with it, as an element removed from a
        // page does in a browser: the button that stands at its place next is not the one that was
        // focused, and a field that left is not being edited (#976).
        if (_focusedAt is { IsActive: false })
        {
            _focused = null;
            ForgetFocus();
        }
        if (_textAt is { IsActive: false }) EndEditing();
        // WHAT CHANGED, once per frame. Here rather than in the semantics walk because that walk is
        // asked by a bridge whenever assistive tech is curious — never, once, or three times in a
        // frame — and an announcement posted from it would fire a number of times nobody chose.
        // Costs one length check in a frame with no live region, which is almost every frame.
        _announcer.Observe(_lastFrame, timeMs);
        AdoptAutofocus();
        AdoptFocusRequests();
        // A subscribed frame ticker IS motion: frames keep flowing while anyone wants them, and the
        // loop goes idle the moment the last subscription is disposed. Motion builds nothing, as no
        // request does unless something marked a position. A position still waiting to build keeps
        // the request too: a SetState from a scroll callback, from a hook at the end of the frame or
        // from a build is waiting in the owner, and a write of `false` here would leave it there
        // until something else asked for a frame.
        NeedsRender = _lastFrame.HasActiveMotion || gliding || PhotonFrameTicker.Shared.HasSubscribers
            || _owner.HasDirty;

        // A code surface whose caret was MOVED since the last frame is brought into view — by a key,
        // a command, a find stepping to a match. After the frame, because that is when the caret's
        // new place exists in window coordinates; the scroll it causes paints on the next one.
        if (RevealCodeCarets()) NeedsRender = true;

        // The space shrank under the edit, and this frame laid out in what is left: the field being
        // edited is brought into it, or the person types into a field they cannot see. A keyboard
        // rising does that, as an inset or, where the system resizes the window for it, as a
        // shorter window; so does a phone turned on its side.
        if (shorter && RevealEditing()) NeedsRender = true;
        // Where nothing scrolls the field into view, the screen is lifted until it shows.
        if (Lift(height)) NeedsRender = true;

        // The caret is 2Hz motion, not vsync motion. Holding NeedsRender for it pinned the whole
        // loop to the display's refresh — 120 presents a second on a ProMotion panel, measured, to
        // flip a rectangle twice a second. So an editing caret SCHEDULES instead: the next frame is
        // due at the next blink transition, and the shells' idle ticks ask IsFrameDue alongside
        // NeedsRender. While a drag draws a selection the caret does not blink at all (it is forced
        // solid), and the pointer is what drives frames — nothing to schedule.
        _nextFrameDueMs = _textAt is not null && !NeedsRender && !_dragSelecting && !_codeDragging
            ? _caretPhaseMs + (MathF.Floor((_lastTimeMs - _caretPhaseMs) / CaretBlinkMs) + 1) * CaretBlinkMs
            : null;
        return _lastFrame;
    }

    /// <summary>The tree this host keeps across frames: every position, the instances it retains, and
    /// the order its components mount and unmount in, the root's included (see BuildOwner).</summary>
    private readonly BuildOwner _owner = new();


    private Pressable? _pressed;

    /// <summary>An inert control consumed the press — the release must not fall through to links.</summary>
    private bool _pressSwallowed;

    /// <summary>The pressed control's position, so the release finds it in whatever frame is current
    /// by then. The node itself does not survive a Build; its position does, for as long as the same
    /// control stands there, and a control that took its place is not the one pressed.</summary>
    private Element? _pressedAt;
    private VisualNode? _hovered;
    /// <summary>The hover CHAIN — every region under the pointer, topmost first, tracked by position
    /// (the press's rebuild-surviving rule) with the node kept for the Hoverable callbacks. CSS
    /// :hover matches every ancestor, and the native pointer answers the same question.</summary>
    private readonly List<Element> _hoverAt = new();
    private readonly List<VisualNode> _hoverNodes = new();
    private readonly TransitionClock _transitions = new();
    private readonly ScrollClock _scrolls = new();

    /// <summary>
    /// A finger on a scrollable surface. Armed on press and only ACTIVE once it has travelled past
    /// the slop along the view's own axis — so a tap inside a list is still a tap, and a sideways
    /// swipe never scrolls a vertical list. It holds the view's position, which keeps the offset: a
    /// view that leaves under the finger takes the gesture with it, rather than handing it to the
    /// next view drawn at its place.
    /// </summary>
    private (Element Element, ScrollAxis Axis, float MaxOffset, float Start, float FromOffset,
        float LastAt, float LastTimeMs, float Velocity, bool Active)? _pan;

    /// <summary>
    /// A press a table viewport's scroll took. On a scrollbar it is the scroll's from the start
    /// (<c>Active</c>). A finger's is a CANDIDATE, as a pan of a scroll view is: the press goes on to the
    /// cell under it, and the table takes it only once it travels past the slop, cancelling that press,
    /// so a tap on a cell is still a tap.
    /// </summary>
    private (TableViewport Viewport, Rect Bounds, float StartX, float StartY, bool Active)? _tablePress;

    /// <summary>
    /// Whether a wheel or drag GLIDES to where it was sent instead of jumping there. On by default
    /// — a jump reads as a redraw rather than as movement, and the eye loses its place. Reduce
    /// Motion turns it off whatever the app said: it is movement, and that setting means it.
    /// </summary>
    public bool SmoothScroll { get; set; } = true;

    /// <summary>
    /// Where this host's layout says what it decided that the app did not write: a table that fills an
    /// axis with no bound takes the visible extent of the scroller around it, and says so here. Every
    /// host starts with <see cref="DefaultLog"/>, which the application arms with its logger.
    /// </summary>
    public Action<string>? Log { get; set; } = DefaultLog;

    /// <summary>The <see cref="Log"/> a new host starts with. The application points it at its logger,
    /// so a shell that builds a host passes nothing for it.</summary>
    public static Action<string>? DefaultLog { get; set; }
    private readonly PresenceClock _presences = new();
    private readonly DragClock _drags = new();

    /// <summary>The in-flight drag candidate: armed on press inside a drag region, ACTIVATED once
    /// the pointer travels past the slop (which cancels the pressable press), resolved on release.</summary>
    // The armed gesture surface. `Node` is a DragDismiss or a Draggable — the rules live on it, so
    // the host only tracks where the finger started and whether the slop has been passed. `Element`
    // is its position, which keeps the drag.
    private (Element Element, VisualNode Node, float StartX, float StartY, float Extent, bool Active)? _drag;

    /// <summary>The clock of the last rendered frame — glide-backs anchor to it on release.</summary>
    private float _lastTimeMs;
    private Pressable? _focused;

    /// <summary>The id the keyboard focus is held by, zero for none: a control's position's, or a
    /// linked run's. With it, the position the focused stop sits in, which the focus-within and the
    /// reveal questions walk up from.</summary>
    private int _focusedId;
    private Element? _focusedAt;

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

        // Where the focus is NOW, found by its id: the tree has been rebuilt on every keystroke since
        // it was set, so the node it once pointed at is long gone. Reference identity here meant Tab
        // silently restarting from the first control every time the app called SetState.
        var current = -1;
        if (FocusedId is { } here)
        {
            for (var i = 0; i < stops.Count; i++)
            {
                if (stops[i].Id != here) continue;
                current = i;
                break;
            }
        }

        var start = current < 0 ? (step > 0 ? 0 : stops.Count - 1) : current + step;
        var index = ((start % stops.Count) + stops.Count) % stops.Count;
        var stop = stops[index];

        return Land(stop);
    }

    /// <summary>
    /// Puts the focus ON a stop, whatever kind it is. Shared by the Tab walk and by a screen
    /// reader's activate, because landing somewhere is one behaviour and not two: a field that
    /// starts editing when Tab reaches it has to start editing when TalkBack's double tap does.
    /// </summary>
    private bool Land(FocusStop stop)
    {
        // Bring it into view BEFORE it takes focus: a caret blinking somewhere off screen is the
        // same as no caret at all.
        ScrollIntoView(stop);

        if (stop.Entry is not null)
        {
            // Landing on a field starts editing it — Tab into a field and type is the whole point.
            _focused = null;
            ForgetFocus();
            BeginEditing(stop.Entry, stop.Element);
            return true;
        }

        EndEditing();
        if (stop.Adjustable is not null)
        {
            // A slider under focus: the ring shows, Enter does nothing, and the ARROWS are the
            // whole point — dispatched from KeyDown against the current frame's stop.
            _focused = null;
            FocusOn(stop);
            _focusVisible = true;
            NeedsRender = true;
            return true;
        }
        _focused = stop.Pressable;
        FocusOn(stop);
        _focusVisible = true;   // arrived without a pointer: this is exactly who the ring is for
        NeedsRender = true;
        return true;
    }

    /// <summary>
    /// Scrolls whatever contains this stop until the stop is inside it.
    /// <para>
    /// The container is found by POSITION: a scroll region whose element lies above the stop's holds
    /// it, and the deepest such one is the innermost. The tree already says who contains whom — asking
    /// it is cheaper and truer than keeping a second map that can disagree.
    /// </para>
    /// </summary>
    private bool ScrollIntoView(FocusStop stop)
    {
        if (_lastFrame is null || stop.Bounds.Height <= 0) return false;
        if (RevealInTable(stop)) return true;

        var regions = _lastFrame.ScrollRegions;
        var best = -1;
        for (var i = 0; i < regions.Count; i++)
        {
            if (regions[i].MaxOffset <= 0) continue;
            if (!IsUnder(stop.Element, regions[i].Element)) continue;
            if (best < 0 || regions[i].Element.Depth > regions[best].Element.Depth) best = i;
        }
        if (best < 0) return false;

        var (laid, toScreen) = Laid(stop);
        var (own, viewport) = InScrollSpace(regions, best, laid, toScreen);
        var horizontal = regions[best].Axis == ScrollAxis.Horizontal;
        var (start, end, viewStart, viewEnd) = horizontal
            ? (own.X, own.X + own.Width, viewport.X, viewport.X + viewport.Width)
            : (own.Y, own.Y + own.Height, viewport.Y, viewport.Y + viewport.Height);

        // Scrolled to just inside the near edge, with a control's worth of margin, rather than to
        // the exact edge: a field flush against the top of a viewport looks like the first one, and
        // there is no way to tell there is more above it.
        var margin = MathF.Min(ScrollIntoViewMargin, (viewEnd - viewStart) / 4);
        var delta = 0f;
        if (start < viewStart + margin) delta = start - viewStart - margin;
        else if (end > viewEnd - margin) delta = end - viewEnd + margin;
        if (delta == 0) return false;

        // Brought into view NOW, not glided to: the ring has to be on screen in the frame that draws
        // it, and the next Tab may arrive before a glide would have landed — the browser scrolls a
        // focused element into view the same way. A wheel still glides; only focus jumps. (Found by
        // the first gallery section tall enough to put a control below the fold: the glide's first
        // frame had not moved, and the walk read the ring off-screen.)
        var current = ScrollClock.OffsetOf(regions[best].Element) ?? regions[best].Fallback;
        _scrolls.ScrollTo(regions[best].Element, current + delta, regions[best].MaxOffset);
        NeedsRender = true;
        return true;
    }

    private const float ScrollIntoViewMargin = 24f;

    /// <summary>
    /// A stop in a table viewport's cell is brought into view by the table's own scroll, which the
    /// scroll views' search never moves: its cells past the window are built, and Tab reaches them. The
    /// cell is the element right under the viewport's own, and its band says which axes it moves along,
    /// so a pinned header is never scrolled after. Answers whether the table moved.
    /// </summary>
    private bool RevealInTable(FocusStop stop)
    {
        var tables = _lastFrame!.TableRegions;
        for (var i = tables.Count - 1; i >= 0; i--)
        {
            var table = tables[i];
            TableViewCell? cell = null;
            var at = stop.Element;
            for (var parent = at.Parent; parent is not null; at = parent, parent = parent.Parent)
            {
                if (!ReferenceEquals(parent, table.Element)) continue;
                cell = at.Configuration as TableViewCell;
                break;
            }
            if (cell is null || cell.Mark) continue;
            var viewport = table.Viewport;
            var area = new Rect(stop.Bounds.X - table.Bounds.X, stop.Bounds.Y - table.Bounds.Y,
                stop.Bounds.Width, stop.Bounds.Height);
            if (!viewport.Scroll.Reveal(area, cell.Column >= viewport.PinnedColumns, cell.Row >= viewport.PinnedRows))
                return false;
            NeedsRender = true;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Brings each code surface's primary caret into view when its model says it moved — the Photon
    /// half of what the browser does with <c>scrollIntoView</c>, and the reason arrowing past the edge
    /// of a code viewport no longer leaves you typing somewhere you cannot see. A surface seen for the
    /// first time is left where the layout put it: nothing moved it yet. Answers whether anything
    /// scrolled. The version a surface was last brought into view at is kept on its position
    /// (<see cref="CodeRevealState"/>), so a surface drawn at another's place starts unseen.
    /// </summary>
    private bool RevealCodeCarets()
    {
        if (_lastFrame is null) return false;
        var scrolled = false;
        var regions = _lastFrame.CodeRegions;
        for (var i = 0; i < regions.Count; i++)
        {
            var region = regions[i];
            var version = region.Surface.Model.RevealVersion;
            if (NodeStates.For<CodeRevealState>(region.Element) is not { } revealed) continue;
            var known = revealed.Known;
            var seen = revealed.Version;
            revealed.Known = true;
            revealed.Version = version;
            if (!known || seen == version) continue;

            var carets = region.Surface.Model.Carets;
            if (carets.Count == 0) continue;
            var caret = carets[0];
            var (laid, toScreen) = Laid(regions, i, region.Bounds, caret);
            scrolled |= RevealRect(region.Element, laid, toScreen);
        }
        return scrolled;
    }

    /// <summary>
    /// Brings what is being edited into view: a code surface by its caret when it is on screen to
    /// have one, and otherwise the field or surface whole, by its focus stop, which a frame keeps
    /// where its viewport no longer reaches (a field the keyboard just covered has no region left to
    /// find it by). Answers whether anything scrolled.
    /// </summary>
    private bool RevealEditing()
    {
        if (_lastFrame is null || _textAt is not { } editing) return false;
        var surfaces = _lastFrame.CodeRegions;
        for (var i = 0; i < surfaces.Count; i++)
        {
            if (!ReferenceEquals(surfaces[i].Element, editing)) continue;
            var carets = surfaces[i].Surface.Model.Carets;
            if (carets.Count == 0) break;
            var (laid, toScreen) = Laid(surfaces, i, surfaces[i].Bounds, carets[0]);
            return RevealRect(editing, laid, toScreen);
        }
        var stops = _lastFrame.FocusStops;
        for (var i = 0; i < stops.Count; i++)
            if (stops[i].Id == editing.Id) return ScrollIntoView(stops[i]);
        return false;
    }

    /// <summary>
    /// Sets <see cref="_lift"/>: for a field being edited that ends below <paramref name="laidHeight"/>,
    /// where the keyboard took the room it needed and no scroll view can bring it up, the least that
    /// brings its bottom above it with a control's worth of margin, and never so much that its top
    /// leaves the screen. Read from the field's focus stop, which the frame keeps even where the
    /// field is drawn under the keyboard, and which is laid out where it was before the lift, so the
    /// lift a covered field takes holds as long as it is edited. Answers whether the lift changed.
    /// </summary>
    private bool Lift(float laidHeight)
    {
        var lift = 0f;
        var squeezed = _viewInsets.Bottom > 0 || laidHeight < _roomBeforeEdit;
        if (squeezed && _textAt is { } editing && _lastFrame is { } frame && !InVerticalScroll(frame, editing))
        {
            var stops = frame.FocusStops;
            for (var i = 0; i < stops.Count; i++)
            {
                if (stops[i].Id != editing.Id) continue;
                var field = stops[i].Bounds;
                if (field.Bottom > laidHeight)
                    lift = Math.Clamp(field.Bottom + ScrollIntoViewMargin - laidHeight, 0,
                        MathF.Max(0, field.Top - ScrollIntoViewMargin));
                break;
            }
        }
        if (lift == _lift) return false;
        _lift = lift;
        return true;
    }

    /// <summary>Whether a scroll view that moves up and down, with somewhere to move, holds
    /// <paramref name="at"/>: what it holds is brought into view by scrolling it.</summary>
    private static bool InVerticalScroll(RealizeResult frame, Element at)
    {
        var regions = frame.ScrollRegions;
        for (var i = 0; i < regions.Count; i++)
            if (regions[i].Axis != ScrollAxis.Horizontal && regions[i].MaxOffset > 0
                && IsUnder(at, regions[i].Element))
                return true;
        return false;
    }

    /// <summary>
    /// Scrolls every scroll region that holds <paramref name="at"/>, each on its own axis, the
    /// least that brings <paramref name="rect"/> inside it — a code viewport scrolls down AND across
    /// for the same caret, which is why this is not <see cref="ScrollIntoView"/>'s innermost-only
    /// rule. A small margin keeps the caret off the very edge, so the line after it is seen coming.
    /// </summary>
    private bool RevealRect(Element at, Rect rect, Matrix2D? toScreen)
    {
        var regions = _lastFrame!.ScrollRegions;
        var scrolled = false;
        for (var i = 0; i < regions.Count; i++)
        {
            var region = regions[i];
            if (region.MaxOffset <= 0 || !IsUnder(at, region.Element)) continue;

            var (own, viewport) = InScrollSpace(regions, i, rect, toScreen);
            var horizontal = region.Axis == ScrollAxis.Horizontal;
            var (start, end, viewStart, viewEnd) = horizontal
                ? (own.X, own.X + own.Width, viewport.X, viewport.X + viewport.Width)
                : (own.Y, own.Y + own.Height, viewport.Y, viewport.Y + viewport.Height);

            var margin = MathF.Min(horizontal ? own.Height : own.Height / 2, (viewEnd - viewStart) / 4);
            var delta = 0f;
            if (start < viewStart + margin) delta = start - viewStart - margin;
            else if (end > viewEnd - margin) delta = end - viewEnd + margin;
            if (delta == 0) continue;

            var current = ScrollClock.OffsetOf(region.Element) ?? region.Fallback;
            _scrolls.ScrollTo(region.Element, current + delta, region.MaxOffset);
            scrolled = true;
        }
        return scrolled;
    }

    /// <summary>
    /// <paramref name="rect"/> and the viewport of the <paramref name="index"/>th scroll region, both
    /// in the space the region was laid out in. <paramref name="toScreen"/> takes
    /// <paramref name="rect"/> to the screen when a transform drew what it measures, and null says it
    /// is on screen already. A scroll offset is a distance in the scroll view's own space: measured on
    /// screen, a scroll view drawn twice as large scrolled a focused control or a caret twice as far as
    /// it had to (#700). The rect crosses by its own corners, never as the box around them on screen:
    /// under a turn, that box taken back through the inverse grew a control at the foot of the view
    /// into one reaching above its top, and the view scrolled the wrong way.
    /// </summary>
    private (Rect Rect, Rect Viewport) InScrollSpace(IReadOnlyList<ScrollRegion> regions, int index, Rect rect,
        Matrix2D? toScreen)
    {
        var view = TransformedAt(regions, index);
        if (view is null && toScreen is null) return (rect, regions[index].Bounds);
        var carry = toScreen ?? Matrix2D.Identity;
        if (view is { } turned) carry *= turned.Inverse;
        return (carry.TransformBounds(rect), view?.Local ?? regions[index].Bounds);
    }

    /// <summary>A focus stop's box as it was laid out, and what takes it to the screen when a
    /// transform drew it: the box on screen and nothing otherwise.</summary>
    private (Rect Rect, Matrix2D? ToScreen) Laid(FocusStop stop)
    {
        var stops = _lastFrame!.FocusStops;
        for (var i = 0; i < stops.Count; i++)
        {
            if (stops[i].Id != stop.Id) continue;
            if (TransformedAt(stops, i) is { } turned && turned.Inverse.Invert() is { } forward)
                return (turned.Local, forward);
            break;
        }
        return (stop.Bounds, null);
    }

    /// <summary>Whether the keyboard focus (a field or code being edited, or the control wearing the
    /// ring) is inside the subtree at <paramref name="scope"/>: a focus-scoped shortcut's question.
    /// Web twin: <c>focusWithin</c> in the lowering, over the focused element.</summary>
    private bool FocusIsWithin(Element scope) =>
        (_textAt ?? _focusedAt) is { } focused && (ReferenceEquals(focused, scope) || IsUnder(focused, scope));

    /// <summary>
    /// Whether <paramref name="at"/> lies below <paramref name="above"/> in the tree, inside its box:
    /// walked up the parents, and never across a LAYER, an overlay's child or an anchored panel, which
    /// the realizer lays out on its own over the page and so inside none of the boxes above it. A
    /// path's prefix answered the same question before #976, a layer's path starting a tree of its own.
    /// </summary>
    private static bool IsUnder(Element at, Element above)
    {
        var child = at;
        for (var parent = at.Parent; parent is not null; child = parent, parent = parent.Parent)
        {
            if (parent is NodeElement node && node.HoldsDeferred(child)) return false;
            if (ReferenceEquals(parent, above)) return true;
        }
        return false;
    }

    private bool IsFocused(HitRegion region) =>
        _focusedId != 0
            ? region.Element.Id == _focusedId
            : _focused is not null && ReferenceEquals(region.Node, _focused);

    /// <summary>Puts the focus on a stop's id and its position.</summary>
    private void FocusOn(FocusStop stop)
    {
        _focusedId = stop.Id;
        _focusedAt = stop.Element;
    }

    /// <summary>Takes the focus off whatever id held it.</summary>
    private void ForgetFocus()
    {
        _focusedId = 0;
        _focusedAt = null;
    }

    /// <summary>Gives focus to a specific region — what a pointer press does, so that releasing over
    /// a control and then pressing Enter goes on working on the control the user just touched.
    /// <paramref name="at"/> is the control's position (a <see cref="HitRegion.Element"/>), which the
    /// focus follows across rebuilds; without one it holds the node, for a frame that never rebuilds.</summary>
    public void Focus(Pressable node, Element? at = null, bool visible = true)
    {
        _focused = node;
        _focusedId = at?.Id ?? 0;
        _focusedAt = at;
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

    /// <summary>The position of the field, code surface or spreadsheet being edited, or null.</summary>
    private Element? _textAt;
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
            // A caret the host moved (a key, a press, an edit the keyboard did not make) has left the
            // word a phone keyboard was composing; the keyboard's own edit sets it again after this.
            _composing = null;
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
            var length = EditedValue?.Length ?? 0;
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
        var style = entry.Resolve(_theme).AtWindow(this.Width);

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
    private static string Copyable(TextEntry entry, string value, int start, int end) =>
        entry.Obscure ? "" : value[start..end];

    /// <summary>What is actually DRAWN for a value — a password shows dots, and a click has to land
    /// where the dots are.</summary>
    private static string Shown(TextEntry entry, string value) =>
        entry.Obscure ? new string('•', value.Length) : value;

    /// <summary>The field being edited in the CURRENT frame, or null. Resolved by its position every
    /// time rather than remembered: the node handed out last frame is already stale.
    /// <para>
    /// A field scrolled out of view is still the one being edited. Its region is clipped away, so
    /// nobody can tap what they cannot see, but its focus stop is registered wherever it is and
    /// still names it. A phone's keyboard rising over the field is that case in every form: the
    /// frame that lays out above the keyboard clips the field before the reveal scrolls it back,
    /// and reading it as no field there would turn the keyboard into a key pad for that frame and
    /// drop what was typed into it.
    /// </para></summary>
    public TextEntry? TextTarget
    {
        get
        {
            if (_textAt is null || _lastFrame is null) return null;
            var fields = _lastFrame.TextRegions;
            for (var i = 0; i < fields.Count; i++)
                if (ReferenceEquals(fields[i].Element, _textAt)) return fields[i].Entry;
            var stops = _lastFrame.FocusStops;
            for (var i = 0; i < stops.Count; i++)
                if (ReferenceEquals(stops[i].Element, _textAt) && stops[i].Entry is { } entry) return entry;
            return null;
        }
    }

    /// <summary>
    /// The CODE surface being edited in the current frame, or null. Resolved by position every frame for
    /// the same reason the text field is: the node handed out last frame is already a corpse, but
    /// the CONTROLLER it points at is the component's own and outlives every rebuild.
    /// </summary>
    public CodeSurface? CodeTarget
    {
        get
        {
            if (_textAt is null || _lastFrame is null) return null;
            var regions = _lastFrame.CodeRegions;
            for (var i = 0; i < regions.Count; i++)
                if (ReferenceEquals(regions[i].Element, _textAt)) return regions[i].Surface;
            return null;
        }
    }

    /// <summary>A press that began in a code surface is drawing a selection until it lifts.</summary>
    private bool _codeDragging;

    /// <summary>The code surface the pointer is passing over with nothing pressed, which hears that it
    /// left when the pointer moves off it (<see cref="HoverCode"/>).</summary>
    private Element? _hoveredCode;

    /// <summary>A press is held, wherever it began: the pointer moving then is dragging, and rests over
    /// no code (the web twin reads the same from a move's <c>buttons</c>).</summary>
    private bool _held;

    /// <summary>The SPREADSHEET surface being edited, resolved by position every frame like the code
    /// surface: the node is a corpse, the MODEL outlives every rebuild.</summary>
    public SheetSurface? SheetTarget
    {
        get
        {
            if (_textAt is null || _lastFrame is null) return null;
            var regions = _lastFrame.SheetRegions;
            for (var i = 0; i < regions.Count; i++)
                if (ReferenceEquals(regions[i].Element, _textAt)) return regions[i].Surface;
            return null;
        }
    }

    /// <summary>
    /// Recycle each frame's LayoutNodes into the next (the frame's dominant allocation). OPT-IN:
    /// when on, a replaced RealizeResult's tree is rewritten by later frames — production shells
    /// turn this on because nothing retains their frames; tests and tooling that hold results
    /// across frames leave it off and every tree stays immutable.
    /// </summary>
    public bool RecycleFrames { get; init; }

    private readonly LayoutNodePool _nodePool = new();

    /// <summary>A press that began in a sheet belongs to it until it lifts: its moves and its release
    /// go to that sheet's model, which knows whether the press is growing the selection or dragging
    /// a fill, as a code surface's does.</summary>
    private bool _sheetPressed;

    private void BeginCodeEditing(CodeRegion region)
    {
        var changed = !ReferenceEquals(_textAt, region.Element);
        if (changed) EndEditing();
        _textAt = region.Element;
        _focused = null;
        ForgetFocus();
        if (changed) region.Surface.Model.FocusChanged(true);
        NeedsRender = true;
    }

    /// <summary>Restarts the blink — anything that moved the caret, including a code command.</summary>
    private void RestartBlink() => _caretPhaseMs = _lastTimeMs;

    /// <summary>Where the caret sits, clamped to the text that is actually there — the app may have
    /// replaced the value with something shorter (a formatter, a reset) between keystrokes.</summary>
    public int CaretIndex => Math.Clamp(_caret, 0, EditedValue?.Length ?? 0);

    /// <summary>
    /// The value last handed to the field's <c>OnChanged</c> and not yet drawn, or null. A keyboard
    /// edits twice between two frames (an autocorrection deletes the word and types its fix, a
    /// keyboard batches its edits), and the second edit has to start from what the first one made:
    /// read off the frame, it started from the value before both and undid the first.
    /// </summary>
    private string? _edited;

    /// <summary>The focused field's value as the next edit has to see it: the last one typed, until
    /// a frame draws what the app made of it.</summary>
    private string? EditedValue => _edited ?? TextTarget?.Value;

    /// <summary>A phone keyboard's composition, as a range of the value (see
    /// <see cref="TextEditingValue"/>), or null. Drawn underlined, like the desktop's marked text.
    /// It carries the text it marks a range OF: an app that rewrites the value (a formatter, an
    /// upper-caser) ends it, as setting a controller's text does in Flutter, because the keyboard is
    /// then told a text it never composed in.</summary>
    private (int Start, int End, string Text)? _composing;

    /// <summary>
    /// Half-second on, half-second off, off the frame clock — the rate every desktop uses. A caret
    /// that does not blink reads as a rendering artifact rather than a place to type.
    /// <para>
    /// The phase is measured from the last caret MOVE, not from the epoch, so a caret that just
    /// arrived somewhere is solid the instant it gets there. While a drag is drawing a selection it
    /// does not blink at all: the end following your pointer is the one thing you are watching.
    /// </para>
    /// </summary>
    public bool CaretVisible => _textAt is null || _dragSelecting || _codeDragging
        || (int)((_lastTimeMs - _caretPhaseMs) / CaretBlinkMs) % 2 == 0;

    /// <summary>The normative phase — shared with the web mirror (CSS animates twice this).</summary>
    private const float CaretBlinkMs = CodeSurface.CaretBlinkMs;

    /// <summary>
    /// A field or a code surface that asked for the keyboard gets it when it APPEARS: the search box
    /// of a palette that just opened, the first field of a form, an editor opened ready to type. The
    /// web realization focuses the same tree on mount; native ignored it, so the ⌘K panel opened
    /// ready to type in a browser and dead in a window.
    /// <para>
    /// Once per MOUNT, the browser's rule. A field honoured while it stays on screen is not honoured
    /// again, or leaving it with Escape would hand it straight back on the next frame and the field
    /// could never be left. One that leaves the tree and comes back asks again, as the code editor's
    /// find field does every time the bar opens. It was once per PATH for the life of the host, so a
    /// bar opened a second time came up with no caret.
    /// </para>
    /// <para>
    /// It takes the keyboard from whatever held it, as a mounted field does in a browser: it appeared
    /// because someone opened it. It used to be refused while anything else was being typed in, and
    /// ⌘F in the code editor opened a find bar that typing never reached. When several appear in one
    /// frame the first field wins, then the first code surface.
    /// </para>
    /// </summary>
    private void AdoptAutofocus()
    {
        if (_lastFrame is null) return;
        _fieldsNow.Clear();
        TextRegion? newField = null;
        var fields = _lastFrame.TextRegions;
        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            if (!field.Entry.Autofocus || field.Entry.Disabled) continue;
            _fieldsNow.Add(field.Element);
            if (newField is null && !_fieldsShown.Contains(field.Element)) newField = field;
        }
        _surfacesNow.Clear();
        CodeRegion? newSurface = null;
        var surfaces = _lastFrame.CodeRegions;
        for (var i = 0; i < surfaces.Count; i++)
        {
            var surface = surfaces[i];
            if (!surface.Surface.Autofocus) continue;
            _surfacesNow.Add(surface.Element);
            if (newSurface is null && !_surfacesShown.Contains(surface.Element)) newSurface = surface;
        }
        (_fieldsShown, _fieldsNow) = (_fieldsNow, _fieldsShown);
        (_surfacesShown, _surfacesNow) = (_surfacesNow, _surfacesShown);

        if (newField is { } asked) BeginEditing(asked);
        else if (newSurface is { } code) BeginCodeEditing(code);
    }

    /// <summary>The positions of the fields asking for the keyboard in the last frame, and the set the
    /// next frame fills: two sets swapped, so a steady frame allocates nothing. A field that takes
    /// another's place has a position of its own, so it is a new mount and asks again. The surfaces
    /// keep their own pair, as they did when a path stood for both before #976.</summary>
    private HashSet<Element> _fieldsShown = [];
    private HashSet<Element> _fieldsNow = [];
    private HashSet<Element> _surfacesShown = [];
    private HashSet<Element> _surfacesNow = [];

    /// <summary>
    /// Gives a code surface the keyboard when its model ASKS (<see cref="ICodeSurfaceModel.FocusVersion"/>):
    /// an IDE after a file opens, the editor's own find bar as it closes. Remembered per MODEL and
    /// counted from 0, so a request made before the surface was first drawn is honoured when it is,
    /// and one already honoured is not honoured again when the surface is drawn somewhere else.
    /// </summary>
    private void AdoptFocusRequests()
    {
        if (_lastFrame is null) return;
        var surfaces = _lastFrame.CodeRegions;
        for (var i = 0; i < surfaces.Count; i++)
        {
            var region = surfaces[i];
            var model = region.Surface.Model;
            var version = model.FocusVersion;
            var seen = _focusRequests.TryGetValue(model, out var box) ? box.Value : 0;
            if (version == seen) continue;
            if (box is null) _focusRequests.Add(model, new System.Runtime.CompilerServices.StrongBox<int>(version));
            else box.Value = version;
            BeginCodeEditing(region);
        }
        // A sheet asks the same way, when the formula bar or the name box is done with the keyboard.
        var sheets = _lastFrame.SheetRegions;
        for (var i = 0; i < sheets.Count; i++)
        {
            var region = sheets[i];
            var model = region.Surface.Model;
            var version = model.FocusVersion;
            var seen = _sheetFocusRequests.TryGetValue(model, out var box) ? box.Value : 0;
            if (version == seen) continue;
            if (box is null) _sheetFocusRequests.Add(model, new System.Runtime.CompilerServices.StrongBox<int>(version));
            else box.Value = version;
            var changed = !ReferenceEquals(_textAt, region.Element);
            if (changed) EndEditing();
            _textAt = region.Element;
            _focused = null;
            ForgetFocus();
            if (changed) model.FocusChanged(true);
            RestartBlink();
            NeedsRender = true;
        }
    }

    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<ICodeSurfaceModel,
        System.Runtime.CompilerServices.StrongBox<int>> _focusRequests = new();

    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<ISheetSurfaceModel,
        System.Runtime.CompilerServices.StrongBox<int>> _sheetFocusRequests = new();

    private void BeginEditing(TextRegion field, float? localX = null) =>
        BeginEditing(field.Entry, field.Element, localX is { } x ? IndexAt(field.Entry, x) : null);

    private void BeginEditing(TextEntry entry, Element at, int? caret = null)
    {
        var field = new TextRegion(default, entry, at);
        if (field.Entry.Disabled) return;
        var changed = !ReferenceEquals(_textAt, field.Element);
        // Tell the field being LEFT that it lost focus before telling the next one it gained it.
        // Skipping this left every field the user had passed through still wearing its focus ring —
        // a form where four boxes all look like the active one.
        if (changed) EndEditing();
        _textAt = field.Element;
        if (changed) _edited = null;
        // Where the pointer fell, or the end of the text when it arrived by Tab.
        Caret = caret ?? field.Entry.Value.Length;
        _anchor = caret is null ? null : _caret;
        _focused = null;
        ForgetFocus();
        if (changed) field.Entry.OnFocusChanged?.Invoke(true);
        NeedsRender = true;
    }

    /// <summary>
    /// Ends the edit: the field or surface that held the caret loses it, and with it any
    /// composition. Escape and back do this from inside; a shell does it when the platform took the
    /// keyboard away itself (an iPad's hide-keyboard key, another app's window).
    /// </summary>
    public void EndEditing()
    {
        if (_textAt is null) return;
        TextTarget?.OnFocusChanged?.Invoke(false);
        // A code surface hears it too: the typing run ends, and a composition the platform just
        // dropped must not stay underlined in the document.
        if (CodeTarget is { } code)
        {
            code.Model.FocusChanged(false);
            SurfaceChanged(code.OnChanged);
        }
        // …and a sheet: its entry stays open, as Excel keeps it, and its composition goes.
        if (SheetTarget is { } sheet)
        {
            sheet.Model.FocusChanged(false);
            SurfaceChanged(sheet.OnChanged);
        }
        _textAt = null;
        _marked = "";
        _edited = null;
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
        // A focused SHEET types Excel's way: the first character REPLACES the cell (a fresh entry
        // takes it); the rest go in at the entry's caret. The document only changes on commit.
        if (SheetTarget is { } sheetTarget)
        {
            if (!sheetTarget.Model.HandleText(text)) return false;
            RestartBlink();
            SurfaceChanged(sheetTarget.OnChanged);
            return true;
        }
        // A code surface types through its model, which is where auto-closing pairs, the
        // "step over the closer" rule and undo coalescing all live.
        if (CodeTarget is { } surface)
        {
            if (!surface.Model.HandleText(text)) return false;
            RestartBlink();
            SurfaceChanged(surface.OnChanged);
            return true;
        }
        if (TextTarget is not { } entry || entry.Disabled) return false;
        // Typing over a selection REPLACES it — the one behaviour that makes selecting worth doing.
        var (start, end) = Selection;
        var value = _edited ?? entry.Value;
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
    public bool HasTextFocus => _textAt is not null;

    /// <summary>The last realized frame — the shells' read-only window onto regions and semantics
    /// (probes, bridges). Null before the first render.</summary>
    public RealizeResult? LastFrame => _lastFrame;

    /// <summary>The composition in flight for the focused field ("" when none).</summary>
    public string MarkedText => _textAt is null ? "" : _marked;

    public bool HasMarkedText => MarkedText.Length > 0;

    /// <summary>
    /// Replaces the composition in flight ("" cancels it). Composing over a selection replaces the
    /// selection first — the same rule typing has — and that edit is real: cancelling the
    /// composition afterwards does not resurrect what typing over it would also have destroyed.
    /// </summary>
    public bool SetMarkedText(string text)
    {
        if (_textAt is null) return false;
        // A code surface composes IN its document: the model shows the marked text where it will
        // land, underlined, and a cancellation takes it back out. The host still tracks it, because
        // the platform's input client asks the host whether anything is marked.
        if (CodeTarget is { } code)
        {
            // A composition the model refuses (a read-only editor) is not one the host may claim:
            // the platform asks the HOST whether anything is marked. A cancellation still clears it.
            if (!code.Model.SetComposition(text) && text.Length > 0) return false;
            SurfaceChanged(code.OnChanged);
        }
        // A sheet composes in its entry the same way, and a composition at rest begins one, as typing
        // does: the cell's editor shows the marked text where it will land.
        if (SheetTarget is { } sheet)
        {
            if (!sheet.Model.SetComposition(text) && text.Length > 0) return false;
            SurfaceChanged(sheet.OnChanged);
        }
        if (TextTarget is { } entry && !entry.Disabled)
        {
            var (start, end) = Selection;
            var value = _edited ?? entry.Value;
            if (end > start)
                Commit(entry, value[..start] + value[end..], start, clearAnchor: true);
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

    // ---- A phone's keyboard ---------------------------------------------------------------------
    //
    // A phone keyboard edits a DOCUMENT, not a stream of keys: it reads the text around the caret,
    // replaces a word it autocorrected, marks the word it is suggesting for, moves the selection.
    // Android's input connection and iOS's text input both work that way, so both phone shells hand
    // the host the field's whole new state, Flutter's TextInputClient.updateEditingValue, and the
    // host turns it into the one edit the app hears about. A code surface and a sheet keep their text
    // to themselves, have no editing value, and take the keyboard's keys and committed text instead.

    /// <summary>
    /// The field being edited as a keyboard sees it: its value (the last one typed until a frame
    /// draws it), the selection and the composition. Null when no field holds the caret, which
    /// includes a code surface and a sheet: they take keys, not a document.
    /// </summary>
    public TextEditingValue? EditingValue
    {
        get
        {
            if (TextTarget is not { } entry) return null;
            var text = _edited ?? entry.Value;
            var caret = CaretIndex;
            var anchor = _anchor is { } held ? Math.Clamp(held, 0, text.Length) : caret;
            return _composing is { } composing && composing.Text == text
                ? new TextEditingValue(text, anchor, caret, composing.Start, composing.End)
                : new TextEditingValue(text, anchor, caret);
        }
    }

    /// <summary>
    /// The keyboard's edit, as the field's whole new state (Flutter's
    /// <c>TextInputClient.updateEditingValue</c>). A new text reaches the app through
    /// <see cref="TextEntry.OnChanged"/>, once, as typing does; the selection and the composition are
    /// the host's to keep. A single-line field flattens a line break, as a paste does. Answers false
    /// when no field holds the caret, or the one that does is disabled.
    /// </summary>
    public bool UpdateEditingValue(TextEditingValue value)
    {
        if (TextTarget is not { } entry || entry.Disabled) return false;
        value = value with { Text = value.Text ?? "" };
        if (entry.Lines <= 1 && value.Text.AsSpan().IndexOfAny('\r', '\n') >= 0) value = Flattened(value);
        var text = value.Text;
        var extent = Math.Clamp(value.SelectionExtent, 0, text.Length);
        var anchor = Math.Clamp(value.SelectionBase, 0, text.Length);

        if (text != (_edited ?? entry.Value)) Commit(entry, text, extent);
        Caret = extent;
        _anchor = anchor == extent ? null : anchor;
        // After the caret, whose every move drops it: this is the one move that keeps it.
        _composing = value.HasComposing ? (value.ComposingStart, value.ComposingEnd, text) : null;
        _marked = "";
        NeedsRender = true;
        return true;
    }

    /// <summary>
    /// A keyboard's state for a single line: each line break a space, as a paste makes it. A CRLF
    /// is two characters that become one, so an offset past one moves back with the text it is in.
    /// </summary>
    private static TextEditingValue Flattened(TextEditingValue value)
    {
        var text = value.Text;
        int At(int offset)
        {
            var shift = 0;
            for (var i = text.IndexOf("\r\n", StringComparison.Ordinal);
                 i >= 0 && i + 2 <= offset;
                 i = text.IndexOf("\r\n", i + 2, StringComparison.Ordinal))
                shift++;
            return offset - shift;
        }
        return new TextEditingValue(text.ReplaceLineEndings(" "), At(value.SelectionBase),
            At(value.SelectionExtent), At(value.ComposingStart), At(value.ComposingEnd));
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
    public Rect? CaretRect() => CaretInFrame() is { } caret ? caret with { Y = caret.Y - _lift } : null;

    /// <summary>The caret where the frame laid it, before <see cref="_lift"/>.</summary>
    private Rect? CaretInFrame()
    {
        if (_lastFrame is null || _textAt is null) return null;

        var fields = _lastFrame.TextRegions;
        for (var i = 0; i < fields.Count; i++)
        {
            if (!ReferenceEquals(fields[i].Element, _textAt)) continue;
            var bounds = fields[i].Bounds;
            var entry = fields[i].Entry;
            var style = entry.Resolve(_theme).AtWindow(this.Width);
            var caretInValue = Math.Min(CaretIndex, entry.Value.Length);
            var composed = _marked.Length > 0 ? entry.Value.Insert(caretInValue, _marked) : entry.Value;
            var upTo = Math.Min(caretInValue + _marked.Length, composed.Length);
            var advance = 0f;
            if (upTo > 0 && TextRasterizer is { } rasterizer)
            {
                var raster = (_textCache ?? TextRasterCache.Shared).Get(
                    rasterizer, composed[..upTo], style, _typeScale, float.MaxValue, 1, RenderScale,
                    TextAlignment.Start);   // a PREFIX's width: padding it would move the caret
                if (raster is not null) advance = raster.Texture.Width / RenderScale;
            }
            // The same window-follows-caret clamp the realizer applies while editing.
            // The field's own width: under a transform, its box on screen is another size.
            advance = MathF.Min(advance, (TransformedAt(fields, i)?.Local.Width ?? bounds.Width) - 8f);
            return ToScreen(fields, i, bounds, new Rect(MathF.Max(advance, 0), 0, 2f, style.LineHeight));
        }

        // A sheet's input stands at its entry's caret while a cell is edited and at the active cell before,
        // so the candidate window of a composition begun at rest opens at the cell it will type into.
        var sheets = _lastFrame.SheetRegions;
        for (var i = 0; i < sheets.Count; i++)
        {
            if (!ReferenceEquals(sheets[i].Element, _textAt)) continue;
            return ToScreen(sheets, i, sheets[i].Bounds, sheets[i].Surface.Model.InputBounds);
        }

        var surfaces = _lastFrame.CodeRegions;
        for (var i = 0; i < surfaces.Count; i++)
        {
            if (!ReferenceEquals(surfaces[i].Element, _textAt)) continue;
            var carets = surfaces[i].Surface.Model.Carets;
            if (carets.Count == 0) return null;
            return ToScreen(surfaces, i, surfaces[i].Bounds, carets[0]);
        }
        return null;
    }

    /// <summary>The editing keys — what Backspace and the arrows mean inside a field. Returns false
    /// for anything it does not claim, so Tab and Escape go on meaning what they mean everywhere.</summary>
    private bool EditKey(TextEntry entry, string key, KeyModifiers modifiers)
    {
        if (entry.Disabled) return false;
        var value = _edited ?? entry.Value;
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
                    if (start != end && Copyable(entry, value, start, end) is { Length: > 0 } copied)
                        Clipboard?.Write(copied);
                    return true;

                case 'x':
                    if (start == end) return true;
                    // An obscured field cuts by DELETING only: writing its dots' secret out is not
                    // its call, and writing "" would clear what the user already had there.
                    if (Copyable(entry, value, start, end) is { Length: > 0 } cut) Clipboard?.Write(cut);
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
                // Submit and STAY, as a browser's field does: a search field walks its results one
                // Enter at a time (the code editor's find bar is exactly that), and where the app
                // wants the keyboard somewhere else after a submit, it moves it. This left the field,
                // so the second Enter in a find bar went nowhere.
                entry.OnSubmit?.Invoke();
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
        if (entry.OnChanged is null) return;
        _edited = value;
        var marks = _owner.Marks;
        entry.OnChanged(value);
        RebuildUnlessTold(marks);
    }

    /// <summary>Clears keyboard focus (pointer interaction, escape).</summary>
    public void ClearFocus()
    {
        if (_focused is null && _focusedId == 0) return;
        _focused = null;
        ForgetFocus();
        NeedsRender = true;
    }

    /// <summary>
    /// Excel's keyboard over the focused sheet, through its model: the web surface hands keys to the
    /// same one, so the two cannot drift, and this host's clipboard goes with them so the copy keys
    /// are served through it. Answers false for keys the sheet does not claim. The keyboard stays on the
    /// sheet for an Escape it does not claim: the model released Tab, and the Tab that follows moves on
    /// from the sheet's own stop, which a dropped keyboard had forgotten (#732).
    /// </summary>
    private bool SheetKey(SheetSurface surface, string key, KeyModifiers modifiers)
    {
        if (!surface.Model.HandleKey(key, modifiers, KeyboardConvention, Clipboard)) return false;
        RestartBlink();
        SurfaceChanged(surface.OnChanged);
        return true;
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
        // The KEYBOARD's own door into an editing surface, and the same one a screen reader takes.
        // Tab ARRIVES at a code editor or a spreadsheet and leaves the keys with the page (it has
        // to: the editor would eat the next Tab as an indent), so Enter is how somebody with no
        // pointer says "in here". Without it the ring was as far as a keyboard could get — this
        // method's own doc says it runs the focused control "the way Enter and Space do everywhere
        // else", and for the two surfaces that own the keyboard it ran nothing.
        return _focusedId != 0 && Activate(_focusedId);
    }

    /// <summary>
    /// The frame's semantics tree, in reading order — what a platform accessibility bridge hands
    /// to VoiceOver/TalkBack. Derived from the last realized frame; empty before the first render.
    /// <para>
    /// A pure snapshot, deliberately: it answers "what is there", and it may be asked any number of
    /// times per frame. "What just CHANGED" is a different question with a different cadence, and
    /// it is answered by <see cref="TakeAnnouncements"/>.
    /// </para>
    /// </summary>
    public IReadOnlyList<SemanticNode> Semantics()
    {
        if (_lastFrame is null) return Array.Empty<SemanticNode>();
        var nodes = SemanticsTree.Collect(_lastFrame);
        if (_lift == 0) return nodes;
        // Read where it is drawn: a finger exploring the lifted screen finds what is under it.
        var lifted = new SemanticNode[nodes.Count];
        for (var i = 0; i < nodes.Count; i++)
            lifted[i] = nodes[i] with { Bounds = nodes[i].Bounds with { Y = nodes[i].Bounds.Y - _lift } };
        return lifted;
    }

    /// <summary>
    /// The live-region announcements produced since this was last called, and the queue is emptied.
    /// A shell drains it after each frame and posts each one the way its platform does —
    /// <c>NSAccessibilityPostNotification</c> on macOS, an announcement notification on UIKit, and
    /// nothing on Android, where setting <c>accessibilityLiveRegion</c> on the container IS the
    /// platform doing the posting.
    /// <para>
    /// DRAINING, not reading. An announcement is an event; a bridge that polled a list would post
    /// the same one every poll, which is the failure the whole diff exists to prevent, arriving
    /// from the other end.
    /// </para>
    /// </summary>
    public IReadOnlyList<LiveAnnouncement> TakeAnnouncements() => _announcer.Take();

    /// <summary>
    /// Runs the control named <paramref name="id"/> (a <see cref="SemanticNode.Id"/>), the way a
    /// screen reader's activate action does. Resolved out of THIS frame's regions — same reason as
    /// every press: the handler on a node from an old rebuild closes over dead state. Answers false
    /// when the id names nothing to do.
    /// <para>
    /// A pressable RUNS; anything else that takes focus is LANDED ON, which is what activating it
    /// means: double-tapping a field on TalkBack opens the keyboard in it, exactly as Tab does. Only
    /// the first half existed, so the Android bridge offered <c>ACTION_CLICK</c> over every text and
    /// code field — the table says those roles are activatable — and the tap did nothing at all.
    /// </para>
    /// </summary>
    public bool Activate(int id)
    {
        var regions = _lastFrame?.HitRegions;
        if (regions is null) return false;
        for (var i = 0; i < regions.Count; i++)
        {
            var region = regions[i];
            if (region.Element.Id != id || region.Node.Disabled) continue;
            region.Node.OnPressed?.Invoke();
            NeedsRender = true;
            return true;
        }

        // A LINK is FOLLOWED, never landed on — a reader's double tap, Enter and Space through
        // ActivateFocused, and a tap through ResolveLink are three routes to one seam. Resolved by
        // its REGION rather than by its focus stop, which is where the first version put it and got
        // two things wrong at once (#255, found by the review):
        //
        //   - a composite SUPPRESSES the stops inside it (an Adjustable, a Navigable), so a linked
        //     run inside a grid was announced as a link the reader could not follow — the very
        //     defect this closed, one level in. A region is not suppressed, which is exactly why a
        //     pressable cell inside a Navigable is activatable today;
        //   - the stop matched on a NON-EMPTY destination, so `Link("")` was landed on by the
        //     keyboard while the pointer navigated with the empty string. The region carries what
        //     it carries, and both routes now say the same thing.
        var links = _lastFrame?.LinkRegions;
        if (links is not null)
        {
            for (var i = 0; i < links.Count; i++)
            {
                if (links[i].Id != id) continue;
                if (_navigationRequested is null) return false;
                _navigationRequested(links[i].Destination);
                return true;
            }
        }

        var stops = _lastFrame?.FocusStops;
        if (stops is null) return false;
        for (var i = 0; i < stops.Count; i++)
        {
            // Everything else that takes focus is LANDED ON, and the condition names the two that
            // are answered ELSEWHERE rather than the kinds it happens to know: a pressable was
            // already answered above by the region carrying its handler, and an Adjustable is
            // stepped by Adjust, where activating does nothing on any platform. Listing the
            // kinds instead (an entry, a code surface) silently dropped the SheetSurface, which
            // announces as a CodeField and carries a stop of its own — the same shape of mistake as
            // the catch-all this file's NativeRole was written to end.
            if (stops[i].Id != id) continue;
            if (stops[i].Pressable is not null || stops[i].Adjustable is not null) continue;
            Land(stops[i]);
            // ARRIVING and ENTERING are the same thing for a text field and two different things
            // for a surface that owns the keyboard, which is the one place `Land` is not the whole
            // answer. Tab ARRIVES: the ring shows and the next Tab travels on. Activating ENTERS,
            // which is what a pointer press does (BeginCodeEditing) and what a reader's double tap
            // means — CodeTarget and SheetTarget both resolve from _textAt, so entering IS
            // setting it.
            //
            // Telling them apart was not a preference. Making the Tab walk enter put the caret in a
            // code editor, and the editor then ate the next Tab through CodeKeymap — the Studio's
            // own walk caught a ring that never came back round.
            //
            // Asked POSITIVELY, by what the stop IS. The first version asked by elimination — "not
            // a text field, so a surface" — and the Navigable stop that arrived one PR later
            // carried neither, so activating a calendar put itself in the field being edited, left every
            // target null and killed the arrows the composite exists for. A stop that is none of
            // these ARRIVES and nothing more, which is the right default for whatever comes next.
            if (stops[i] is { Code: not null } or { Sheet: not null })
            {
                _textAt = stops[i].Element;
                _focused = null;
                ForgetFocus();
                NeedsRender = true;
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// Assistive tech adjusts a slider-role element (VoiceOver's swipe up/down): +1 increments,
    /// −1 decrements — the same OnAdjust the keyboard's arrows drive, resolved by the element's
    /// <see cref="SemanticNode.Id"/>.
    /// </summary>
    public bool Adjust(int id, int direction)
    {
        var stops = _lastFrame?.FocusStops;
        if (stops is null) return false;
        for (var i = 0; i < stops.Count; i++)
        {
            if (stops[i].Id != id || stops[i].Adjustable is not { } adjustable) continue;
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
    public void SetHovered(VisualNode? node)
    {
        _hovered = node;
        _hoverAt.Clear(); // the test seam tracks by reference — one frame, one tree
        _hoverNodes.Clear();
    }

    /// <summary>
    /// Gestures v1 — pointer tracking: resolves the TOPMOST hover-reactive region under the pointer
    /// (paint order, last-contains wins) and re-renders when the target changes. Feed it from the
    /// platform shell's pointer-move events; it is a no-op until a frame has been rendered.
    /// A <see cref="PointerKind.Touch"/> move drives drags, pans and selections exactly the same —
    /// it only skips the hover resolution at the tail (spec S5: hover never fires on touch).
    /// </summary>
    public void PointerMove(float x, float y, PointerKind kind = PointerKind.Mouse)
    {
        y += _lift;
        // A canvas being dragged keeps the pointer even when it leaves the box; otherwise the
        // topmost canvas under the point gets the move, and the one being left gets its leave.
        var overCanvas = _pressedCanvas ?? CanvasUnder(new Point(x, y));
        if (!Equals(overCanvas, _hoveredCanvas))
        {
            // The frame is owed HERE, not only below: leaving a canvas for empty space announces
            // the leave and then falls past the `overCanvas is { }` branch, so a canvas that clears
            // a hover highlight in OnPointerLeave would have kept it on screen until something else
            // happened to need a frame. Every other pointer path here asks for one explicitly.
            if (_hoveredCanvas is not null) NeedsRender = true;
            _hoveredCanvas?.Node.OnPointerLeave?.Invoke();
            _hoveredCanvas = overCanvas;
        }
        if (overCanvas is { } active)
        {
            active.Node.OnPointerMove?.Invoke(
                Local(active, new Point(x, y), _pressedCanvas is not null, KeyModifiers.None));
            NeedsRender = true;
            if (_pressedCanvas is not null) return;   // a drag belongs to the canvas alone
        }

        // A press a table's scroll took, or a finger's candidate pan of a table: past the slop the table
        // takes it and the press under it is cancelled, as a scroll view's pan does.
        if (_tablePress is { } table)
        {
            if (!table.Active
                && MathF.Max(MathF.Abs(x - table.StartX), MathF.Abs(y - table.StartY)) > Touch.PressCancelSlop)
            {
                _pressed = null;
                _pressedAt = null;
                // A sheet's press under the pan is taken away, and told so: a finger's tap selects
                // nothing once the grid moved under it.
                CancelSheetPress(kind);
                table.Active = true;
                _tablePress = table;
            }
            if (table.Active)
            {
                table.Viewport.Scroll.Pointer(PointerPhase.Move, TableLocal(table.Viewport, table.Bounds, new Point(x, y)),
                    kind, _lastTimeMs);
                NeedsRender = true;
                return;
            }
        }

        // A press that began in a sheet: told to its model, which knows whether the press is growing
        // the selection or dragging a fill, and follows the pointer if it is.
        if (_sheetPressed && _lastFrame is not null)
        {
            var sheetRegions = _lastFrame.SheetRegions;
            for (var i = 0; i < sheetRegions.Count; i++)
            {
                if (!ReferenceEquals(sheetRegions[i].Element, _textAt)) continue;
                if (!sheetRegions[i].Surface.Model.HandlePointer(PointerPhase.Move,
                        ToLocal(sheetRegions, i, sheetRegions[i].Bounds, new Point(x, y)), KeyModifiers.None, 1, kind)) break;
                SurfaceChanged(sheetRegions[i].Surface.OnChanged);
                break;
            }
            return;
        }

        // The same, over a code surface — told to the model, which knows whether the press it saw
        // is drawing a selection and moves the focus with the pointer if it is.
        if (_codeDragging && _lastFrame is not null)
        {
            var surfaces = _lastFrame.CodeRegions;
            for (var i = 0; i < surfaces.Count; i++)
            {
                if (!ReferenceEquals(surfaces[i].Element, _textAt)) continue;
                if (!surfaces[i].Surface.Model.HandlePointer(PointerPhase.Move,
                        ToLocal(surfaces, i, surfaces[i].Bounds, new Point(x, y)), KeyModifiers.None, 1)) break;
                SurfaceChanged(surfaces[i].Surface.OnChanged);
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
                if (!ReferenceEquals(fields[i].Element, _textAt)) continue;
                var index = IndexAt(editing, ToLocal(fields, i, fields[i].Bounds, new Point(x, y)).X);
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
                _pressedAt = null;
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
                        _drags.Drag(drag.Element, draggable.Normalized
                            ? reported * drag.Extent
                            : reported);
                    }
                    draggable.OnMoved?.Invoke(reported);
                }
                else
                {
                    _drags.Drag(drag.Element, Math.Max(0, travel));
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
                _pressedAt = null;
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
                if (_scrolls.ScrollTo(pan.Element, pan.FromOffset - travelled, pan.MaxOffset))
                    NeedsRender = true;
                _pan = pan;
                return;
            }

            _pan = pan;
        }

        // S5: hover belongs to pointers that can rest over things. A finger cannot — it is
        // pressing or it is gone — and a pen the shells report only while it touches cannot either,
        // so their moves end HERE instead of latching whatever box they happened to cross last.
        if (kind != PointerKind.Mouse) return;
        if (!_held) HoverCode(new Point(x, y));

        // The CHAIN of regions under the pointer, topmost first — CSS :hover matches every
        // ancestor, so a button inside a hover-opened panel hovers BOTH (the web twin's answer).
        // Identity is the POSITION: instances are per-frame, and reference equality only answers
        // within the frame that made them (the press learned this first), while a box drawn at
        // another's place is not the box the pointer was over.
        var regions = _lastFrame?.HoverRegions;
        var chainAt = new List<Element>();
        var chainNodes = new List<VisualNode>();
        if (regions is not null)
        {
            for (var i = regions.Count - 1; i >= 0; i--)
            {
                if (!Covers(regions, i, regions[i].Bounds, new Point(x, y))) continue;
                chainAt.Add(regions[i].Element);
                chainNodes.Add(regions[i].Node);
            }
        }

        var sameChain = chainAt.Count == _hoverAt.Count;
        if (sameChain)
        {
            for (var i = 0; i < chainAt.Count; i++)
            {
                if (ReferenceEquals(chainAt[i], _hoverAt[i])) continue;
                sameChain = false;
                break;
            }
        }

        if (!sameChain)
        {
            // S5 programmable hover: Hoverable callbacks fire on MEMBERSHIP transitions — leave
            // BEFORE enter, the order every pointer model guarantees.
            for (var i = 0; i < _hoverNodes.Count; i++)
                if (_hoverNodes[i] is Hoverable left && !chainAt.Contains(_hoverAt[i]))
                    left.OnChanged(false);
            var previous = _hoverAt.ToList();
            _hoverAt.Clear(); _hoverAt.AddRange(chainAt);
            _hoverNodes.Clear(); _hoverNodes.AddRange(chainNodes);
            _hovered = chainNodes.Count > 0 ? chainNodes[0] : null;
            for (var i = 0; i < chainNodes.Count; i++)
                if (chainNodes[i] is Hoverable entered && !previous.Contains(chainAt[i]))
                    entered.OnChanged(true);
            NeedsRender = true;
        }
        else if (chainNodes.Count > 0)
        {
            // The pointer sat still through a rebuild: adopt the FRESH instances quietly, so the
            // eventual leave fires on nodes that are actually mounted — no churn, no repaint.
            _hoverNodes.Clear(); _hoverNodes.AddRange(chainNodes);
            _hovered = chainNodes[0];
        }
    }

    /// <summary>
    /// A wheel or a trackpad moved by <paramref name="across"/> and <paramref name="down"/> dp toward
    /// the content's end, over the point (<paramref name="x"/>, <paramref name="y"/>); a
    /// <paramref name="stepped"/> movement is a wheel's notch, which glides. The topmost table viewport
    /// under the point takes what its scroll can, and what it leaves goes to the scroll views around,
    /// each axis to the topmost one that moves along it: a horizontal view takes the horizontal
    /// movement, or the vertical one when there is none, as a mouse with one wheel expects. Returns true
    /// (and marks the frame dirty) when something moved or began to glide.
    /// </summary>
    public bool ScrollBy(float x, float y, float across, float down, bool stepped = true,
        KeyModifiers modifiers = KeyModifiers.None)
    {
        y += _lift;
        if (_lastFrame is null) return false;
        var point = new Point(x, y);
        var leftAcross = across;
        var leftDown = down;
        var moved = false;

        var tables = _lastFrame.TableRegions;
        for (var i = tables.Count - 1; i >= 0; i--)
        {
            if (!Covers(tables, i, tables[i].Visible ?? tables[i].Bounds, point)) continue;
            var left = tables[i].Viewport.Scroll.Wheel(leftAcross, leftDown, stepped, modifiers);
            if (left.Across != leftAcross || left.Down != leftDown) moved = true;
            leftAcross = (float)left.Across;
            leftDown = (float)left.Down;
            break;
        }

        var regions = _lastFrame.ScrollRegions;
        for (var i = regions.Count - 1; i >= 0 && (leftAcross != 0 || leftDown != 0); i--)
        {
            var region = regions[i];
            if (!Covers(regions, i, region.Bounds, point)) continue;
            var horizontal = region.Axis == ScrollAxis.Horizontal;
            var takesAcross = horizontal && leftAcross != 0;
            var delta = horizontal ? (takesAcross ? leftAcross : leftDown) : leftDown;
            if (delta == 0) continue;
            // A view that cannot move passes the movement on to the one around it.
            if (!_scrolls.ScrollBy(region.Element, delta, region.MaxOffset, region.Fallback)) continue;
            moved = true;
            if (takesAcross) leftAcross = 0;
            else leftDown = 0;
        }

        if (moved) NeedsRender = true;
        return moved;
    }

    /// <summary>Moves on every table viewport's glide or fling the last frame drew. Answers whether one
    /// is still moving, which keeps the frames coming.</summary>
    private bool AdvanceTables(float timeMs)
    {
        if (_lastFrame is null) return false;
        var moving = false;
        foreach (var table in _lastFrame.TableRegions)
            if (table.Viewport.Scroll.Advance(timeMs)) moving = true;
        return moving;
    }

    /// <summary>A point on screen in the own space of the table viewport a press began on, found in the
    /// last frame by the scroll it carries (a rebuild draws a new node over the same scroll), through
    /// the transform that drew it as the press itself was (<see cref="ToLocal"/>); the box the press
    /// began in when the last frame no longer draws it.</summary>
    private Point TableLocal(TableViewport viewport, Rect pressedIn, Point point)
    {
        var tables = _lastFrame?.TableRegions;
        if (tables is not null)
            for (var i = tables.Count - 1; i >= 0; i--)
                if (ReferenceEquals(tables[i].Viewport.Scroll, viewport.Scroll))
                    return ToLocal(tables, i, tables[i].Bounds, point);
        return new Point(point.X - pressedIn.X, point.Y - pressedIn.Y);
    }

    /// <summary>Whether a point in a table viewport's own space lands on one of its scrollbars.</summary>
    private static bool OnScrollbar(ITableScrollModel scroll, Point local) =>
        scroll.TrackOf(ScrollAxis.Vertical) is { } vertical && vertical.Contains(local)
        || scroll.TrackOf(ScrollAxis.Horizontal) is { } horizontal && horizontal.Contains(local);

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
        y += _lift;
        if (_lastFrame is null) return CursorShape.Default;
        var point = new Point(x, y);

        // Topmost first, exactly as dispatch does — a button drawn over a field is a button.
        // Explicit cursors (BoxStyle.Cursor) go first of all: a fill handle painted OVER a cell
        // says crosshair even though the cell underneath would say default.
        var cursors = _lastFrame.CursorRegions;
        for (var i = cursors.Count - 1; i >= 0; i--)
        {
            if (!Covers(cursors, i, cursors[i].Bounds, point)) continue;
            return ShapeOf(cursors[i].Cursor);
        }

        var fields = _lastFrame.TextRegions;
        var hits = _lastFrame.HitRegions;
        var links = _lastFrame.LinkRegions;

        // A disabled control says so: the pointer is the only warning before the click that does
        // nothing.
        if (HitAt(hits, point) is var hit and >= 0)
        {
            // A note painted over the control is what the pointer is over.
            if (CoveredByNote(hit, point, out _)) return CursorShape.Default;
            return hits[hit].Node.Disabled ? CursorShape.NotAllowed : CursorShape.Pointer;
        }
        // Over a sheet the sheet answers, as it answers a press there: a crosshair where a press would
        // grab the fill handle.
        var sheets = _lastFrame.SheetRegions;
        for (var i = sheets.Count - 1; i >= 0; i--)
            if (Covers(sheets, i, sheets[i].Visible ?? sheets[i].Bounds, point))
                return ShapeOf(sheets[i].Surface.Model.CursorAt(ToLocal(sheets, i, sheets[i].Bounds, point)));
        for (var i = fields.Count - 1; i >= 0; i--)
            if (Covers(fields, i, fields[i].Visible ?? fields[i].Bounds, point))
                return fields[i].Entry.Disabled ? CursorShape.NotAllowed : CursorShape.Text;
        // Code you place a caret in is a field too, and was the one kind this list forgot: the
        // pointer stayed an arrow over an editor, where every editor shows the beam. A read-only
        // editor still takes the caret and the selection, so it says the same.
        var code = _lastFrame.CodeRegions;
        for (var i = code.Count - 1; i >= 0; i--)
        {
            // Over what a surface offers, where no row is, the pointer is the list's and not the code's,
            // and over a note it is the note's.
            if (OnOffered(code, i, point) || OnNote(code, i, point)) return CursorShape.Default;
            if (Covers(code, i, code[i].Visible ?? code[i].Bounds, point)) return CursorShape.Text;
        }
        for (var i = links.Count - 1; i >= 0; i--)
            if (Covers(links, i, links[i].Bounds, point)) return CursorShape.Pointer;

        return CursorShape.Default;
    }

    private static CursorShape ShapeOf(PointerCursor cursor) => cursor switch
    {
        PointerCursor.Pointer => CursorShape.Pointer,
        PointerCursor.Text => CursorShape.Text,
        PointerCursor.NotAllowed => CursorShape.NotAllowed,
        PointerCursor.Crosshair => CursorShape.Crosshair,
        PointerCursor.ColResize => CursorShape.ColResize,
        PointerCursor.RowResize => CursorShape.RowResize,
        _ => CursorShape.Default,
    };

    /// <summary>Where the scroll view at <paramref name="at"/> currently sits, read off its position
    /// (a <see cref="ScrollRegion.Element"/>) — the test seam for gesture regressions (a latched pan
    /// shows up here as an offset nobody asked for).</summary>
    public float ScrollOffsetOf(Element at) => ScrollClock.OffsetOf(at) ?? 0f;

    /// <summary>The pointer left the window (or the input is touch) — hover clears.</summary>
    public void PointerLeave()
    {
        LeaveCode(except: null);
        if (_hoveredCanvas is { } leaving)
        {
            _hoveredCanvas = null;
            leaving.Node.OnPointerLeave?.Invoke();
            NeedsRender = true;
        }

        if (_hovered is null && _hoverNodes.Count == 0) return;
        foreach (var node in _hoverNodes)
            if (node is Hoverable left) left.OnChanged(false); // S5 programmable hover
        if (_hovered is Hoverable topmost && !_hoverNodes.Contains(topmost))
            topmost.OnChanged(false); // the SetHovered seam tracks by reference alone
        _hovered = null;
        _hoverAt.Clear();
        _hoverNodes.Clear();
        NeedsRender = true;
    }

    /// <summary>
    /// Begins a press: the topmost enabled hit region under the point becomes the pressed node and
    /// the next frame renders its pressed token swap. Returns whether a region captured the press.
    /// (v1 fence: drag-slop/cancel and fling join the gesture system.)
    /// </summary>
    public bool PressDown(float x, float y, int clickCount = 1, KeyModifiers modifiers = KeyModifiers.None,
        PointerKind kind = PointerKind.Mouse)
    {
        y += _lift;
        _held = true;
        if (_lastFrame is null) return false;
        var point = new Point(x, y);

        // W3: a canvas owns the pointer inside its box. It is asked FIRST because it draws its own
        // content and therefore knows its own hit-testing — the engine has no idea what is under
        // the point there, and a rectangle-shaped guess would be worse than asking.
        if (CanvasUnder(point) is { } pressed)
        {
            _pressedCanvas = pressed;
            pressed.Node.OnPointerDown?.Invoke(Local(pressed, point, pressedNow: true, modifiers));
            NeedsRender = true;
            return true;
        }

        // A table viewport's scroll: a press on its scrollbar is the scroll's from the start; a finger
        // on its cells is a candidate pan, and the press goes on to the cell (see _tablePress).
        _tablePress = null;
        var tables = _lastFrame.TableRegions;
        for (var i = tables.Count - 1; i >= 0; i--)
        {
            if (!Covers(tables, i, tables[i].Visible ?? tables[i].Bounds, point)) continue;
            var viewport = tables[i].Viewport;
            var local = ToLocal(tables, i, tables[i].Bounds, point);
            var onBar = OnScrollbar(viewport.Scroll, local);
            if (!viewport.Scroll.Pointer(PointerPhase.Down, local, kind, _lastTimeMs)) break;
            _tablePress = (viewport, tables[i].Bounds, x, y, onBar);
            if (!onBar) break;
            NeedsRender = true;
            return true;
        }

        // Gestures v2: arm the topmost drag surface under the point as a CANDIDATE — it only
        // becomes a drag once the pointer travels past the slop (taps inside keep working).
        _drag = null;
        var dragRegions = _lastFrame.DragRegions;
        for (var i = dragRegions.Count - 1; i >= 0; i--)
        {
            if (!Covers(dragRegions, i, dragRegions[i].Bounds, point)) continue;
            // The surface's own extent along the axis — what a NORMALIZED gesture divides by.
            var horizontalSurface = dragRegions[i].Node is Draggable { Axis: DragAxis.Horizontal };
            var extent = horizontalSurface ? dragRegions[i].Bounds.Width : dragRegions[i].Bounds.Height;
            _drag = (dragRegions[i].Element, dragRegions[i].Node, x, y, extent, false);
            break;
        }

        // …and the topmost SCROLLABLE surface, the same way. A drag surface is the more specific
        // gesture and keeps priority; where there is none, a finger scrolls.
        _pan = null;
        if (_drag is null && _tablePress is null)
        {
            var scrollRegions = _lastFrame.ScrollRegions;
            for (var i = scrollRegions.Count - 1; i >= 0; i--)
            {
                var region = scrollRegions[i];
                if (!Covers(scrollRegions, i, region.Bounds, point) || region.MaxOffset <= 0) continue;
                var along = region.Axis == ScrollAxis.Horizontal ? x : y;
                _pan = (region.Element, region.Axis, region.MaxOffset, along,
                    ScrollClock.OffsetOf(region.Element) ?? region.Fallback, along, _lastTimeMs, 0, false);
                break;
            }
        }

        var regions = _lastFrame.HitRegions;
        _pressSwallowed = false;
        Element? swallowedBy = null;
        // A note a code surface painted over the control under the point covers it: the press is the
        // note's, which moves no caret, as a press on a card over a button is the card's on the web.
        if (CoveredByNote(HitAt(regions, point), point, out _))
        {
            _pressSwallowed = true;
            return true;
        }
        // From the region the point lands on (HitAt), down: a slop above it that lost the point to a
        // box drawn beside it takes no part, inert or not.
        for (var i = HitAt(regions, point); i >= 0; i--)
        {
            var region = regions[i];
            if (!Covers(regions, i, region.Bounds, point)) continue;
            if (region.Node.Disabled || region.Node.OnPressed is null)
            {
                // INERT — disabled, or enabled with nothing to run (a Button used as a Menu's
                // trigger visual has no handler of its own). Inert swallows against what is BEHIND
                // it, but not against the pressable that WRAPS it: the Menu's own press was being
                // eaten by its topmost inert trigger. An ancestor (the inert one lies under its
                // position) is the owner of the gesture; a stranger underneath stays protected.
                swallowedBy ??= region.Element;
                continue;
            }
            if (swallowedBy is not null && !IsUnder(swallowedBy, region.Element))
            {
                _pressSwallowed = true;
                return true;
            }
            _pressed = region.Node;
            _pressedAt = region.Element;
            // Pressing a control takes focus off whatever had it — including a field being edited,
            // whose caret must not go on blinking somewhere the user is no longer looking. Unless
            // it may not take the keyboard (Pressable.CanRequestFocus): a row of the list a code
            // editor shows at its caret is pressed while the code keeps the keyboard.
            if (region.Node.CanRequestFocus)
            {
                EndEditing();
                // Focused, but not RINGED: pressing Enter after clicking a button goes on working,
                // and the click leaves no ring behind it.
                Focus(region.Node, region.Element, visible: false);
            }
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
            if (!Covers(fields, i, fields[i].Visible ?? fields[i].Bounds, point)) continue;
            BeginEditing(fields[i], ToLocal(fields, i, fields[i].Bounds, point).X);
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
            // A press on what the surface offers, that none of its rows took (between them, on the
            // documentation), is the list's: it moves no caret under it, and the code keeps the keyboard.
            // So is a press on a note, a card being read.
            if (OnOffered(surfaces, i, point) || OnNote(surfaces, i, point))
            {
                _pressSwallowed = true;
                return true;
            }
            if (!Covers(surfaces, i, surfaces[i].Visible ?? surfaces[i].Bounds, point)) continue;
            BeginCodeEditing(surfaces[i]);
            // What the press MEANS — a caret, a word, a line, a shift-extended selection — is the
            // model's to decide. The platform counts the clicks: its double-click interval is a
            // system setting, never ours to guess.
            surfaces[i].Surface.Model.HandlePointer(PointerPhase.Down, ToLocal(surfaces, i, surfaces[i].Bounds, point),
                modifiers, clickCount);
            _codeDragging = true;
            RestartBlink();
            SurfaceChanged(surfaces[i].Surface.OnChanged);
            return true;
        }

        // …and a SPREADSHEET surface, the same way: the model decides what the press means (the
        // cell, a stretch, an edit, the fill handle), and the press is the sheet's until it lifts.
        var sheetRegions = _lastFrame.SheetRegions;
        for (var i = sheetRegions.Count - 1; i >= 0; i--)
        {
            if (!Covers(sheetRegions, i, sheetRegions[i].Visible ?? sheetRegions[i].Bounds, point)) continue;
            var changed = !ReferenceEquals(_textAt, sheetRegions[i].Element);
            if (changed) EndEditing();
            _textAt = sheetRegions[i].Element;
            _focused = null;
            ForgetFocus();
            if (changed) sheetRegions[i].Surface.Model.FocusChanged(true);
            RestartBlink();
            sheetRegions[i].Surface.Model.HandlePointer(PointerPhase.Down,
                ToLocal(sheetRegions, i, sheetRegions[i].Bounds, point), modifiers, clickCount, kind);
            _sheetPressed = true;
            // A pen's press on the cells is the sheet's, as a mouse's is: its drag selects, so the
            // table's candidate pan lets go of it. A finger's stays a candidate, a tap or a pan.
            if (kind != PointerKind.Touch && _tablePress is { Active: false } candidate)
            {
                _tablePress = null;
                candidate.Viewport.Scroll.Pointer(PointerPhase.Cancel, Point.Zero, kind, _lastTimeMs);
            }
            SurfaceChanged(sheetRegions[i].Surface.OnChanged);
            return true;
        }

        // A press on empty space ends editing, which is how every form on every platform behaves —
        // and is the only way to leave a field without Tab.
        EndEditing();
        ClearFocus();
        return _drag is not null || _pan is not null;
    }

    /// <summary>A press a sheet owned is taken away, by the system or by a table's pan, and its model is
    /// told: a fill pours nothing, a drag stops where it stood, and a finger's tap selects nothing.</summary>
    private void CancelSheetPress(PointerKind kind)
    {
        if (!_sheetPressed) return;
        _sheetPressed = false;
        var sheetRegions = _lastFrame?.SheetRegions ?? Array.Empty<SheetRegion>();
        for (var i = 0; i < sheetRegions.Count; i++)
        {
            if (!ReferenceEquals(sheetRegions[i].Element, _textAt)) continue;
            if (sheetRegions[i].Surface.Model.HandlePointer(PointerPhase.Cancel, Point.Zero, KeyModifiers.None, 1, kind))
                SurfaceChanged(sheetRegions[i].Surface.OnChanged);
            break;
        }
    }

    /// <summary>
    /// The system took the gesture away without ever lifting the finger — UIKit's
    /// <c>touchesCancelled</c>, a window losing key, a call arriving. Nothing was decided, so
    /// nothing is reported: the press drops and the surface glides back to where the caller last
    /// put it. The web controller answers <c>pointercancel</c> the same way.
    /// </summary>
    public void PointerCancel(PointerKind kind = PointerKind.Mouse)
    {
        // The press is over, released or not: the pointer moving next rests on the code again.
        _held = false;
        // The input sequence died with the finger still down — the finger is gone, so nothing
        // rests over anything; a pen too. A mouse keeps its position through a cancel, and its hover with it.
        if (kind != PointerKind.Mouse) PointerLeave();

        _pan = null;
        if (_tablePress is { } table)
        {
            _tablePress = null;
            table.Viewport.Scroll.Pointer(PointerPhase.Cancel, Point.Zero, kind, _lastTimeMs);
        }

        // A press an editing surface owned is told it was taken away: a code surface's drag ends, and
        // a sheet's fill pours nothing.
        if (_codeDragging && _lastFrame is not null)
        {
            var codeRegions = _lastFrame.CodeRegions;
            for (var i = 0; i < codeRegions.Count; i++)
            {
                if (!ReferenceEquals(codeRegions[i].Element, _textAt)) continue;
                if (codeRegions[i].Surface.Model.HandlePointer(PointerPhase.Cancel, Point.Zero, KeyModifiers.None, 1))
                    SurfaceChanged(codeRegions[i].Surface.OnChanged);
                break;
            }
        }
        _codeDragging = false;
        CancelSheetPress(kind);

        if (_drag is { Active: true } drag)
        {
            var rest = drag.Node switch
            {
                Draggable { Follows: false } => 0f,
                Draggable { Normalized: true } d => d.RestOffset * drag.Extent,
                Draggable d => d.RestOffset,
                _ => 0f,
            };
            _drags.Release(drag.Element, _lastTimeMs, rest);
        }

        _drag = null;
        _pressed = null;
        // The press's position goes with it, or the paint walk, which asks by position, kept the
        // control drawn pressed after the system took the finger.
        _pressedAt = null;
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
    public bool PressUp(float x, float y, PointerKind kind = PointerKind.Mouse)
    {
        y += _lift;
        _held = false;
        // The canvas that took the press gets the release, wherever it happens — a drag that ends
        // outside the box still ends, which is what every gesture on every platform means by "up".
        if (_pressedCanvas is { } canvas)
        {
            _pressedCanvas = null;
            canvas.Node.OnPointerUp?.Invoke(Local(canvas, new Point(x, y), pressedNow: false, KeyModifiers.None));
            NeedsRender = true;
            return true;
        }

        // A finger that lifts is GONE — no pointer rests over anything, whatever set the hover
        // during the sequence, and a pen that lifts is the same. A mouse stays where it is, so its
        // hover survives the click (clearing it would flicker on every press).
        if (kind != PointerKind.Mouse) PointerLeave();

        // A table's press ends: one the table took lands there (a finger still moving flings); a
        // candidate that never left the slop was a tap, which goes on to the cell it began on.
        if (_tablePress is { } table)
        {
            _tablePress = null;
            var local = TableLocal(table.Viewport, table.Bounds, new Point(x, y));
            if (table.Active)
            {
                table.Viewport.Scroll.Pointer(PointerPhase.Up, local, kind, _lastTimeMs);
                _pan = null;
                NeedsRender = true;
                return true;
            }
            table.Viewport.Scroll.Pointer(PointerPhase.Cancel, local, kind, _lastTimeMs);
        }

        _dragSelecting = false;
        if (_codeDragging && _lastFrame is not null)
        {
            var codeRegions = _lastFrame.CodeRegions;
            for (var i = 0; i < codeRegions.Count; i++)
            {
                if (!ReferenceEquals(codeRegions[i].Element, _textAt)) continue;
                if (codeRegions[i].Surface.Model.HandlePointer(PointerPhase.Up,
                        ToLocal(codeRegions, i, codeRegions[i].Bounds, new Point(x, y)), KeyModifiers.None, 1))
                    SurfaceChanged(codeRegions[i].Surface.OnChanged);
                break;
            }
        }
        _codeDragging = false;
        if (_sheetPressed)
        {
            _sheetPressed = false;
            var sheetRegions = _lastFrame?.SheetRegions ?? Array.Empty<SheetRegion>();
            for (var i = 0; i < sheetRegions.Count; i++)
            {
                if (!ReferenceEquals(sheetRegions[i].Element, _textAt)) continue;
                // A fill drag lands as the press lifts, the source tiling across the preview as ONE
                // undo step, and the release is the sheet's. A selection drag simply ends, changing
                // nothing, and the release goes on like any other.
                if (!sheetRegions[i].Surface.Model.HandlePointer(PointerPhase.Up,
                        ToLocal(sheetRegions, i, sheetRegions[i].Bounds, new Point(x, y)), KeyModifiers.None, 1, kind)) break;
                SurfaceChanged(sheetRegions[i].Surface.OnChanged);
                return true;
            }
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
            _pressedAt = null;
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
                _drags.Release(drag.Element, _lastTimeMs,
                    draggable.Normalized ? draggable.RestOffset * drag.Extent : draggable.RestOffset);
                draggable.OnReleased?.Invoke(travel);
                NeedsRender = true;
                return true;
            }

            var dismiss = (DragDismiss)drag.Node;
            var dy = y - drag.StartY;
            if (dy >= DragDismiss.ThresholdDp)
            {
                _drags.Drop(drag.Element);
                dismiss.OnDismiss?.Invoke();
            }
            else
            {
                _drags.Release(drag.Element, _lastTimeMs);
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
            if (_scrolls.Fling(flick.Element, -flick.Velocity, flick.MaxOffset)) NeedsRender = true;
            return true;
        }
        _pan = null;

        var pressed = _pressed;
        var pressedAt = _pressedAt;
        if (pressed is null) return ResolveLink(x, y);
        _pressed = null;
        _pressedAt = null;
        NeedsRender = true;

        var point = new Point(x, y);
        var regions = _lastFrame?.HitRegions;
        if (regions is null) return false;
        for (var i = regions.Count - 1; i >= 0; i--)
        {
            var region = regions[i];
            // The SAME pressable, found by its position. Matching on object identity looked right
            // and failed for every press that outlived its frame — which is every real one, because
            // showing the pressed state repaints and the next Build hands back new nodes; matching
            // on its path ran whatever control had taken the place.
            if (!IsTheSamePressable(region, pressed, pressedAt)) continue;
            if (!Covers(regions, i, region.Bounds, point)) return false; // canceled by releasing outside
            region.Node.OnPressed?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>Whether a region is the one the press began on: the same POSITION, which holds the
    /// control for as long as it stands there, as the paint walk decides it
    /// (<see cref="PressScope.IsPressed"/>). The node alone answers only for a press the host tracks
    /// no position for: an app can keep one node and hand it to two pages, so a page replaced by the
    /// other shows the same node at a position nobody pressed.</summary>
    private static bool IsTheSamePressable(HitRegion region, Pressable pressed, Element? pressedAt) =>
        pressedAt is not null
            ? ReferenceEquals(region.Element, pressedAt)
            : ReferenceEquals(region.Node, pressed);

    /// <summary>A tap that no pressable claimed: the TOPMOST link region under the point navigates
    /// through the host's seam (a Pressable INSIDE a link wins — checked before this).</summary>
    private bool ResolveLink(float x, float y)
    {
        var regions = _lastFrame?.LinkRegions;
        if (regions is null || _navigationRequested is null) return false;
        var point = new Point(x, y);
        for (var i = regions.Count - 1; i >= 0; i--)
        {
            if (!Covers(regions, i, regions[i].Bounds, point)) continue;
            _navigationRequested(regions[i].Destination);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Dispatches a tap against the LAST rendered frame: the topmost containing hit region (paint
    /// order — last registered wins, matching Stack semantics) receives the press; disabled regions
    /// swallow the tap without firing (they still exist for accessibility). Returns whether any
    /// region was hit.
    /// </summary>
    /// <summary>The canvas being dragged — it keeps the pointer until the release, wherever that
    /// lands.</summary>
    private CanvasRegion? _pressedCanvas;

    /// <summary>The canvas the pointer is over, so leaving one can be announced exactly once.</summary>
    private CanvasRegion? _hoveredCanvas;

    /// <summary>The TOPMOST canvas under a point — paint order, last registered wins, the same rule
    /// every other region resolution here uses.</summary>
    private CanvasRegion? CanvasUnder(Point point)
    {
        if (_lastFrame is null) return null;
        var regions = _lastFrame.CanvasRegions;
        for (var i = regions.Count - 1; i >= 0; i--)
            if (Covers(regions, i, regions[i].Bounds, point)) return regions[i];
        return null;
    }

    /// <summary>The point in the CANVAS's coordinates — the translation that makes the arithmetic
    /// the app's, through the transform that draws the canvas when one does (#658). Outside the box
    /// the numbers go negative or past the size, which is correct: a drag that left still has a
    /// position. The canvas is found in the current frame by its position, as a code surface is, since
    /// the region a drag began on is a frame old.</summary>
    private CanvasPointer Local(CanvasRegion region, Point point, bool pressedNow, KeyModifiers modifiers)
    {
        var local = new Point(point.X - region.Bounds.X, point.Y - region.Bounds.Y);
        var canvases = _lastFrame?.CanvasRegions ?? [];
        for (var i = 0; i < canvases.Count; i++)
        {
            if (!ReferenceEquals(canvases[i].Element, region.Element)) continue;
            local = ToLocal(canvases, i, canvases[i].Bounds, point);
            break;
        }
        return new(local.X, local.Y, pressedNow, modifiers);
    }

    public bool Tap(float x, float y)
    {
        y += _lift;
        if (_lastFrame is null) return false;

        var point = new Point(x, y);
        var regions = _lastFrame.HitRegions;
        var hit = HitAt(regions, point);
        if (hit < 0) return false;
        if (!regions[hit].Node.Disabled) regions[hit].Node.OnPressed?.Invoke();
        return true;
    }

    /// <summary>
    /// The hit region <paramref name="point"/> lands on, as an index into <paramref name="regions"/>,
    /// or -1. The topmost region DRAWN under the point takes it, unless a region in front of that one
    /// reaches the point only with its slop (the target a control keeps beyond its box, §08) and
    /// stands inside its box: an icon button's target over the card it sits in, a dialog's button
    /// over its barrier. A slop never takes a press from a box drawn BESIDE it: in a list of rows
    /// shorter than the minimum target, each row's slop reached over its neighbours, and the row
    /// drawn after took their presses, the lower two thirds of every row under a finger (#630).
    /// </summary>
    private int HitAt(IReadOnlyList<HitRegion> regions, Point point)
    {
        var drawn = -1;
        for (var i = regions.Count - 1; i >= 0; i--)
        {
            if (!Covers(regions, i, regions[i].Drawn, point, drawn: true)) continue;
            drawn = i;
            break;
        }
        for (var i = regions.Count - 1; i > drawn; i--)
        {
            if (!Covers(regions, i, regions[i].Bounds, point)) continue;
            if (drawn < 0 || StandsInside(regions, i, drawn)) return i;
        }
        return drawn;
    }

    /// <summary>
    /// Whether <paramref name="point"/> lands on the <paramref name="index"/>th region of
    /// <paramref name="regions"/>, whose box on screen is <paramref name="box"/>: inside the box and,
    /// for a region a transform drew, inside its own rect once the point is taken back into its
    /// space, since a tilted region's box holds corners its drawing does not (#513).
    /// <paramref name="drawn"/> asks about a hit region's drawn box rather than its target.
    /// </summary>
    private bool Covers(object regions, int index, Rect box, Point point, bool drawn = false)
    {
        if (!box.Contains(point)) return false;
        if (TransformedAt(regions, index) is not { } region) return true;
        return (drawn ? region.LocalDrawn : region.Local).Contains(region.Inverse.Transform(point));
    }

    /// <summary>
    /// Whether <paramref name="point"/> lands on what the <paramref name="index"/>th code surface
    /// offers at its caret: inside the box on screen the list is drawn in and, for a surface a
    /// transform drew, inside the list itself once the point is taken back into the surface's space,
    /// as <see cref="Covers"/> tests the surface: the box around a turned list holds corners no row is
    /// drawn in, and the code drawn there lost its presses and its beam to the list.
    /// </summary>
    private bool OnOffered(IReadOnlyList<CodeRegion> surfaces, int index, Point point)
    {
        if (surfaces[index].Offered is not { } offered || !offered.Contains(point)) return false;
        return TransformedAt(surfaces, index) is not { LocalOffered: { } local } region
            || local.Contains(region.Inverse.Transform(point));
    }

    /// <summary>
    /// Whether <paramref name="point"/> lands on a note the <paramref name="index"/>th code surface
    /// draws over its code (<see cref="CodeSurface.Notes"/>): inside the box on screen the note is drawn
    /// in and, for a surface a transform drew, inside the note itself once the point is taken back into
    /// the surface's space, as <see cref="OnOffered"/> tests the list.
    /// </summary>
    private bool OnNote(IReadOnlyList<CodeRegion> surfaces, int index, Point point)
    {
        if (surfaces[index].Notes is not { } notes) return false;
        for (var note = 0; note < notes.Count; note++)
        {
            if (!notes[note].Contains(point)) continue;
            if (TransformedAt(surfaces, index) is not { LocalNotes: { } local } region
                || local[note].Contains(region.Inverse.Transform(point))) return true;
        }
        return false;
    }

    /// <summary>
    /// Whether the hit region at <paramref name="hit"/> (an index into the frame's hit regions, or -1) is
    /// covered at <paramref name="point"/> by a note a code surface painted after it
    /// (<see cref="CodeNoteLayer"/>), and which surface's, <paramref name="code"/>: the pointer and a press
    /// there are the note's. A control inside the note, or one painted after it, is not covered.
    /// </summary>
    private bool CoveredByNote(int hit, Point point, out int code)
    {
        code = -1;
        if (hit < 0 || _lastFrame is not { } frame) return false;
        foreach (var layer in frame.NoteLayers)
        {
            if (hit >= layer.FirstHit || !OnNote(frame.CodeRegions, layer.Code, point)) continue;
            code = layer.Code;
            return true;
        }
        return false;
    }

    /// <summary>Which surface's notes drew the hit region at <paramref name="hit"/> (a control inside a card),
    /// as an index into the frame's code regions, or -1: the pointer there is over the note.</summary>
    private int NoteDrawing(int hit)
    {
        if (hit < 0 || _lastFrame is not { } frame) return -1;
        foreach (var layer in frame.NoteLayers)
            if (layer.FirstHit <= hit && hit < layer.AfterHit) return layer.Code;
        return -1;
    }

    /// <summary>
    /// The pointer passing over the code with nothing pressed (web twin: lowerCodeSurface's
    /// pointermove): the surface it is over hears where it rests, for its hover to explain what is
    /// there, and a surface it left hears that it left. What is drawn over the code takes the pointer
    /// as it takes a press: a control or a field, and a surface's own options and notes, so a card being
    /// read stays open while the pointer is on it.
    /// </summary>
    private void HoverCode(Point point)
    {
        if (_lastFrame is null) return;
        var fields = _lastFrame.TextRegions;
        var hit = HitAt(_lastFrame.HitRegions, point);
        // On a note painted over a control, or on a control the note draws itself: still over that note's
        // surface, whose model hears nothing.
        var noted = CoveredByNote(hit, point, out var covering) ? covering : NoteDrawing(hit);
        if (noted >= 0)
        {
            var element = _lastFrame.CodeRegions[noted].Element;
            LeaveCode(except: element);
            _hoveredCode = element;
            return;
        }
        var over = hit >= 0;
        for (var i = fields.Count - 1; i >= 0 && !over; i--)
            over = Covers(fields, i, fields[i].Visible ?? fields[i].Bounds, point);
        var surfaces = _lastFrame.CodeRegions;
        for (var i = surfaces.Count - 1; i >= 0 && !over; i--)
        {
            if (OnOffered(surfaces, i, point) || OnNote(surfaces, i, point))
            {
                // Still over this surface, whose model hears nothing while the pointer is on what it drew.
                LeaveCode(except: surfaces[i].Element);
                _hoveredCode = surfaces[i].Element;
                return;
            }
            if (!Covers(surfaces, i, surfaces[i].Visible ?? surfaces[i].Bounds, point)) continue;
            LeaveCode(except: surfaces[i].Element);
            _hoveredCode = surfaces[i].Element;
            if (surfaces[i].Surface.Model.HandlePointer(PointerPhase.Hover,
                    ToLocal(surfaces, i, surfaces[i].Bounds, point), KeyModifiers.None, 0))
                SurfaceChanged(surfaces[i].Surface.OnChanged);
            return;
        }
        LeaveCode(except: null);
    }

    /// <summary>Tells the code surface the pointer was over, unless it is <paramref name="except"/>, that
    /// the pointer left it.</summary>
    private void LeaveCode(Element? except)
    {
        if (_hoveredCode is not { } left || ReferenceEquals(left, except)) return;
        _hoveredCode = null;
        if (_lastFrame is null) return;
        foreach (var region in _lastFrame.CodeRegions)
        {
            if (!ReferenceEquals(region.Element, left)) continue;
            if (region.Surface.Model.HandlePointer(PointerPhase.Exit, Point.Zero, KeyModifiers.None, 0))
                SurfaceChanged(region.Surface.OnChanged);
            return;
        }
    }

    /// <summary>
    /// <paramref name="point"/>, on screen, in the own space of the <paramref name="index"/>th region
    /// of <paramref name="regions"/>, whose box on screen is <paramref name="bounds"/>: its offset from
    /// the region's corner, taken back through the transform that drew the region when one did. It is
    /// the only arithmetic a host does for a press on a surface: what the point MEANS (a caret, a line
    /// and a column, a cell, a canvas's coordinates) is the surface's, and the same on every host.
    /// Subtracting the box's corner alone was exact for a translation, and a field drawn twice as
    /// large put the caret at twice the column the press was on (#658).
    /// </summary>
    private Point ToLocal(object regions, int index, Rect bounds, Point point)
    {
        if (TransformedAt(regions, index) is not { } region) return new Point(point.X - bounds.X, point.Y - bounds.Y);
        var local = region.Inverse.Transform(point);
        return new Point(local.X - region.Local.X, local.Y - region.Local.Y);
    }

    /// <summary>
    /// The box on screen that <paramref name="rect"/>, in the own space of the <paramref name="index"/>th
    /// region of <paramref name="regions"/>, is drawn in: <see cref="ToLocal"/> the other way, for
    /// what the host places by a surface's content: the platform's candidate window by its caret, and
    /// the caret a scroll view reveals. The frame keeps the inverse, which every pointer move asks
    /// for, so the rare way out inverts it back.
    /// </summary>
    private Rect ToScreen(object regions, int index, Rect bounds, Rect rect)
    {
        var (laid, toScreen) = Laid(regions, index, bounds, rect);
        return toScreen is { } matrix ? matrix.TransformBounds(laid) : laid;
    }

    /// <summary>
    /// <paramref name="rect"/>, in the own space of the <paramref name="index"/>th region of
    /// <paramref name="regions"/> and measured from its corner, as it was laid out, with what takes it
    /// to the screen when a transform drew the region: the rect on screen, at the corner of the box
    /// <paramref name="bounds"/>, and nothing otherwise. What carries it on (to the screen, to a scroll
    /// view's space) moves its four corners, so a turn never widens it twice.
    /// </summary>
    private (Rect Rect, Matrix2D? ToScreen) Laid(object regions, int index, Rect bounds, Rect rect) =>
        TransformedAt(regions, index) is { } region && region.Inverse.Invert() is { } forward
            ? (rect with { X = region.Local.X + rect.X, Y = region.Local.Y + rect.Y }, forward)
            : (rect with { X = bounds.X + rect.X, Y = bounds.Y + rect.Y }, null);

    /// <summary>
    /// What turns a point back into the space of the <paramref name="index"/>th region of
    /// <paramref name="regions"/>, if a transform drew it. Looked up through an index built once per
    /// frame, and only for a frame that transformed something: scanned, the table cost every region
    /// test a pass over it, and a page sliding in, all of whose regions are transformed, made a
    /// pointer move quadratic.
    /// </summary>
    private TransformedRegion? TransformedAt(object regions, int index)
    {
        var frame = _lastFrame;
        if (frame is null || frame.TransformedRegions.Count == 0) return null;
        if (!ReferenceEquals(_transformedFor, frame))
        {
            _transformedAt ??= new Dictionary<(object, int), TransformedRegion>();
            _transformedAt.Clear();
            foreach (var region in frame.TransformedRegions)
                _transformedAt[(region.Regions, region.Index)] = region;
            _transformedFor = frame;
        }
        return _transformedAt!.TryGetValue((regions, index), out var found) ? found : null;
    }

    // The frame the index of its transformed regions was built for, and the index.
    private RealizeResult? _transformedFor;
    private Dictionary<(object, int), TransformedRegion>? _transformedAt;

    /// <summary>
    /// Whether the box the <paramref name="inner"/>th hit region is drawn in lies wholly inside the box
    /// the <paramref name="outer"/>th is drawn in, compared as they are drawn: a region a transform drew
    /// is taken by its own four corners, carried into the other's space, since the boxes around two
    /// turned rows nest while the rows lie side by side, and the narrower row's slop took the wider
    /// one's presses. A box with no area lies nowhere: a row its scroll view clipped away keeps a sliver
    /// of slop on screen and a drawn box of zero height on the viewport's edge, which lay "inside" the
    /// last row shown, and took its presses for a row nobody could see.
    /// </summary>
    private bool StandsInside(IReadOnlyList<HitRegion> regions, int inner, int outer)
    {
        var box = regions[inner].Drawn;
        if (box.Width <= 0 || box.Height <= 0) return false;
        var innerTurned = TransformedAt(regions, inner);
        var outerTurned = TransformedAt(regions, outer);
        // The boxes on screen are exact, their clips included, unless a turn tilts one of them: a
        // translation, a scale and a quarter turn keep a box a box. Compared by their own corners, a
        // moved child that runs past its card lost the clip that kept it inside on screen.
        if (!Tilted(innerTurned) && !Tilted(outerTurned))
            return Holds(regions[outer].Drawn, box.Left, box.Top, box.Right, box.Bottom);
        var within = outerTurned?.LocalDrawn ?? regions[outer].Drawn;

        var local = innerTurned?.LocalDrawn ?? box;
        var carry = innerTurned?.Inverse.Invert() ?? Matrix2D.Identity;
        if (outerTurned is { } turned) carry *= turned.Inverse;
        var a = carry.Transform(new Point(local.Left, local.Top));
        var b = carry.Transform(new Point(local.Right, local.Top));
        var c = carry.Transform(new Point(local.Left, local.Bottom));
        var d = carry.Transform(new Point(local.Right, local.Bottom));
        return Holds(within, a.X, a.Y, a.X, a.Y) && Holds(within, b.X, b.Y, b.X, b.Y)
            && Holds(within, c.X, c.Y, c.X, c.Y) && Holds(within, d.X, d.Y, d.X, d.Y);
    }

    /// <summary>Whether a turn or a shear drew <paramref name="region"/>, tilting its box: its box on
    /// screen then holds corners it does not. A translation, a scale and a quarter turn keep a box a box,
    /// and a region nothing transformed is its box.</summary>
    private static bool Tilted(TransformedRegion? region)
    {
        if (region is not { } turned) return false;
        var m = turned.Inverse;
        const float flat = 1e-4f;
        var straight = MathF.Abs(m.M12) < flat && MathF.Abs(m.M21) < flat;
        var quarter = MathF.Abs(m.M11) < flat && MathF.Abs(m.M22) < flat;
        return !straight && !quarter;
    }

    /// <summary>Whether <paramref name="box"/> holds the rect from (<paramref name="left"/>,
    /// <paramref name="top"/>) to (<paramref name="right"/>, <paramref name="bottom"/>), edges included,
    /// within the hundredth of a point a matrix and its inverse round to.</summary>
    private static bool Holds(Rect box, float left, float top, float right, float bottom) =>
        left >= box.Left - 0.01f && top >= box.Top - 0.01f && right <= box.Right + 0.01f
        && bottom <= box.Bottom + 0.01f;

    /// <summary>
    /// WHERE the keyboard focus is, as the id the frame's <see cref="FocusStop"/>s carry, which a
    /// position keeps for as long as it lives — null when nothing holds it. Read-only and public
    /// because the host is not the only thing that needs the answer: a platform accessibility bridge
    /// has to report the focused element, and any harness driving this window has to know where Tab
    /// left off.
    /// <para>
    /// A field being EDITED holds the focus as much as a button wearing the ring does — the caret
    /// is simply that field's own indicator, which is why the ring state is tracked separately.
    /// Tab's own "where am I" already merges the two; reporting only the ring's half told the
    /// outside world that a form being typed into had focus nowhere.
    /// </para>
    /// </summary>
    public int? FocusedId => _textAt?.Id ?? (_focusedId != 0 ? _focusedId : null);

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
        // An app's own chord wins: ⌘K is the app's, and a control holding focus has no claim on it.
        if (BindingFor(key, modifiers) is { } binding)
        {
            binding.OnPressed();
            NeedsRender = true;
            return true;
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
            if (surface.Model.HandleKey(key, modifiers, KeyboardConvention, Clipboard))
            {
                RestartBlink();
                SurfaceChanged(surface.OnChanged);
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
        // THIS frame's stops by id: the node from the frame it was focused in is long gone.
        if (modifiers == KeyModifiers.None && _focusedId != 0
            && (key is "ArrowLeft" or "ArrowRight" or "ArrowUp" or "ArrowDown")
            && _lastFrame?.FocusStops is { } stops)
        {
            for (var i = 0; i < stops.Count; i++)
            {
                if (stops[i].Id != _focusedId || stops[i].Adjustable is not { } adjustable) continue;
                adjustable.OnAdjust(key is "ArrowRight" or "ArrowUp" ? 1 : -1);
                NeedsRender = true;
                return true;
            }
        }

        // A two-dimensional COMPOSITE under focus answers the moves its keyboard declares: arrows
        // walk a cell, PgUp/PgDn a page, +Shift a section, Home/End the row's bounds. Resolved out of
        // THIS frame's stops by id, like the Adjustable above, and read from NavigableKeys — the
        // very table the web realizer reads, which is why that table moved to where both can.
        //
        // Nothing dispatched here at all before the rows laid out (#248): Navigable.OnMove was wired
        // on one target, so a calendar on Photon was a grid you could not walk. A key the grid does
        // not claim answers null and travels on, so Tab and Escape still belong to the page.
        if (modifiers is KeyModifiers.None or KeyModifiers.Shift
            && _focusedId != 0
            && NavigableKeys.Move(key, modifiers.HasFlag(KeyModifiers.Shift)) is { } move
            && _lastFrame?.FocusStops is { } gridStops)
        {
            for (var i = 0; i < gridStops.Count; i++)
            {
                if (gridStops[i].Id != _focusedId || gridStops[i].Grid is not { } grid) continue;
                grid.OnMove(move);
                NeedsRender = true;
                return true;
            }
        }

        // A table viewport under focus scrolls with the keys its scroll takes: the arrows, the pages
        // and the ends. Tab is never one of them, so the focus still moves on.
        if (_focusedId != 0 && _lastFrame?.FocusStops is { } tableStops)
        {
            for (var i = 0; i < tableStops.Count; i++)
            {
                if (tableStops[i].Id != _focusedId || tableStops[i].Table is not { } table) continue;
                if (!table.Scroll.Key(key, modifiers)) break;
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
                if (Focused is null && _focusedId == 0) return false;
                ClearFocus();
                return true;

            // A blinking caret is a running animation: the frame clock has to keep turning for it,
            // and only while a field is actually being edited.

        }
        return false;
    }

    /// <summary>The binding of THIS frame that answers a chord: the last one declared (the dialog on
    /// top wins), skipping a focus-scoped one whose subtree does not hold the focus.</summary>
    private ShortcutBinding? BindingFor(string key, KeyModifiers modifiers)
    {
        var bindings = _lastFrame?.Shortcuts;
        if (bindings is null) return null;
        for (var i = bindings.Count - 1; i >= 0; i--)
        {
            var chord = bindings[i].Chord;
            if (chord.Modifiers != modifiers) continue;
            if (!string.Equals(chord.Key, key, StringComparison.OrdinalIgnoreCase)) continue;
            if (bindings[i].Scope is { } scope && !FocusIsWithin(scope)) continue;
            return bindings[i];
        }
        return null;
    }

    /// <summary>
    /// Whether the platform's back has anything to do on this frame: an Escape the frame binds, which
    /// is every open dialog, sheet, drawer, menu and picker, and a <c>ListDetail</c> showing its detail
    /// on a phone. An edit is not one of them (see <see cref="Back"/>).
    /// <para>
    /// A shell reads it after each frame because Android asks UP FRONT. Its predictive back plays
    /// the "leave the app" animation while the gesture is still under the finger, so an app takes
    /// back by registering for it before the gesture starts, and only while it has somewhere to go.
    /// False leaves back to the system, which leaves the app.
    /// </para>
    /// </summary>
    public bool CanGoBack => BindingFor(KeyChord.Escape.Key, KeyChord.Escape.Modifiers) is not null;

    /// <summary>
    /// The platform's back, Android's gesture and button: the layer on top (a dialog, a sheet, a
    /// drawer, a menu, a phone's detail pane) closes, through the Escape it already declares, because
    /// every layer back should close already answers to it. A field inside a dialog leaves with the
    /// dialog, as a binding answers every key before the field does.
    /// <para>
    /// Unlike <see cref="KeyDown"/>, neither an edit nor a focus ring takes it, as a focused
    /// <c>TextField</c> never holds Flutter's back and an <c>EditText</c> never holds Android's. On a
    /// phone the soft keyboard has already spent the first back going down, keeping the caret; a
    /// second back spent on the caret, or on a ring, would be a press the person saw nothing useful
    /// come of. With nothing open, back is the system's, and it leaves the app with the field still
    /// focused. Answers whether anything took it.
    /// </para>
    /// </summary>
    public bool Back() => CanGoBack && KeyDown(KeyChord.Escape.Key, KeyChord.Escape.Modifiers);

    /// <summary>
    /// Takes the tree down: every component that mounted is told it left, the root among them.
    /// What a shell calls when the surface this host drew for is
    /// gone for good, so that what the tree subscribed to (a clock, the network, the links the app
    /// is opened with) stops calling into components nobody will draw again. Idempotent.
    /// <para>
    /// A phone ends an activity and keeps the process: without this the tree of every activity the
    /// process ever had stayed subscribed to the process's services, and each heard every event
    /// the live one did.
    /// </para>
    /// </summary>
    public void Unmount() => _owner.UnmountAll();
}
