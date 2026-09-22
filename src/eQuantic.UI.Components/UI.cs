using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The DECLARATIVE surface: every vocabulary node and shared component as a factory method named
/// exactly like its type, so a build tree reads without a single <c>new</c>. Import once —
/// <c>using static eQuantic.UI.Components.UI;</c> (the SDK injects it globally) — and a screen is:
/// <code>
/// Column(gap: Space.S3, children: [
///     Text($"Count: {_count}", TypeRole.Display),
///     Button("Up", onPressed: () => SetState(() => _count++)),
/// ])
/// </code>
/// Contract: a factory mirrors its type's constructor EXACTLY (same parameter names, same order,
/// same defaults — named arguments carry over unchanged); an optional TAIL of parameters may
/// follow, each matching an <c>init</c> property by name and type (the factory applies them via
/// an initializer — how a declarative screen reaches semantics like <c>Label</c>/<c>Selected</c>
/// and layout like <c>Width</c>/<c>Height</c> on a container
/// that constructors deliberately do not carry); and container nodes append a final trailing
/// <c>children</c> parameter that accepts a collection expression. Rarer <c>init</c> properties
/// keep the constructor + initializer form; the factories are sugar, never a second API.
/// <para>
/// FENCES, both deliberate: no overloads — this class transpiles to a JS twin, and JS methods
/// cannot overload, so every node keeps ONE canonical factory signature; and no factories for
/// value records (NavItem, DialogAction, GridTrack…) — they are data, and data reads fine with
/// target-typed <c>new(…)</c>.
/// </para>
/// </summary>
public static class UI
{
    // ---- Layout containers (children as a collection expression) ----------------------------

    /// <summary>Vertical flex without <c>new</c> — <c>Column(gap: Space.S3, children: [ … ])</c>.</summary>
    public static Column Column(float gap = 0, MainAlign main = MainAlign.Start,
        CrossAlign cross = CrossAlign.Stretch, bool wrap = false, float? runGap = null,
        EdgeInsets? padding = null, SizeValue width = default, SizeValue height = default,
        VisualNode[]? children = null)
    {
        var node = new Column(gap, main, cross, wrap, runGap, padding) { Width = width, Height = height };
        if (children != null)
            foreach (var child in children)
                node.Add(child);
        return node;
    }

    /// <summary>Tells you when its child is ON SCREEN, and when it leaves — the question a table of
    /// contents asks, without wrapping the page in a scroll view to ask it.</summary>
    public static InView InView(VisualNode child, Action<bool> onChanged) => new InView(child, onChanged);

    /// <summary>Draws an overlay WHERE IT STANDS — the surface without the scrim, the viewport and
    /// the portal that normally carry it. Five components rendered blank on a page without it.</summary>
    public static InFlow InFlow(VisualNode child) => new InFlow(child);

    /// <summary>Draws its subtree AS IF it were hovered, pressed or focused — a gallery, a design
    /// review and a visual-regression suite could otherwise only ever show the rest state.</summary>
    public static Simulated Simulated(SimulatedState state, VisualNode child) =>
        new Simulated(state, child);

    /// <summary>Horizontal flex without <c>new</c> — <c>Row(gap: Space.S2, children: [ … ])</c>.</summary>
    public static Row Row(float gap = 0, MainAlign main = MainAlign.Start,
        CrossAlign cross = CrossAlign.Center, bool wrap = false, float? runGap = null,
        EdgeInsets? padding = null, SizeValue width = default, SizeValue height = default,
        VisualNode[]? children = null)
    {
        var node = new Row(gap, main, cross, wrap, runGap, padding) { Width = width, Height = height };
        if (children != null)
            foreach (var child in children)
                node.Add(child);
        return node;
    }

    /// <summary>True 2D layout (spec S4) — tracks first, then the auto-flowing children.</summary>
    public static Grid Grid(IReadOnlyList<GridTrack> columns, float gap = 0, float? rowGap = null,
        SizeValue width = default, SizeValue height = default, VisualNode[]? children = null)
    {
        var node = new Grid(columns, gap, rowGap) { Width = width, Height = height };
        if (children != null)
            foreach (var child in children)
                node.Add(child);
        return node;
    }

