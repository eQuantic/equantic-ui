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
/// same defaults — named arguments carry over unchanged), and container nodes append a trailing
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
    public static Column Column(float gap = 0, VisualNode[]? children = null)
    {
        var node = new Column(gap);
        if (children != null)
            foreach (var child in children)
                node.Add(child);
        return node;
    }

    /// <summary>Horizontal flex without <c>new</c> — <c>Row(gap: Space.S2, children: [ … ])</c>.</summary>
    public static Row Row(float gap = 0, VisualNode[]? children = null)
    {
        var node = new Row(gap);
        if (children != null)
            foreach (var child in children)
                node.Add(child);
        return node;
    }

    /// <summary>True 2D layout (spec S4) — tracks first, then the auto-flowing children.</summary>
    public static Grid Grid(IReadOnlyList<GridTrack> columns, float gap = 0, float? rowGap = null,
        VisualNode[]? children = null)
    {
        var node = new Grid(columns, gap, rowGap);
        if (children != null)
            foreach (var child in children)
                node.Add(child);
        return node;
    }

    /// <summary>Z-axis composition (spec A3) — paint order is child order, last on top.</summary>
    public static Stack Stack(Alignment align = Alignment.TopStart, VisualNode[]? children = null)
    {
        var node = new Stack(align);
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
        int maxLines = 0) =>
        new Text(content, role, color, maxLines);

    /// <summary>Single-line entry (spec B9): value + change/submit callbacks.</summary>
    public static TextEntry TextEntry(string value, Action<string>? onChanged = null) =>
        new TextEntry(value, onChanged);

    /// <summary>Press surface with the §08 hit contract; the child owns all visuals.</summary>
    public static Pressable Pressable(VisualNode child, Action? onPressed = null) =>
        new Pressable(child, onPressed);

    /// <summary>Navigation semantics: the child becomes a link to <paramref name="href"/>.</summary>
    public static Link Link(string href, VisualNode child) =>
        new Link(href, child);

    /// <summary>A curated glyph (spec A10) on the §07 size whitelist (16/20/24/32).</summary>
    public static Icon Icon(Icons glyph, float size = 24, ColorToken? color = null, string? label = null) =>
        new Icon(glyph, size, color, label);

    /// <summary>A vector shape at any size — an icon freed of the size whitelist.</summary>
    public static Vector Vector(IconGlyph glyph, float size, ColorToken? color = null, string? label = null) =>
        new Vector(glyph, size, color, label);

    /// <summary>A bitmap with explicit dimensions (layout never waits for the network).</summary>
    public static Image Image(string source, float width, float height, ImageFit fit = ImageFit.Cover,
        string alt = "") =>
        new Image(source, width, height, fit, alt);

    /// <summary>The indeterminate progress ring.</summary>
    public static Spinner Spinner(float size = IconSize.Dense, ColorToken? color = null) =>
        new Spinner(size, color);

    /// <summary>Flex child sharing LEFTOVER main-axis space by weight (spec A2).</summary>
    public static Flexible Flexible(VisualNode child, int flex = 1) =>
        new Flexible(child, flex);

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
    public static ScrollView ScrollView(VisualNode child, ScrollAxis axis = ScrollAxis.Vertical) =>
        new ScrollView(child, axis);

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

    /// <summary>Initials avatar.</summary>
    public static Avatar Avatar(string initials, SizeVariant size = SizeVariant.Medium,
        string? name = null) =>
        new Avatar(initials, size, name);

    /// <summary>Inline status banner.</summary>
    public static Banner Banner(Variant status, string title, string? body = null) =>
        new Banner(status, title, body);

    /// <summary>Binary check control.</summary>
    public static Checkbox Checkbox(bool @checked, Action? onChanged = null, string? label = null) =>
        new Checkbox(@checked, onChanged, label);

    /// <summary>Binary toggle.</summary>
    public static Switch Switch(bool on, Action? onChanged = null) =>
        new Switch(on, onChanged);

    /// <summary>Continuous value control.</summary>
    public static Slider Slider(float value, Action<float>? onChanged = null) =>
        new Slider(value, onChanged);

    /// <summary>Discrete increment/decrement control.</summary>
    public static Stepper Stepper(int value, Action<int>? onChanged = null) =>
        new Stepper(value, onChanged);

    /// <summary>One choice out of a dropdown list.</summary>
    public static Select Select(IReadOnlyList<string> options, int selectedIndex = -1,
        Action<int>? onChanged = null, string? placeholder = null) =>
        new Select(options, selectedIndex, onChanged, placeholder);

    /// <summary>The full text field: label, helper, error, leading icon.</summary>
    public static TextInput TextInput(string value, Action<string>? onChanged = null, string label = "",
        string? placeholder = null, string? helper = null, string? error = null,
        Icons? leading = null, SizeVariant size = SizeVariant.Large) =>
        new TextInput(value, onChanged, label, placeholder, helper, error, leading, size);

    /// <summary>The search entry.</summary>
    public static SearchField SearchField(string query, Action<string>? onChanged = null,
        string placeholder = "Search…", Action? onSubmit = null) =>
        new SearchField(query, onChanged, placeholder, onSubmit);

    /// <summary>Linear progress; null value = indeterminate.</summary>
    public static ProgressBar ProgressBar(float? value = null, Variant variant = Variant.Primary) =>
        new ProgressBar(value, variant);

    /// <summary>Hairline separator.</summary>
    public static Divider Divider(DividerInset inset = DividerInset.None,
        DividerAxis axis = DividerAxis.Horizontal) =>
        new Divider(inset, axis);

    /// <summary>Icon-only button; the label is what assistive tech announces.</summary>
    public static IconButton IconButton(Icons glyph, string label,
        IconButtonKind kind = IconButtonKind.Standard, SizeVariant size = SizeVariant.Medium,
        Action? onPressed = null) =>
        new IconButton(glyph, label, kind, size, onPressed);

    /// <summary>The nothing-here state (spec B12).</summary>
    public static EmptyState EmptyState(Icons icon, string title, string? body = null) =>
        new EmptyState(icon, title, body);

    /// <summary>Horizontal tab strip.</summary>
    public static Tabs Tabs(IReadOnlyList<string> labels, int selected, Action<int>? onSelect = null) =>
        new Tabs(labels, selected, onSelect);

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
