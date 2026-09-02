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

    /// <summary>Horizontal flex without <c>new</c> — <c>Row(gap: Space.S2, children: [ … ])</c>.</summary>
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
    /// A PACK glyph — what an icon package's catalog hands out (<c>MaterialSymbolsIcons.Home</c>,
    /// <c>LucideIcons.Check</c>). Named rather than mirrored because <c>Icon</c> is already the
    /// curated-enum factory and this surface has no overloads: without a name of its own, a pack
    /// glyph is unreachable in any file importing the surface, which is exactly the hole
    /// <c>Gap</c> and <c>DotBadge</c> fill for their types.
    /// </summary>
    public static Icon Glyph(IconGlyph glyph, float size = 24, ColorToken? color = null, string? label = null) =>
        new Icon(glyph, size, color, label);

    /// <summary>A curated glyph (spec A10) on the §07 size whitelist (16/20/24/32).</summary>
    public static Icon Icon(Icons glyph, float size = 24, ColorToken? color = null, string? label = null) =>
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
        string alt = "") =>
        new Image(source, width, height, fit, alt);

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
    public static Sticky Sticky(VisualNode child, float offset = 0) =>
        new Sticky(child, offset);

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

    /// <summary>Arrow-key adjustment semantics: one Tab stop, arrows call back with ±1.</summary>
    public static Adjustable Adjustable(VisualNode child, Action<int> onAdjust) =>
        new Adjustable(child, onAdjust);

    /// <summary>A keyboard shortcut live while this subtree is mounted (spec S8).</summary>
    public static Shortcut Shortcut(VisualNode child, KeyChord chord, Action onPressed) =>
        new Shortcut(child, chord, onPressed);

    /// <summary>A subtree that adapts to the window size class (spec S6).</summary>
    public static AdaptiveNode AdaptiveNode(VisualNode compact, VisualNode? medium = null,
        VisualNode? expanded = null) =>
        new AdaptiveNode(compact, medium, expanded);

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
    public static Slider Slider(float value, Action<float>? onChanged = null) =>
        new Slider(value, onChanged);

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

    /// <summary>Linear progress; null value = indeterminate.</summary>
    public static ProgressBar ProgressBar(float? value = null, Variant variant = Variant.Primary) =>
        new ProgressBar(value, variant);

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

    /// <summary>Icon-only button; the label is what assistive tech announces.</summary>
    public static IconButton IconButton(Icon glyph, string label,
        IconButtonKind kind = IconButtonKind.Standard, SizeVariant size = SizeVariant.Medium,
        Action? onPressed = null) =>
        new IconButton(glyph, label, kind, size, onPressed);

    /// <summary>The nothing-here state (spec B12).</summary>
    public static EmptyState EmptyState(Icon icon, string title, string? body = null) =>
        new EmptyState(icon, title, body);

    /// <summary>Horizontal tab strip.</summary>
    /// <summary>The segmented picker (spec B7) — one row of mutually exclusive choices.
    /// <paramref name="stretch"/> is the tail: true fills the width evenly (the default a settings
    /// row wants), false lets the control size to its content (what a tab strip above a panel
    /// wants).</summary>
    public static SegmentedControl SegmentedControl(IReadOnlyList<string> segments, int selectedIndex,
        Action<int>? onChanged = null, bool stretch = true) =>
        new SegmentedControl(segments, selectedIndex, onChanged) { Stretch = stretch };

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