    /// <summary>A component draws its OWN pixels in the box the layout gives it, once per frame
    /// (W3) — a sunburst, a simulation, a chart whose geometry is recomputed from data that never
    /// stops. Pointer events arrive in the CANVAS's coordinates, so polar hit-testing and
    /// per-particle picking are the app's arithmetic. Fills its box unless told otherwise.</summary>
    public static Canvas Canvas(Action<ICanvasPainter> draw, SizeValue width = default,
        SizeValue height = default, Action<CanvasPointer>? onPointerDown = null,
        Action<CanvasPointer>? onPointerMove = null, Action<CanvasPointer>? onPointerUp = null,
        Action? onPointerLeave = null, string? label = null) =>
        new Canvas(draw, width, height)
        {
            OnPointerDown = onPointerDown,
            OnPointerMove = onPointerMove,
            OnPointerUp = onPointerUp,
            OnPointerLeave = onPointerLeave,
            Label = label,
        };

    /// <summary>Z-axis composition (spec A3) — paint order is child order, last on top.</summary>
    public static Stack Stack(Alignment align = Alignment.TopStart, SizeValue width = default,
        SizeValue height = default, VisualNode[]? children = null)
    {
        var node = new Stack(align) { Width = width, Height = height };
        if (children != null)
            foreach (var child in children)
                node.Add(child);
        return node;
    }

    // ---- Vocabulary atoms and wrappers -------------------------------------------------------

    /// <summary>The atom (spec A1): background, border, radius, padding around one child.</summary>
    public static Box Box(BoxStyle style = default, VisualNode? child = null) =>
        new Box(style, child);

    /// <summary>A shaped paragraph (spec A8) — role-driven type, token color.</summary>
    public static Text Text(string content, TypeRole role = TypeRole.BodyL, ColorToken? color = null,
        int maxLines = 0, TextAlignment align = TextAlignment.Start, bool mono = false,
        bool tabular = false, TypeStyle? styleOverride = null, int headingLevel = 0) =>
        new Text(content, role, color, maxLines, align, mono, tabular, styleOverride, headingLevel);

    /// <summary>Single-line entry (spec B9): value + change/submit callbacks. The optional tail is
    /// the field's own semantic surface — the accessible name (a placeholder is only a hint), the
    /// hint itself, the disabled bit and password obscuring; validation display and focus plumbing
    /// belong to the composing form component and stay initializer-only.</summary>
    public static TextEntry TextEntry(string value, Action<string>? onChanged = null,
        string? label = null, string? placeholder = null, bool disabled = false,
        bool obscure = false) =>
        new TextEntry(value, onChanged)
        {
            Label = label,
            Placeholder = placeholder,
            Disabled = disabled,
            Obscure = obscure,
        };

    /// <summary>Press surface with the §08 hit contract; the child owns all visuals. The optional
    /// tail is the SEMANTIC surface a standalone button needs — the accessible name, the
    /// toggle/pick state (aria-pressed/checked), the disabled bit, the pressed-state fill and the
    /// disclosure state (aria-expanded), and the composite ROLE (tab, radio, switch — a custom
    /// navigation's items are radios to assistive tech, and a declarative screen could not say so).
    /// Without it a declarative screen could not name an icon-only button or mark a nav item
    /// selected at all (the OS Cleaner F1 report; the role joined after its full migration).
    /// Modal machinery — <see cref="Primitives.Pressable.Mixed"/>,
    /// <see cref="Primitives.Pressable.InitialFocus"/> — stays initializer-only: it belongs to the
    /// components that own the pattern.</summary>
    public static Pressable Pressable(VisualNode child, Action? onPressed = null,
        string? label = null, bool? selected = null, bool disabled = false,
        ColorToken? pressedBackground = null, bool? expanded = null,
        PressableRole role = PressableRole.Button) =>
        new Pressable(child, onPressed)
        {
            Label = label,
            Selected = selected,
            Disabled = disabled,
            PressedBackground = pressedBackground,
            Expanded = expanded,
            Role = role,
        };

    /// <summary>Navigation semantics: the child becomes a link to <paramref name="destination"/>.
    /// <paramref name="label"/> names an icon-only link; <paramref name="current"/> marks the link
    /// that points at the page the reader is ON (<c>aria-current="page"</c> — a sidebar's active
    /// row stated to assistive tech, not just painted).</summary>
    public static Link Link(string destination, VisualNode child, string? label = null,
        bool current = false) =>
        new Link(destination, child)
        {
            Label = label,
            Current = current,
        };

    /// <summary>
    /// A glyph on the §07 size whitelist (16/20/24/32) — curated (<c>Icon(Icons.Close)</c>) or a
    /// pack's (<c>Icon(LucideIcons.Search)</c>), which is one factory because it is one constructor.
    /// <para>
    /// There was a second name, <c>Glyph</c>, and its own comment said why: <c>Icon</c> was the
    /// CURATED-enum factory, this surface has no overloads, and a pack glyph had nowhere else to go.
    /// The hole was in <see cref="eQuantic.UI.Primitives.Icon"/> having one constructor per family;
    /// with a curated glyph converting to an <c>IconGlyph</c> implicitly, both spellings reach the
    /// factory named after the type, which is the rule the extra name was working around.
    /// </para>
    /// </summary>
    public static Icon Icon(IconGlyph glyph, float size = 24, ColorToken? color = null, string? label = null) =>
        new Icon(glyph, size, color, label);

    /// <summary>A vector shape at any size — an icon freed of the size whitelist, and of the
    /// square box when <paramref name="height"/> gives it an aspect of its own.</summary>
    public static Vector Vector(IconGlyph glyph, float size, ColorToken? color = null, string? label = null,
        float height = 0) =>
        new Vector(glyph, size, color, label, height);

    /// <summary>Vector ARTWORK — several shapes, each in the colour its designer chose. Give it one
    /// number and it keeps the drawing's own aspect, because a squashed logo is a wrong logo.</summary>
    public static Drawing Drawing(VectorDrawing artwork, float width, float height = 0,
        ColorToken? tint = null, string? label = null) =>
        new Drawing(artwork, width, height, tint, label);

    /// <summary>A bitmap with explicit dimensions (layout never waits for the network).</summary>
    public static Image Image(string source, float width, float height, ImageFit fit = ImageFit.Cover,
        string label = "") =>
        new Image(source, width, height, fit, label);

    /// <summary>The indeterminate progress ring.</summary>
    public static Spinner Spinner(float size = IconSize.Dense, ColorToken? color = null) =>
        new Spinner(size, color);

    /// <summary>Flex child sharing LEFTOVER main-axis space by weight (spec A2). A non-zero
    /// <paramref name="basis"/> is the size a WRAPPING parent breaks lines against.</summary>
    public static Flexible Flexible(VisualNode child, int flex = 1, float basis = 0, int shrink = 1) =>
        new Flexible(child, flex, basis, shrink);

    /// <summary>Layout-only space that collapses when siblings need it (spec A4).</summary>
    public static Spacer Spacer(int flex = 1) =>
        new Spacer(flex);

    /// <summary>
    /// A RIGID gap of <paramref name="dp"/> — the one-off rhythm break a container's own
    /// <c>gap</c> cannot express (a heading that needs 34dp under it while its siblings sit at
    /// 16). Named rather than mirrored, for two reasons: it wraps a static factory
    /// (<c>Spacer.Fixed</c>) instead of a constructor, and the mirrored <c>Spacer(flex)</c> above
    /// SHADOWS the type — inside a file that imports this surface, <c>Spacer.Fixed(34)</c> stops
    /// compiling, so the rigid form needs a name of its own to stay reachable at all.
    /// </summary>
    public static Spacer Gap(float dp) =>
        // Fully qualified deliberately: the shadowing this method exists to work around bites
        // INSIDE this class too — `Spacer.Fixed(dp)` here binds `Spacer` to the method above.
        eQuantic.UI.Primitives.Spacer.Fixed(dp);

    /// <summary>Anchors a Stack child to the stack's edges with signed offsets (spec A3).</summary>
    public static Positioned Positioned(VisualNode child, float? top = null, float? end = null,
        float? bottom = null, float? start = null) =>
        new Positioned(child, top, end, bottom, start);

    /// <summary>A scrolling viewport over bounded content (spec A6).</summary>
    public static ScrollView ScrollView(VisualNode child, ScrollAxis axis = ScrollAxis.Vertical,
        SizeValue width = default, SizeValue height = default) =>
        new ScrollView(child, axis) { Width = width, Height = height };

    /// <summary>Keeps the child clear of system-owned display regions (notch, home indicator).</summary>
    public static SafeArea SafeArea(VisualNode child, SafeEdges edges = SafeEdges.All) =>
        new SafeArea(child, edges);

    /// <summary>Scroll-anchored chrome (spec S7): pins to the viewport start once scrolled.</summary>
    public static Pinned Pinned(VisualNode child, float offset = 0) =>
        new Pinned(child, offset);

    /// <summary>The viewport layer: the child escapes the page flow and paints above it.</summary>
    public static Overlay Overlay(VisualNode child) =>
        new Overlay(child);

    /// <summary>Floating panel positioned relative to its in-flow anchor (menus, popovers).</summary>
    public static Anchored Anchored(VisualNode anchor, VisualNode panel) =>
        new Anchored(anchor, panel);

    /// <summary>Pointer-presence callback (spec S5): true on enter, false on leave.</summary>
    public static Hoverable Hoverable(VisualNode child, Action<bool> onChanged) =>
        new Hoverable(child, onChanged);

    /// <summary>Enter motion for declarative appearance (spec §06) — fade or slide-up.</summary>
    public static Presence Presence(VisualNode child, PresenceMotion enter = PresenceMotion.Fade) =>
        new Presence(child, enter);

    /// <summary>Continuous one-axis gesture (spec S9): the child follows the finger.</summary>
    public static Draggable Draggable(VisualNode child, Action<float>? onReleased = null) =>
        new Draggable(child, onReleased);

    /// <summary>Vertical drag-to-dismiss (the sheet contract).</summary>
    public static DragDismiss DragDismiss(VisualNode child, Action? onDismiss = null) =>
        new DragDismiss(child, onDismiss);

    /// <summary>
    /// Progress semantics: the child REPORTS how far along something is. Not a control — no Tab
    /// stop and no handler, which is the whole difference from <see cref="Adjustable"/>.
    /// <para>
    /// <paramref name="value"/> null is INDETERMINATE, and the role stays: "something is happening
    /// and nobody knows how far" is exactly what a reader should say. That is the inverse of the
    /// slider's rule, where a missing value means the node is not a slider at all.
    /// </para>
    /// </summary>
    public static Progress Progress(VisualNode child, string label = "", RangeValue? value = null,
        string? valueText = null) =>
        new Progress(child) { Label = label, Value = value, ValueText = valueText };

    /// <summary>
    /// Live-region semantics: what changes INSIDE this subtree is announced wherever the user
    /// happens to be, and focus never moves. Announcing is not labelling — a label says what a thing
    /// IS and is read on arrival, a live region says what just HAPPENED.
    /// <para>
    /// <paramref name="urgency"/> defaults to polite because assertive is a cost every other
    /// announcement on the page pays: ask for it when the user cannot go on without hearing this,
    /// and not to be noticed. <paramref name="label"/> is what the region is FOR when the subtree
    /// alone does not say it — "Upload status" beside a bar whose own text is a percentage.
    /// </para>
    /// </summary>
    public static LiveRegion LiveRegion(VisualNode child,
        LiveRegionUrgency urgency = LiveRegionUrgency.Polite, string label = "") =>
        new LiveRegion(child) { Urgency = urgency, Label = label };

    /// <summary>
    /// Arrow-key adjustment semantics: one Tab stop, arrows call back with ±1.
    /// <para>
    /// <paramref name="value"/> is what the control HOLDS, and a slider needs it —
    /// <c>role="slider"</c> requires <c>aria-valuenow</c>, so a slider built without one announces
    /// <c>group</c> instead of lying about a position it does not know. The other two roles
    /// announce a SELECTION their children already state and take none.
    /// </para>
    /// </summary>
    public static Adjustable Adjustable(VisualNode child, Action<int> onAdjust,
        RangeValue? value = null, AdjustableRole role = AdjustableRole.Slider,
        string? valueText = null) =>
        new Adjustable(child, onAdjust) { Value = value, Role = role, ValueText = valueText };

    /// <summary>
    /// The 2-D twin of <see cref="Adjustable"/>: one Tab stop for the whole thing, and a keyboard
    /// that moves a selection around inside it — a calendar's month, a picker's grid.
    /// <para>
    /// <paramref name="rows"/> is a structural claim rather than "what is inside": a grid whose
    /// cells are not inside rows is an invalid accessibility tree, which is why it is not called
    /// <c>children</c>. <paramref name="hasHeaderRow"/> marks row 0 as the column headers, and
    /// <paramref name="activeCell"/> is what a screen reader announces the arrows moving to —
    /// without the focus ever leaving the composite's one stop.
    /// </para>
    /// </summary>
    public static Navigable Navigable(Action<NavigableMove> onMove, IReadOnlyList<VisualNode> rows,
        string label = "", NavigableRole role = NavigableRole.Grid, bool hasHeaderRow = false,
        (int Row, int Item)? activeCell = null) =>
        new Navigable(onMove, rows)
        {
            Label = label,
            Role = role,
            HasHeaderRow = hasHeaderRow,
            ActiveCell = activeCell,
        };

    /// <summary>A keyboard shortcut live while this subtree is mounted (spec S8).</summary>
    public static Shortcut Shortcut(VisualNode child, KeyChord chord, Action onPressed) =>
        new Shortcut(child, chord, onPressed);

    /// <summary>A subtree that adapts to the window size class (spec S6).</summary>
    public static AdaptiveNode AdaptiveNode(VisualNode compact, VisualNode? medium = null,
        VisualNode? expanded = null) =>
        new AdaptiveNode(compact, medium, expanded);

    /// <summary>A LIVE camera surface. Explicitly sized like <see cref="Image"/>, and for the same
    /// reason: layout cannot infer an extent from a source that has not answered yet. A null
    /// <paramref name="session"/> draws the placeholder, so "not started yet" needs no branch.</summary>
    public static CameraPreview CameraPreview(ICameraSession? session, float width, float height,
        CornerRadii cornerRadius = default, string label = "") =>
        new CameraPreview(session, width, height) { CornerRadius = cornerRadius, Label = label };

    /// <summary>
    /// Continuous transform-only loop motion around one child — the shimmer and
    /// indeterminate-progress building block. All five of its arguments are positional because
    /// none of them has a default worth guessing: a loop with no extent and no period is not a
    /// loop. <paramref name="hideAtRest"/> is the Reduce Motion policy, and the one thing that
    /// differs between a decorative shimmer (hidden at rest) and a progress bar (still, but
    /// there).
    /// </summary>
    public static LoopMotion LoopMotion(VisualNode child, LoopEffect effect, float fromX, float toX,
        int durationMs, bool hideAtRest = false) =>
        new LoopMotion(child, effect, fromX, toX, durationMs) { HideAtRest = hideAtRest };

    /// <summary>
    /// An EDITABLE code surface: whatever the child draws, plus a caret, a selection and a
    /// keyboard. The controller is a live object the composing component OWNS and keeps — it
    /// survives the rebuild each keystroke causes, which is why it is an argument and not state
    /// this node holds. <paramref name="onChanged"/> is how that rebuild is asked for, since the
    /// controller mutates outside the tree.
    /// <para>
    /// The surface's geometry (<c>ContentTop</c>, <c>LineHeight</c>, <c>ContentLeft</c>,
    /// <c>ColumnWidth</c>) and the two mark colours stay on the initializer: they are one
    /// component's arithmetic against its own font, not what a screen says when it places an
    /// editor.
    /// </para>
    /// </summary>
    public static CodeSurface CodeSurface(VisualNode child, CodeEditorController editor,
        Action? onChanged = null, string? label = null, bool autofocus = false) =>
        new CodeSurface(child, editor)
        {
            OnChanged = onChanged,
            Label = label,
            Autofocus = autofocus,
        };

    /// <summary>
    /// An EDITABLE SPREADSHEET surface: the grid the child draws, plus the selection band, the
    /// active-cell ring and the keyboard. The controller is owned and kept by the composing
    /// component, exactly as <see cref="CodeSurface"/>'s is.
    /// <para>
    /// <paramref name="firstRow"/> and <paramref name="firstCol"/> are the VIRTUALIZED window's
    /// origin — which sheet cell the child's top-left one draws — so they ride here rather than on
    /// the initializer: a scrolled sheet whose marks are placed against the wrong origin draws
    /// them over the wrong cells, which is a correctness answer and not decoration. The header
    /// offsets are the component's own arithmetic and stay behind <c>new</c>.
    /// </para>
    /// </summary>
    public static SheetSurface SheetSurface(VisualNode child, SheetController controller,
        Action? onChanged = null, string? label = null, int firstRow = 0, int firstCol = 0) =>
        new SheetSurface(child, controller)
        {
            OnChanged = onChanged,
            Label = label,
            FirstRow = firstRow,
            FirstCol = firstCol,
        };

    /// <summary>
    /// An embedded web DOCUMENT, isolated from the tree around it —
    /// <c>WebFrame(WebContent.Url("https://…"), title: "Map")</c> or
    /// <c>WebFrame(WebContent.Document(markup), title: "Live preview")</c>.
    /// <para>
    /// Both arguments are required and neither is a knob: <see cref="WebContent"/> is what makes
    /// address-or-document a choice the type enforces instead of a precedence, and a frame with no
    /// title is one a screen reader cannot describe. <paramref name="sandbox"/> starts at scripts
    /// alone and every further capability is granted by name.
    /// </para>
    /// <para>
    /// <c>Width</c> and <c>Height</c> are deliberately absent: they default to
    /// <see cref="SizeValue.Fill"/> on the node, C# has no constant expression for that, and a
    /// <c>= default</c> here would hand back a HUG frame where <c>new</c> gives a filling one —
    /// the one promise this surface makes that a tail cannot keep. They stay on the initializer.
    /// </para>
    /// </summary>
    public static WebFrame WebFrame(WebContent content, string title,
        WebSandbox sandbox = WebSandbox.Scripts, CornerRadii cornerRadius = default) =>
        new WebFrame(content, title) { Sandbox = sandbox, CornerRadius = cornerRadius };

    // ---- Shared component library ------------------------------------------------------------

    /// <summary>The design-system Button (spec A12) without <c>new</c>.</summary>
    public static Button Button(string label, Variant variant = Variant.Primary,
        SizeVariant size = SizeVariant.Medium, Action? onPressed = null) =>
        new Button(label, variant, size, onPressed);

    /// <summary>The GDPR/LGPD consent card — drawn while the visitor's answer is unknown, gone after
    /// it; <paramref name="policyHref"/> adds the privacy-policy link the card should never lack.</summary>
    public static CookieConsent CookieConsent(string? policyHref = null) =>
        new CookieConsent(policyHref);

    /// <summary>The surface container (spec A13).</summary>
    public static Card Card(VisualNode child, CardKind kind = CardKind.Elevated) =>
        new Card(child, kind);

    /// <summary>Compact tag/filter control.</summary>
    public static Chip Chip(string label, ChipKind kind = ChipKind.Filter, bool selected = false,
        Action? onPressed = null, Action? onRemove = null) =>
        new Chip(label, kind, selected, onPressed, onRemove);

    /// <summary>Count badge (spec A11).</summary>
    public static Badge Badge(int count = 0, int max = 99, Variant variant = Variant.Destructive) =>
        new Badge(count, max, variant);

    /// <summary>
    /// The COUNTLESS badge (spec A11) — a bare dot that says "something changed here" without
    /// saying how much. Named rather than mirrored for the same reason <see cref="Gap"/> is: it
    /// wraps a static factory (<c>Badge.AsDot</c>), and the mirrored <c>Badge(count, …)</c> above
    /// shadows the type, so <c>Badge.AsDot()</c> stops compiling in any file importing this
    /// surface — which is every file of a consumer's project.
    /// </summary>
    public static Badge DotBadge(Variant variant = Variant.Destructive) =>
        // Fully qualified deliberately: the shadowing bites INSIDE this class too.
        eQuantic.UI.Components.Badge.AsDot(variant);

    /// <summary>Initials avatar.</summary>
    public static Avatar Avatar(string initials, SizeVariant size = SizeVariant.Medium,
        string? name = null) =>
        new Avatar(initials, size, name);

    /// <summary>Inline status banner.</summary>
    public static Banner Banner(Variant status, string title, string? body = null) =>
        new Banner(status, title, body);

    /// <summary>Binary check control.</summary>
    /// <summary>A check, with the tail a form row needs: the accessible name it announces, and the
    /// disabled bit a batch operation turns on across a whole list.</summary>
    public static Checkbox Checkbox(bool @checked, Action? onChanged = null, string? label = null,
        bool disabled = false) =>
        new Checkbox(@checked, onChanged, label) { Disabled = disabled };

    /// <summary>Binary toggle. <paramref name="label"/> is what assistive tech announces — a list of
    /// switches with no names is a list of identical controls — and <paramref name="disabled"/> is
    /// what makes them inert while a batch applies.</summary>
    public static Switch Switch(bool on, Action? onChanged = null, string? label = null,
        bool disabled = false) =>
        new Switch(on, onChanged) { Label = label, Disabled = disabled };

    /// <summary>Continuous value control.</summary>
    public static Slider Slider(float value, Action<float>? onChanged = null,
        float min = 0, float max = 1, float step = 0, string label = "", string? valueText = null,
        bool disabled = false, Variant variant = Variant.Primary) =>
        new Slider(value, onChanged)
        {
            Min = min,
            Max = max,
            Step = step,
            Label = label,
            // The value IN WORDS — "R$ 400" for 400. Reachable HERE because the factory is the
            // authoring surface: a control whose factory cannot express its own configuration
            // leaves `new` as the only way to write it, which is the one thing the authoring rule
            // forbids. UI.Switch already carried its label and disabled the same way.
            ValueText = valueText,
            Disabled = disabled,
            Variant = variant,
        };

    /// <summary>Discrete increment/decrement control.</summary>
    public static Stepper Stepper(int value, Action<int>? onChanged = null) =>
        new Stepper(value, onChanged);

    /// <summary>A date, typed or picked from a calendar.</summary>
    public static DatePicker DatePicker(DateOnly? selected = null, Action<DateOnly>? onChanged = null,
        DateOnly? min = null, DateOnly? max = null, string label = "") =>
        new DatePicker(selected, onChanged, min, max, label);

    /// <summary>A time of day, picked from a list of slots.</summary>
    public static TimePicker TimePicker(TimeOnly? selected = null, Action<TimeOnly>? onChanged = null,
        int stepMinutes = 30, TimeOnly? min = null, TimeOnly? max = null, string label = "") =>
        new TimePicker(selected, onChanged, stepMinutes, min, max, label);

    /// <summary>A moment — the date and the time, as one value.</summary>
    public static DateTimePicker DateTimePicker(DateTime? selected = null, Action<DateTime>? onChanged = null,
        DateTime? min = null, DateTime? max = null, int stepMinutes = 30,
        string dateLabel = "", string timeLabel = "") =>
        new DateTimePicker(selected, onChanged, min, max, stepMinutes, dateLabel, timeLabel);

    /// <summary>A month at a time — the grid a date is picked from.</summary>
    public static Calendar Calendar(DateOnly? selected = null, Action<DateOnly>? onChanged = null,
        DateOnly? min = null, DateOnly? max = null) =>
        new Calendar(selected, onChanged, min, max);

    /// <summary>One choice out of a dropdown list.</summary>
    public static Select Select(IReadOnlyList<string> options, int selectedIndex = -1,
        Action<int>? onChanged = null, string? placeholder = null) =>
        new Select(options, selectedIndex, onChanged, placeholder);

    /// <summary>The full text field: label, helper, error, leading icon, and a trailing SLOT
    /// for whatever belongs at the end of the row — a picker's opener, a clear affordance.</summary>
    public static TextInput TextInput(string value, Action<string>? onChanged = null, string label = "",
        string? placeholder = null, string? helper = null, string? error = null,
        Icons? leading = null, SizeVariant size = SizeVariant.Large, VisualNode? trailing = null) =>
        new TextInput(value, onChanged, label, placeholder, helper, error, leading, size, trailing);

    /// <summary>The search entry. Null placeholder = the SDK's localized default.</summary>
    public static SearchField SearchField(string query, Action<string>? onChanged = null,
        string? placeholder = null, Action? onSubmit = null) =>
        new SearchField(query, onChanged, placeholder, onSubmit);

    /// <summary>
    /// Linear progress; null value = INDETERMINATE, which is a state the role reports rather than a
    /// value that went missing.
    /// </summary>
    public static ProgressBar ProgressBar(float? value = null, Variant variant = Variant.Primary,
        string label = "", string? valueText = null, bool prominent = false) =>
        new ProgressBar(value, variant) { Label = label, ValueText = valueText, Prominent = prominent };

    /// <summary>Hairline separator.</summary>
    public static Divider Divider(DividerInset inset = DividerInset.None,
        DividerAxis axis = DividerAxis.Horizontal) =>
        new Divider(inset, axis);

    /// <summary>A markdown document, rendered as the design system — themed by the app's own
    /// <c>IAppTheme</c> like every other component on the page.</summary>
    public static Markdown Markdown(string source) => new Markdown(source);

    /// <summary>A mermaid diagram drawn by the design system — flowcharts and sequence diagrams
    /// as Boxes, Text and Vectors, on web and Photon alike. Unknown grammars show as code.</summary>
    public static Mermaid Mermaid(string source) => new Mermaid(source);

    /// <summary>The language switch — swaps the app's culture through the host's own controller
    /// (re-render on web, repaint on native), no reload either way.</summary>
    public static CultureSwitcher CultureSwitcher(IReadOnlyList<CultureOption> options) =>
        new CultureSwitcher(options);

    /// <summary>
    /// Icon-only button; the label is what assistive tech announces and is never optional — an
    /// icon-only button is the one control with no text of its own to fall back on.
    /// <para>
    /// Takes the glyph as a NODE, and mirrors the constructor exactly, because both rules that
    /// govern this surface point the same way: a transpiled component gets ONE JS constructor, and
    /// a factory mirrors its constructor parameter-for-parameter so named arguments carry between
    /// the two forms. `Icon(Icons.Close)` and `Icon(LucideIcons.Search)` are how a glyph becomes
    /// one, which is the same shape every other node uses.
    /// </para>
    /// </summary>
    public static IconButton IconButton(Icon glyph, string label,
        IconButtonKind kind = IconButtonKind.Standard, SizeVariant size = SizeVariant.Medium,
        Action? onPressed = null) =>
        new IconButton(glyph, label, kind, size, onPressed);

    /// <summary>The nothing-here state (spec B12).</summary>
    public static EmptyState EmptyState(Icon icon, string title, string? body = null) =>
        new EmptyState(icon, title, body);

    /// <summary>The segmented picker (spec B7) — one row of mutually exclusive choices.
    /// <paramref name="stretch"/> is the tail: true fills the width evenly (the default a settings
    /// row wants), false lets the control size to its content (what a tab strip above a panel
    /// wants).</summary>
    public static SegmentedControl SegmentedControl(IReadOnlyList<string> segments, int selectedIndex,
        Action<int>? onChanged = null, bool stretch = true) =>
        new SegmentedControl(segments, selectedIndex, onChanged) { Stretch = stretch };

    /// <summary>Horizontal tab strip.</summary>
    public static Tabs Tabs(IReadOnlyList<string> labels, int selected, Action<int>? onSelect = null) =>
        new Tabs(labels, selected, onSelect);

    /// <summary>The phone's destination bar (spec B4) — 3-5 destinations across the bottom.</summary>
    public static BottomNavigation BottomNavigation(IReadOnlyList<NavItem> items, int selected,
        Action<int>? onSelect = null) =>
        new BottomNavigation(items, selected, onSelect);

    /// <summary>The same destinations stood on their side for a wide window (spec B4). Hand both
    /// this and the bar the SAME item list inside an AdaptiveNode and the shell follows the window.</summary>
    public static NavigationRail NavigationRail(IReadOnlyList<NavItem> items, int selected,
        Action<int>? onSelect = null) =>
        new NavigationRail(items, selected, onSelect);

    /// <summary>The sliding panel for everything past a rail's worth of destinations.</summary>
    public static Drawer Drawer(VisualNode content, bool open, Action? onDismiss = null) =>
        new Drawer(content, open, onDismiss);

    /// <summary>The bar across the top of a screen (spec B3) — Leading and Actions are init slots.</summary>
    public static AppBar AppBar(string title) => new AppBar(title);

    /// <summary>One row of a list (spec B7) — the slots (Leading, Trailing) are init properties.</summary>
    public static ListItem ListItem(string title, string? subtitle = null, Action? onPressed = null) =>
        new ListItem(title, subtitle, onPressed);

    /// <summary>A list built ON DEMAND from an index, for a collection too long to materialize.</summary>
    public static ListView ListView(int count, float itemExtent, Func<int, VisualNode> itemBuilder,
        SizeValue width = default, SizeValue height = default) =>
        new ListView(count, itemExtent, itemBuilder) { Width = width, Height = height };

    /// <summary>A list beside a detail on a wide window, one pane at a time on a phone (spec B4).
    /// The titles, the wide placeholder and the threshold are init slots.</summary>
    public static ListDetail ListDetail(VisualNode list, VisualNode? detail = null,
        Action? onBack = null) =>
        new ListDetail(list, detail, onBack);

    /// <summary>Modal dialog with its action row.</summary>
    public static Dialog Dialog(string title, string body, DialogAction[] actions,
        bool dismissible = false, Action? onDismiss = null) =>
        new Dialog(title, body, actions, dismissible, onDismiss);

    /// <summary>Transient confirmation (spec B15).</summary>
    public static Toast Toast(string message, Variant status = Variant.Info,
        string? actionLabel = null, Action? onAction = null) =>
        new Toast(message, status, actionLabel, onAction);

    /// <summary>Hover hint anchored to its child.</summary>
    public static Tooltip Tooltip(VisualNode child, string text) =>
        new Tooltip(child, text);

    /// <summary>Loading placeholder (spec B16).</summary>
    public static Skeleton Skeleton(SkeletonShape shape, float width, float height = 0) =>
        new Skeleton(shape, width, height);
}
