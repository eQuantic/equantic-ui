using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// A tree of <see cref="TreeNode"/>s (#1530): a Space's containers, a repository's files, any
/// hierarchy an app has or loads. One row per VISIBLE node, indented by its level, with a chevron when
/// it may have children, its icon and its label.
/// <para>
/// WHAT IS WHOSE. The nodes are the app's, and so is the current one (<see cref="SelectedKey"/>), the one
/// item stated as selected. Which nodes are open, what was loaded and which item the keyboard is on are
/// the tree's own state, started from <see cref="ExpandedKeys"/>. A press, or Enter, reports the node
/// through <see cref="OnPressed"/> and, for a node that may have children, opens or closes it as well
/// (<see cref="OnToggled"/>), as the Space's folders and VS Code's explorer do on a single click.
/// </para>
/// <para>
/// LOADING. A node whose <see cref="TreeNode.Children"/> is null is loaded the first time it opens,
/// through <see cref="LoadChildren"/>: a loading item stands under it while the task runs, and an item
/// that says the load failed stands there after a failure, whose press tries again. What was loaded is
/// kept by the node's key, so a parent that builds its nodes again on every frame keeps it; children the
/// app gives win over it.
/// </para>
/// <para>
/// THE KEYBOARD is the WAI-ARIA tree pattern, on one Tab stop (a <see cref="Navigable"/> whose role is
/// <see cref="NavigableRole.Tree"/>): Down and Up walk the visible items; Right opens a closed node and
/// enters an open one; Left closes an open node and otherwise goes to the parent; Home and End; Page Down
/// and Page Up by what the viewport shows; Enter presses; and typing a label's first letters jumps to it.
/// Moving changes nothing of the app's. The item the keyboard is on wears the focus ring once the
/// keyboard has moved it, and loses it on a pointer's press.
/// </para>
/// <para>
/// Given a <see cref="Height"/>, the tree scrolls and brings the keyboard's item back into view when it
/// leaves it; <see cref="RevealPath"/> opens a node's ancestors, loading them as it goes, and puts the
/// keyboard on it.
/// </para>
/// <para>
/// EDITING IN PLACE (#1532). F2, or the app's <see cref="Edit"/> request, puts a field where a node's label
/// is, its text selected; Enter or leaving the field hands the name to <see cref="OnRenamed"/>, and Escape,
/// an empty name or the same name cancel. A request that <see cref="TreeEditRequest.Creates"/> opens the node
/// and puts a new item's field first under it, handed to <see cref="OnCreated"/>. Both return a task: while it
/// runs the node is busy, and when it ends the tree reads the changed children again through its loader, so
/// the change shows. Enter and Escape give the keyboard back to the tree.
/// </para>
/// <para>
/// A MENU PER NODE: <see cref="MenuBuilder"/> gives a node's items, opened on the node's context request (a
/// secondary click, a finger's hold, or the menu key on the keyboard's node) anchored to its row, and the
/// choice is reported through <see cref="OnMenuSelect"/>. What an item does is the app's; after a change of its
/// own, <see cref="Reload"/> reads a node's children again. A node in <see cref="BusyKeys"/> is dimmed and
/// takes no press, no menu and no edit.
/// </para>
/// <para>
/// THE ROW IS THE DEVELOPER'S: <see cref="RowBuilder"/> builds what a row shows after its chevron, and
/// <see cref="Style"/>, the list family's slot, sets its measures. The tree keeps the row itself (its
/// indentation, chevron, fill and focus ring, and what a reader hears). v1 fences: one selected node, no
/// virtualization.
/// </para>
/// <para>
/// A NODE MOVES BY DRAG (#1533): with <see cref="OnMoved"/>, the tree is a nesting <see cref="Sortable"/> and
/// each node row a <see cref="SortableItem"/>, so a pointer or the keyboard carries a node before, after or
/// inside another, and the app is told the node, its new parent and the sibling it lands after. A place that
/// would put a node inside itself is never offered, <see cref="CanMove"/> may refuse more, and a closed folder
/// held under a carried node opens. The rows have depths (#1594): between the end of a subtree and the next row, the
/// pointer's travel across or the keyboard's Right and Left pick how deep the node lands.
/// </para>
/// </summary>
public sealed class TreeView : StatefulComponent
{
    /// <summary>How long a pause ends a type-ahead search, the WAI-ARIA examples' own figure.</summary>
    private const double SearchPauseMs = 700;

    /// <summary>The rows a page moves by when the tree does not know its viewport.</summary>
    private const int RowsPerPageWithoutViewport = 10;

    /// <summary>How long a carried node held inside a closed folder waits before opening it: Finder's
    /// spring-loading is of that order.</summary>
    private const int OpenUnderPauseMs = 700;

    /// <summary>What a carried node does at a row: lands inside it, before it, or after it (the last row).</summary>
    private const int DropInto = 1;
    private const int DropBefore = 2;
    private const int DropAfter = 3;

    private static readonly IReadOnlyList<TreeNode> NoChildren = new List<TreeNode>();

    private readonly HashSet<string> _open = new();
    private readonly Dictionary<string, IReadOnlyList<TreeNode>> _loaded = new();
    private readonly HashSet<string> _loading = new();
    private readonly HashSet<string> _failed = new();

    /// <summary>The loads whose loader has been called: a load marked during a build waits for the tree
    /// to be mounted, and the server's one-frame render, which never mounts it, calls no loader.</summary>
    private readonly HashSet<string> _started = new();

    /// <summary>The loads asked for again while they ran: each starts once more when it ends, so a reload never
    /// leaves an older answer on screen.</summary>
    private readonly HashSet<string> _stale = new();
    private bool _inTree;

    /// <summary>The visible rows of the last build, which the keyboard walks.</summary>
    private List<TreeViewRow> _rows = new();

    /// <summary>Where the keyboard is: a node's key, or for a status item the key it stands under.</summary>
    private string? _activeKey;
    private bool _activeIsStatus;

    /// <summary>Whether the keyboard moved the active item last, which is when it wears the ring.</summary>
    private bool _keyboard;

    private string _search = "";
    private DateTime _searchAt;

    private bool _seeded;
    private IReadOnlyList<string>? _revealed;
    private IReadOnlyList<string>? _revealing;

    /// <summary>The node being renamed, or the node a new item is being created under; none when null.</summary>
    private string? _editKey;
    private bool _editCreates;
    private IconGlyph? _editIcon;
    private string _editText = "";

    /// <summary>Which edit is open: a field that already closed reports its blur under an old one, and is not
    /// heard.</summary>
    private int _editSerial;
    private int? _editSeen;
    private int? _reloadSeen;

    /// <summary>The nodes busy with the tree's own task (a rename, a creation under them).</summary>
    private readonly HashSet<string> _working = new();
    private HashSet<string> _busy = new();

    /// <summary>Raised to give the keyboard back to the tree when a field inside it closes by its keys.</summary>
    private int _focusVersion;

    /// <summary>The node whose menu is open, where the pointer asked for it, and its items.</summary>
    private string? _menuKey;
    private Point? _menuAt;
    private IReadOnlyList<MenuItem> _menuItems = new List<MenuItem>();
    private int _menuHighlight;

    /// <summary>The parent of the keyboard's node at the last build: where the keyboard goes when its node
    /// leaves the tree (renamed, deleted).</summary>
    private string? _activeParent;

    /// <summary>The row a carried node would land at, and how (<see cref="DropInto"/>, <see cref="DropBefore"/>,
    /// <see cref="DropAfter"/>), and the level whose indentation the line starts from, drawn while it is carried.</summary>
    private string? _dropKey;
    private int _dropKind;
    private int _dropLevel;

    /// <summary>The indentation of one level at the last build: the sortable's depth across the axis (#1594), and
    /// whether a carried node's subtree is left out of its places.</summary>
    private float _indent;

    /// <summary>The closed folder a carried node is held inside, and which wait is the live one.</summary>
    private string? _pauseKey;
    private int _pauseSerial;

    private bool _scrollToActive;
    private float _offset;
    private float _viewport;
    private float _rowHeight;
    private int _scrollVersion;
    private ScrollRequest? _scrollRequest;

    public TreeView(IReadOnlyList<TreeNode> roots, string? selectedKey = null, Action<TreeNode>? onPressed = null,
        ListTheme? style = null)
    {
        Roots = roots;
        SelectedKey = selectedKey;
        OnPressed = onPressed;
        _style = style;
    }

    /// <summary>The nodes at the top of the tree, in the order they show.</summary>
    public IReadOnlyList<TreeNode> Roots { get; private set; }

    /// <summary>The key of the app's current node, the one item stated as selected; none when null.</summary>
    public string? SelectedKey { get; private set; }

    /// <summary>A press on a row, or Enter on the keyboard's item: the node it was.</summary>
    public Action<TreeNode>? OnPressed { get; private set; }

    /// <summary>
    /// Loads the children of a node whose <see cref="TreeNode.Children"/> is null, the first time it
    /// opens. Without it, such a node has nothing to open.
    /// <para>
    /// Backed by a field, as every init slot here is, so <see cref="AdoptConfig"/> can carry it: this
    /// instance is kept across the parent's rebuilds, and an <c>init</c> accessor cannot be written from
    /// there.
    /// </para>
    /// </summary>
    public Func<TreeNode, Task<IReadOnlyList<TreeNode>>>? LoadChildren { get => _loadChildren; init => _loadChildren = value; }

    /// <summary>A node was opened (<c>true</c>) or closed, by a press or by the keyboard.</summary>
    public Action<TreeNode, bool>? OnToggled { get => _onToggled; init => _onToggled = value; }

    /// <summary>The keys open when the tree first builds; after that, which nodes are open is the tree's.</summary>
    public IReadOnlyList<string>? ExpandedKeys { get => _expandedKeys; init => _expandedKeys = value; }

    /// <summary>
    /// Keys from a root down to a node: every key but the last opens, loading what it needs, and the last
    /// becomes the keyboard's item, scrolled into view. A new path is revealed again; the same path given
    /// again moves nothing the person has since moved.
    /// </summary>
    public IReadOnlyList<string>? RevealPath { get => _revealPath; init => _revealPath = value; }

    /// <summary>The tree's name, as a reader announces it.</summary>
    public string Label { get => _label; init => _label = value; }

    public SizeValue Width { get => _width; init => _width = value; }

    /// <summary>Null, the default, lets the rows flow in the page; a height makes the tree scroll and
    /// keep the keyboard's item in view.</summary>
    public SizeValue? Height { get => _height; init => _height = value; }

    /// <summary>
    /// Builds what a node's row shows after its chevron, in place of its icon and its label (Flutter's
    /// <c>treeNodeBuilder</c>). The tree keeps the row: its indentation, its chevron, its fill, its focus ring and
    /// everything a reader hears, which is still the node's <see cref="TreeNode.Label"/>. A row being renamed
    /// shows the tree's own field.
    /// </summary>
    public Func<TreeRowState, VisualNode>? RowBuilder { get => _rowBuilder; init => _rowBuilder = value; }

    /// <summary>The tree's own measures and colours, laid over the theme's <see cref="IAppTheme.List"/>: the
    /// members whose names start with <c>Tree</c>, and the family's quiet fill, chevron and title colour.</summary>
    public ListTheme? Style => _style;

    /// <summary>
    /// Renames a node to the name typed, without the spaces around it. Without it, F2 and a rename request do
    /// nothing. While the task runs the node is busy; when it ends, the tree reads the parent's children again
    /// if it loaded them. A task that fails leaves the tree as it was: saying why is the app's.
    /// </summary>
    public Func<TreeNode, string, Task>? OnRenamed { get => _onRenamed; init => _onRenamed = value; }

    /// <summary>
    /// Creates an item named as typed under a node, after an <see cref="Edit"/> request that
    /// <see cref="TreeEditRequest.Creates"/>. While the task runs the node is busy; when it ends, the tree reads
    /// its children again if it loaded them.
    /// </summary>
    public Func<TreeNode, string, Task>? OnCreated { get => _onCreated; init => _onCreated = value; }

    /// <summary>A rename or a creation the app asks for, each version once (a menu's "Rename", "New file").</summary>
    public TreeEditRequest? Edit { get => _edit; init => _edit = value; }

    /// <summary>The nodes whose loaded children the tree reads again, each version once.</summary>
    public TreeReloadRequest? Reload { get => _reload; init => _reload = value; }

    /// <summary>The nodes the app is working on: dimmed, with no press, no menu and no edit, still walked by the
    /// keyboard.</summary>
    public IReadOnlyList<string>? BusyKeys { get => _busyKeys; init => _busyKeys = value; }

    /// <summary>A node's menu items, built when its menu opens; no menu opens without it, or with no item.</summary>
    public Func<TreeNode, IReadOnlyList<MenuItem>>? MenuBuilder { get => _menuBuilder; init => _menuBuilder = value; }

    /// <summary>The node whose menu it was and the index of the item chosen; the menu closes first.</summary>
    public Action<TreeNode, int>? OnMenuSelect { get => _onMenuSelect; init => _onMenuSelect = value; }

    /// <summary>
    /// Moves a node where a pointer or the keyboard dropped it: its new parent (null at the top level) and the
    /// sibling it lands after (null when it lands first; a node dropped inside another lands first). Without it,
    /// no node is carried. While the task runs the node is busy; when it ends, the tree reads the children it
    /// loaded for the old and the new parent again, and the new parent is open.
    /// </summary>
    public Func<TreeMove, Task>? OnMoved { get => _onMoved; init => _onMoved = value; }

    /// <summary>The app's guard, asked for every place a carried node could land, after the tree's own (a node
    /// never lands inside a leaf, nor inside itself or one of its descendants): a place it refuses is neither shown
    /// nor dropped on. Null takes every place.</summary>
    public Func<TreeMove, bool>? CanMove { get => _canMove; init => _canMove = value; }

    private Func<TreeRowState, VisualNode>? _rowBuilder;
    private ListTheme? _style;
    private Func<TreeNode, string, Task>? _onRenamed;
    private Func<TreeNode, string, Task>? _onCreated;
    private TreeEditRequest? _edit;
    private TreeReloadRequest? _reload;
    private IReadOnlyList<string>? _busyKeys;
    private Func<TreeNode, IReadOnlyList<MenuItem>>? _menuBuilder;
    private Action<TreeNode, int>? _onMenuSelect;
    private Func<TreeMove, Task>? _onMoved;
    private Func<TreeMove, bool>? _canMove;
    private Func<TreeNode, Task<IReadOnlyList<TreeNode>>>? _loadChildren;
    private Action<TreeNode, bool>? _onToggled;
    private IReadOnlyList<string>? _expandedKeys;
    private IReadOnlyList<string>? _revealPath;
    private string _label = "";
    private SizeValue _width = SizeValue.Fill;
    private SizeValue? _height;

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not TreeView fresh) return;
        Roots = fresh.Roots;
        SelectedKey = fresh.SelectedKey;
        OnPressed = fresh.OnPressed;
        _loadChildren = fresh.LoadChildren;
        _onToggled = fresh.OnToggled;
        _revealPath = fresh.RevealPath;
        _label = fresh.Label;
        _width = fresh.Width;
        _height = fresh.Height;
        _rowBuilder = fresh.RowBuilder;
        _style = fresh.Style;
        _onRenamed = fresh.OnRenamed;
        _onCreated = fresh.OnCreated;
        _edit = fresh.Edit;
        _reload = fresh.Reload;
        _busyKeys = fresh.BusyKeys;
        _menuBuilder = fresh.MenuBuilder;
        _onMenuSelect = fresh.OnMenuSelect;
        _onMoved = fresh.OnMoved;
        _canMove = fresh.CanMove;
    }

    protected override void OnMount()
    {
        _inTree = true;
        // The loads a build marked before the tree was in the tree: an ExpandedKeys or a RevealPath
        // naming a folder still to load.
        if (LoadChildren is not { } loader) return;
        // Load changes nothing before its first await, so the set is walked as it stands.
        foreach (var key in _loading)
            if (FindNode(key) is { } node) Load(node, loader);
    }

    protected override void OnUnmount() => _inTree = false;

    public override VisualNode Build(ComponentContext context)
    {
        if (!_seeded)
        {
            _seeded = true;
            if (ExpandedKeys is { } keys)
                foreach (var key in keys) _open.Add(key);
        }
        if (RevealPath is { Count: > 0 } path && !SamePath(path, _revealed))
        {
            _revealed = path;
            _revealing = path;
        }
        AdvanceReveal();

        _busy = new HashSet<string>(_working);
        if (BusyKeys is { } busyKeys)
            foreach (var key in busyKeys) _busy.Add(key);
        AdoptRequests();

        // The theme's list slot, then the tree's own style over it (#1545).
        var slot = ListTheme.Merge(context.Theme.List, Style);
        _rowHeight = slot?.TreeRowHeight ?? ControlMetrics.Height(context, SizeVariant.Small);
        _indent = slot?.TreeIndent ?? Space.S4;
        _rows = Flatten();
        // A status item that went away (its load finished) hands the keyboard to the node it stood under,
        // which is where the person was.
        if (_activeIsStatus && _activeKey is { } statusKey && !HasStatusRow(statusKey)) _activeIsStatus = false;
        // A node that left the tree (renamed, deleted) hands the keyboard to its parent, where the person was.
        if (_activeKey is not null && !_activeIsStatus && ActiveIndex() < 0 && _activeParent is { } parent
            && IndexOfNode(parent) >= 0)
            _activeKey = parent;
        var active = ActiveIndex();
        _activeParent = active >= 0 ? _rows[active].ParentKey : null;
        // A menu or an edit whose node left closes, as a panel or a field taken off the page does; so does a
        // menu whose node turned busy.
        if (_menuKey is { } menuKey && (IndexOfNode(menuKey) < 0 || _busy.Contains(menuKey))) _menuKey = null;
        if (_editKey is { } editKey && IndexOfNode(editKey) < 0)
        {
            _editKey = null;
            _editSerial++;
        }

        var items = new List<VisualNode>();
        for (var i = 0; i < _rows.Count; i++)
            items.Add(BuildRow(context, slot, _rows[i], ringed: i == active && _keyboard));

        VisualNode tree = new Navigable(Move, items)
        {
            Role = NavigableRole.Tree,
            Label = Label,
            ActiveCell = active >= 0 ? (active, 0) : null,
            OnTextSearch = Search,
            FocusVersion = _focusVersion,
        };
        var editing = _editKey is not null;
        // A tree that moves its nodes is a nesting sortable, whose items are the node rows (#1533): the pointer's
        // drag, the keyboard's, the lift and the edge scroll are the sortable's, on every target.
        if (OnMoved is not null)
        {
            tree = new Sortable(tree, "tree", Dropped, SdkStrings.Sorting)
            {
                Axis = DragAxis.Vertical,
                Nests = true,
                // The rows' levels are the sortable's depths, one indentation apart (#1594).
                Indent = _indent,
                Label = Label,
                Accepts = Accepts,
                OnHovered = Hovered,
                Enabled = !editing,
            };
        }
        var menuOpen = _menuKey is not null;
        var activeNode = active >= 0 ? _rows[active].Node : null;
        var activeFree = activeNode is not null && !_busy.Contains(activeNode.Key);
        // Enter presses the keyboard's item. Scoped to the tree, so two trees on a screen never both
        // answer it, and live only while there is an item to press: not while a field inside the tree takes
        // Enter, nor while a menu does.
        tree = new Shortcut(tree, KeyChord.Enter, PressActive) { FocusScoped = true, Enabled = active >= 0 && !editing && !menuOpen };
        // F2 renames the keyboard's node, as in every file tree.
        tree = new Shortcut(tree, new KeyChord("F2"), RenameActive)
        {
            FocusScoped = true,
            Enabled = OnRenamed is not null && activeFree && !editing && !menuOpen,
        };
        // The open menu's keys, the Menu's own (#1531).
        tree = MenuPanel.WithKeys(tree, menuOpen && _menuItems.Count > 0,
            () => SetState(() => _menuHighlight = MenuPanel.Step(_menuItems, _menuHighlight, +1)),
            () => SetState(() => _menuHighlight = MenuPanel.Step(_menuItems, _menuHighlight, -1)),
            () => ChooseMenu(_menuHighlight));

        if (Height is not { } height)
            return new Box(new BoxStyle { Width = Width }, tree);

        if (_scrollToActive && active >= 0 && _viewport > 0)
        {
            var top = active * _rowHeight;
            var bottom = top + _rowHeight;
            float? target = top < _offset ? top : bottom > _offset + _viewport ? bottom - _viewport : (float?)null;
            if (target is { } offset)
            {
                _scrollVersion++;
                _scrollRequest = new ScrollRequest(_scrollVersion) { Offset = offset };
            }
        }
        // Kept until the view has said how tall it is: a node revealed on the first frame is scrolled to
        // once the viewport is known, not forgotten before it.
        if (_viewport > 0) _scrollToActive = false;

        return new ScrollView(tree)
        {
            Width = Width,
            Height = height,
            OnScrolled = offset => _offset = offset,
            OnViewportChanged = viewport => _viewport = viewport,
            Request = _scrollRequest,
        };
    }

    private VisualNode BuildRow(ComponentContext context, ListTheme? slot, TreeViewRow row, bool ringed)
    {
        if (row.IsField) return BuildFieldRow(context, slot, row);
        var theme = context.Theme;
        var node = row.Node;
        var chevron = node is not null && HasChevron(node);
        var open = node is not null && _open.Contains(node.Key);
        var selected = node is not null && SelectedKey is not null && node.Key == SelectedKey;
        var failed = node is null && _failed.Contains(row.Key);
        var busy = node is not null && _busy.Contains(node.Key);
        var renaming = node is not null && !_editCreates && _editKey == node.Key;
        var iconColor = slot?.TreeIconColor ?? theme.TextSecondary;

        var content = Content(context, slot, chevron, open);
        var label = node?.Label ?? (failed ? SdkStrings.TreeLoadFailed : SdkStrings.TreeLoading);
        if (renaming)
        {
            // The tree's own content around the field, whatever a builder draws: a field belongs where the
            // label is, and only the tree knows where that is.
            if (node!.Icon is { } editedGlyph) content.Add(new Icon(editedGlyph, IconSize.Dense, iconColor));
            content.Add(new Flexible(Field(context)));
        }
        else if (node is not null && RowBuilder is { } builder)
        {
            content.Add(new Flexible(builder(new TreeRowState(node, row.Level, chevron ? open : null, selected, busy))));
        }
        else
        {
            if (node?.Icon is { } glyph)
                content.Add(new Icon(glyph, IconSize.Dense, iconColor));
            else if (failed)
                content.Add(new Icon(Icons.Refresh, IconSize.Dense, iconColor));
            content.Add(new Flexible(new Text(label, TypeRole.BodyM,
                node is null ? theme.TextSecondary : slot?.TitleColor ?? theme.TextPrimary, maxLines: 1)));
        }

        var quietFill = slot?.QuietFill ?? theme.SurfaceSubtle;
        // NOT on the selected row, whose fill IS its state (the list's rule, #616), nor on a row that takes no
        // press.
        var quiet = selected || busy || renaming ? null : new StyleDiff { Background = quietFill };
        // A carried node would land here (#1533): inside this row's node, ringed over the selection tint; or
        // before or after it, a line along its edge. Drawn over the surface, so no row moves under the pointer.
        var mark = node is not null && _dropKey == node.Key ? _dropKind : 0;
        var fill = selected ? theme.Colors(Variant.Primary).Subtle : mark == DropInto ? theme.Selection : (ColorToken?)null;
        var plain = RowSurface(context, slot, row.Level, content, fill, ringed && !renaming, busy, quiet,
            mark == DropInto ? DropColor(context, slot) : null);
        var surface = mark == DropBefore || mark == DropAfter
            ? DropLine(context, slot, _dropLevel > 0 ? _dropLevel : row.Level, plain, after: mark == DropAfter)
            : plain;

        // Each row is keyed by what it shows, at every layer around it, so a row is the same element on every target
        // while rows open above it: a carried node, a pressed one and a hovered one stay with their node (#1533).
        var key = node is null ? $"status/{row.Key}" : node.Key;
        VisualNode item = new Pressable(surface, busy || renaming ? null : () => Press(row, fromKeyboard: false))
        {
            Key = key,
            Role = PressableRole.TreeItem,
            Label = label,
            // A status item is not a node: it is neither picked nor opened.
            Selected = node is null ? null : selected,
            Expanded = chevron ? open : null,
            TreePlace = new TreePlace(row.Level, row.Position, row.Count),
            Disabled = busy,
        };
        if (node is null) return item;
        // A node row is what a tree that moves its nodes carries; the creation field and the status rows are not.
        if (OnMoved is not null)
            item = new SortableItem(item, node.Key) { Key = key, Enabled = !busy && _editKey is null, Depth = row.Level - 1 };
        // A target per row, so a pointer asks for the menu of the row it is over and the keyboard for the menu
        // of the tree's active item; ONE menu open, built when it opens.
        if (MenuBuilder is not null)
            item = new ContextTarget(item, request => RequestMenu(node, request)) { Key = key, Enabled = !busy && !renaming };
        if (_menuKey == node.Key)
        {
            item = new Anchored(item, MenuPanel.Build(context, _menuItems, true, _menuHighlight, ChooseMenu))
            {
                Key = key,
                At = _menuAt,
                Placement = AnchorPlacement.BottomStart,
                Open = true,
                OnDismiss = CloseMenu,
                PanelRole = AnchorPanelRole.Menu,
                ActiveIndex = _menuHighlight,
            };
        }
        return item;
    }

    /// <summary>The field of a new item being created under a node: the request's icon and the field, at its
    /// children's indentation.</summary>
    private VisualNode BuildFieldRow(ComponentContext context, ListTheme? slot, TreeViewRow row)
    {
        var content = Content(context, slot, chevron: false, open: false);
        if (_editIcon is { } glyph) content.Add(new Icon(glyph, IconSize.Dense, slot?.TreeIconColor ?? context.Theme.TextSecondary));
        content.Add(new Flexible(Field(context)));
        var surface = RowSurface(context, slot, row.Level, content, null, false, false, null);
        return new Pressable(surface, null)
        {
            Key = $"field/{row.Key}",
            Role = PressableRole.TreeItem,
            Label = SdkStrings.TreeItemName,
            TreePlace = new TreePlace(row.Level, row.Position, row.Count),
        };
    }

    /// <summary>A row's content up to its chevron: the chevron's slot, always there, so a leaf's label lines up
    /// with its siblings'.</summary>
    private static Row Content(ComponentContext context, ListTheme? slot, bool chevron, bool open)
    {
        // Its own size, not the accordion's (the family's ChevronSize): a tree's chevron is a control's glyph.
        var size = IconSize.Dense;
        var content = new Row(gap: slot?.TreeGap ?? Space.S1) { Cross = CrossAlign.Center, Width = SizeValue.Fill, Height = SizeValue.Fill };
        content.Add(new Box(new BoxStyle { Width = SizeValue.Fixed(size), Height = SizeValue.Fixed(size) },
            chevron
                ? new Icon(open ? Icons.ChevronDown : Icons.ChevronRight, size, slot?.ChevronColor ?? context.Theme.TextSecondary)
                : null));
        return content;
    }

    /// <summary>A row's surface: its height, its indentation by level (so the depth a reader hears is the depth a
    /// person sees), its fill, and the focus ring, always drawn and transparent unless the keyboard is here, so it
    /// never moves the content when it appears.</summary>
    private VisualNode RowSurface(ComponentContext context, ListTheme? slot, int level, VisualNode content,
        ColorToken? fill, bool ringed, bool busy, StyleDiff? quiet, ColorToken? marked = null)
    {
        var theme = context.Theme;
        var padding = slot?.TreePaddingX ?? Space.S2;
        var indent = slot?.TreeIndent ?? Space.S4;
        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fixed(_rowHeight),
            Padding = new EdgeInsets(padding + (level - 1) * indent, 0, padding, 0),
            CornerRadius = new CornerRadii(theme.Shape(slot?.TreeRowShape ?? ShapeScale.Small)),
            Background = fill,
            BorderWidth = 2,
            BorderColor = marked ?? (ringed ? theme.FocusRing : new ColorToken(Color.Transparent)),
            Opacity = busy ? theme.DisabledOpacity : null,
            Hover = quiet,
            Pressed = quiet,
        }, content);
    }

    /// <summary>The field of the open edit, its text selected as it takes the keyboard. Enter commits and gives the
    /// keyboard back; leaving it commits; Escape cancels and gives the keyboard back.</summary>
    private VisualNode Field(ComponentContext context)
    {
        var theme = context.Theme;
        var serial = _editSerial;
        var entry = new TextEntry(_editText, text => _editText = text)
        {
            Label = SdkStrings.TreeItemName,
            Role = TypeRole.BodyM,
            Autofocus = true,
            SelectOnFocus = true,
            OnSubmit = () => Commit(serial, giveBack: true),
            OnFocusChanged = focused =>
            {
                if (!focused) Commit(serial, giveBack: false);
            },
        };
        VisualNode field = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Colors(Variant.Primary).Base,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Small)),
            Padding = EdgeInsets.Symmetric(Space.S1, 0),
        }, entry);
        return new Shortcut(field, KeyChord.Escape, () => CloseEdit(serial)) { FocusScoped = true };
    }

    // ---- What the tree knows ------------------------------------------------------------------------

    /// <summary>The children the tree knows for a node: the app's, then the loaded ones; none for a leaf
    /// or when nothing can load them; null while they are still to load.</summary>
    private IReadOnlyList<TreeNode>? ChildrenOf(TreeNode node)
    {
        if (node.IsLeaf) return NoChildren;
        if (node.Children is { } given) return given;
        if (_loaded.TryGetValue(node.Key, out var loaded)) return loaded;
        return LoadChildren is null ? NoChildren : null;
    }

    /// <summary>Whether a node may have children: not a leaf, and not known to have none.</summary>
    private bool HasChevron(TreeNode node) => ChildrenOf(node) is not { Count: 0 };

    private List<TreeViewRow> Flatten()
    {
        var rows = new List<TreeViewRow>();
        AddLevel(rows, Roots, null, 1);
        return rows;
    }

    private void AddLevel(List<TreeViewRow> rows, IReadOnlyList<TreeNode> nodes, string? parent, int level)
    {
        // A new item's field stands first among the children it joins, and they count it.
        var field = parent is not null && IsCreatingUnder(parent) ? 1 : 0;
        if (field == 1) rows.Add(new TreeViewRow(parent!, null, parent, level, 1, nodes.Count + 1) { IsField = true });
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            rows.Add(new TreeViewRow(node.Key, node, parent, level, i + 1 + field, nodes.Count + field));
            if (!_open.Contains(node.Key)) continue;
            var children = ChildrenOf(node);
            if (children is not null)
            {
                AddLevel(rows, children, node.Key, level + 1);
                continue;
            }
            // Open and still to load: an app's ExpandedKeys can name such a node before any press did.
            if (!_loading.Contains(node.Key) && !_failed.Contains(node.Key)) StartLoad(node);
            if (IsCreatingUnder(node.Key))
                rows.Add(new TreeViewRow(node.Key, null, node.Key, level + 1, 1, 2) { IsField = true });
            rows.Add(new TreeViewRow(node.Key, null, node.Key, level + 1, 1, 1));
        }
    }

    private bool IsCreatingUnder(string key) => _editCreates && _editKey == key;

    private TreeNode? FindNode(string key) => FindIn(Roots, key, deep: true);

    private TreeNode? FindIn(IReadOnlyList<TreeNode> nodes, string key, bool deep)
    {
        foreach (var node in nodes)
        {
            if (node.Key == key) return node;
            if (!deep || node.IsLeaf) continue;
            var children = node.Children ?? (_loaded.TryGetValue(node.Key, out var loaded) ? loaded : null);
            if (children is not null && FindIn(children, key, deep: true) is { } found) return found;
        }
        return null;
    }

    private int ActiveIndex()
    {
        if (_activeKey is null) return -1;
        for (var i = 0; i < _rows.Count; i++)
            if (_rows[i].Key == _activeKey && _rows[i].IsStatus == _activeIsStatus) return i;
        return -1;
    }

    private bool HasStatusRow(string key)
    {
        foreach (var row in _rows)
            if (row.IsStatus && row.Key == key) return true;
        return false;
    }

    private int IndexOfNode(string key)
    {
        for (var i = 0; i < _rows.Count; i++)
            if (_rows[i].Key == key && !_rows[i].IsStatus) return i;
        return -1;
    }

    private static bool SamePath(IReadOnlyList<string> path, IReadOnlyList<string>? other)
    {
        if (other is null || other.Count != path.Count) return false;
        for (var i = 0; i < path.Count; i++)
            if (path[i] != other[i]) return false;
        return true;
    }

    // ---- Opening and loading ------------------------------------------------------------------------

    /// <summary>Opens or closes a node, starting its load when it opens with children still to load.
    /// State only: the callers run it inside SetState, or inside a build.</summary>
    private void Toggle(TreeNode node, bool open)
    {
        if (!open)
        {
            _open.Remove(node.Key);
            return;
        }
        _open.Add(node.Key);
        if (ChildrenOf(node) is null && !_loading.Contains(node.Key)) StartLoad(node);
    }

    /// <summary>Marks the load as running and runs it. State only, like <see cref="Toggle"/>: what it
    /// changes shows in the build that asked for it.</summary>
    private void StartLoad(TreeNode node)
    {
        if (LoadChildren is not { } loader) return;
        _loading.Add(node.Key);
        _failed.Remove(node.Key);
        Load(node, loader);
    }

    private async void Load(TreeNode node, Func<TreeNode, Task<IReadOnlyList<TreeNode>>> loader)
    {
        // Never inside the build or the handler that asked: a task already finished would otherwise go on
        // in it, and set state while the frame is being built.
        await Task.Yield();
        // Not before the tree is mounted (OnMount starts what waited), and once per load.
        if (!_inTree || !_loading.Contains(node.Key) || !_started.Add(node.Key)) return;
        try
        {
            var children = await loader(node);
            SetState(() =>
            {
                _loading.Remove(node.Key);
                _started.Remove(node.Key);
                _loaded[node.Key] = children;
                if (_stale.Remove(node.Key)) StartLoad(node);
            });
        }
        catch (Exception)
        {
            SetState(() =>
            {
                _loading.Remove(node.Key);
                _started.Remove(node.Key);
                _failed.Add(node.Key);
                if (_stale.Remove(node.Key)) StartLoad(node);
            });
        }
    }

    /// <summary>Takes a reveal one step further each build: opens what is known, starts the load it waits
    /// for, and when the path is open puts the keyboard on its last node. A path whose load failed ends
    /// there; one that names nodes the tree does not have yet waits for them.</summary>
    private void AdvanceReveal()
    {
        if (_revealing is not { } path) return;
        var level = Roots;
        for (var i = 0; i < path.Count - 1; i++)
        {
            // Not in the tree YET: an app often hands the path before the nodes it names (the roots are
            // still loading), so the reveal waits for a build that has them, until a new path replaces it.
            if (FindIn(level, path[i], deep: false) is not { } node) return;
            _open.Add(node.Key);
            if (ChildrenOf(node) is not { } children)
            {
                if (_failed.Contains(node.Key)) _revealing = null;
                else if (!_loading.Contains(node.Key)) StartLoad(node);
                return;
            }
            level = children;
        }
        var last = path[path.Count - 1];
        if (FindIn(level, last, deep: false) is null) return;
        _activeKey = last;
        _activeIsStatus = false;
        _scrollToActive = true;
        _revealing = null;
    }

    // ---- Pointer and keyboard ----------------------------------------------------------------------

    private void Press(TreeViewRow row, bool fromKeyboard)
    {
        if (row.Node is not { } node)
        {
            SetState(() =>
            {
                _activeKey = row.Key;
                _activeIsStatus = true;
                _keyboard = fromKeyboard;
                if (_failed.Contains(row.Key) && FindNode(row.Key) is { } failed) StartLoad(failed);
            });
            return;
        }
        bool? toggled = HasChevron(node) ? !_open.Contains(node.Key) : null;
        SetState(() =>
        {
            _activeKey = node.Key;
            _activeIsStatus = false;
            _keyboard = fromKeyboard;
            if (toggled is { } open) Toggle(node, open);
        });
        OnPressed?.Invoke(node);
        if (toggled is { } opened) OnToggled?.Invoke(node, opened);
    }

    private void PressActive()
    {
        var at = ActiveIndex();
        if (at < 0 || _rows[at].Node is { } node && _busy.Contains(node.Key)) return;
        Press(_rows[at], fromKeyboard: true);
    }

    // ---- Editing in place -------------------------------------------------------------------------

    /// <summary>The app's requests, each version once. State only: it runs inside a build.</summary>
    private void AdoptRequests()
    {
        if (Edit is { } edit && edit.Version != _editSeen)
        {
            _editSeen = edit.Version;
            BeginEdit(edit.Key, edit.Creates, edit.Icon);
        }
        if (Reload is { } reload && reload.Version != _reloadSeen)
        {
            _reloadSeen = reload.Version;
            foreach (var key in reload.Keys) ReloadChildren(key);
        }
    }

    /// <summary>Opens the field of a rename of <paramref name="key"/>, or of a new item under it, the node opened
    /// first. Nothing without the app's callback, on a node the person cannot see (its field would close on the
    /// build that opened it, taking the keyboard with it), on a busy one, or for a new item under a leaf. State
    /// only.</summary>
    private void BeginEdit(string key, bool creates, IconGlyph? icon)
    {
        if ((creates ? OnCreated : OnRenamed) is null || FindNode(key) is not { } node || _busy.Contains(key)
            || IndexOfNode(key) < 0) return;
        if (creates)
        {
            if (node.IsLeaf) return;
            Toggle(node, true);
        }
        _editKey = key;
        _editCreates = creates;
        _editIcon = icon;
        _editText = creates ? "" : node.Label;
        _editSerial++;
        _menuKey = null;
        _activeKey = key;
        _activeIsStatus = false;
        _scrollToActive = true;
    }

    private void RenameActive()
    {
        var at = ActiveIndex();
        if (at < 0 || _rows[at].Node is not { } node) return;
        SetState(() => BeginEdit(node.Key, creates: false, icon: null));
    }

    /// <summary>
    /// Ends the edit <paramref name="serial"/> with the name typed, without the spaces around it: an empty name, or
    /// a rename to the same name, cancels. Otherwise the app's task runs, the node busy meanwhile. A field that
    /// already closed (its Enter, then its blur as it leaves the page) is not heard twice.
    /// </summary>
    private void Commit(int serial, bool giveBack)
    {
        if (serial != _editSerial || _editKey is not { } key) return;
        var creates = _editCreates;
        var name = _editText.Trim();
        var node = FindNode(key);
        var handler = creates ? OnCreated : OnRenamed;
        // Where the change is: the renamed node's siblings, or the children of the node created under.
        var changed = creates ? key : ParentOf(key);
        var runs = node is not null && handler is not null && name.Length > 0 && (creates || name != node.Label);
        SetState(() =>
        {
            CloseEditState(giveBack);
            if (runs) _working.Add(key);
        });
        if (runs) RunEdit(handler!, node!, name, key, changed);
    }

    private void CloseEdit(int serial)
    {
        if (serial != _editSerial || _editKey is null) return;
        SetState(() => CloseEditState(giveBack: true));
    }

    /// <summary>Closes the field, and gives the keyboard back to the tree when the field closed by its own keys
    /// (a press elsewhere leaves it where the person put it). State only.</summary>
    private void CloseEditState(bool giveBack)
    {
        _editKey = null;
        _editSerial++;
        if (!giveBack) return;
        _focusVersion++;
        _keyboard = true;
    }

    /// <summary>The app's task for an edit: <paramref name="busyKey"/> busy while it runs, and the children of
    /// <paramref name="changedKey"/> read again once it succeeded. A task that fails leaves the tree as it was:
    /// the app awaits its own work and says why.</summary>
    private async void RunEdit(Func<TreeNode, string, Task> handler, TreeNode node, string name, string busyKey,
        string? changedKey)
    {
        var done = false;
        try
        {
            await handler(node, name);
            done = true;
        }
        catch (Exception)
        {
            done = false;
        }
        SetState(() =>
        {
            _working.Remove(busyKey);
            if (done && changedKey is not null) ReloadChildren(changedKey);
        });
    }

    /// <summary>Reads again the children the tree loaded for <paramref name="key"/>, keeping the old ones on screen
    /// until the new ones arrive. Nothing for a node whose children the app gives, or that never loaded. State
    /// only.</summary>
    private void ReloadChildren(string key)
    {
        if (!_loaded.ContainsKey(key) || FindNode(key) is not { } node || node.Children is not null) return;
        // A load of it still running answers what was there before the change: it is read again when it ends.
        if (_started.Contains(key))
        {
            _stale.Add(key);
            return;
        }
        StartLoad(node);
    }

    private string? ParentOf(string key)
    {
        var at = IndexOfNode(key);
        return at >= 0 ? _rows[at].ParentKey : null;
    }

    // ---- moving a node (#1533) ----------------------------------------------------------------------------

    /// <summary>The colour of the line and the ring that say where a carried node lands.</summary>
    private static ColorToken DropColor(ComponentContext context, ListTheme? slot) =>
        slot?.TreeDropColor ?? context.Theme.FocusRing;

    /// <summary>The row's surface with the line along its top edge (its bottom edge for the last row), from the
    /// indentation of the level the carried node would take: the row's own, or the one its depth chose (#1594).</summary>
    private VisualNode DropLine(ComponentContext context, ListTheme? slot, int level, VisualNode surface, bool after)
    {
        var start = (slot?.TreePaddingX ?? Space.S2) + (level - 1) * (slot?.TreeIndent ?? Space.S4);
        var line = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fixed(2),
            Background = DropColor(context, slot),
        });
        var stack = new Stack { Width = SizeValue.Fill, Height = SizeValue.Fixed(_rowHeight) };
        stack.Add(surface);
        stack.Add(after ? new Positioned(line, bottom: 0, start: start, end: 0) : new Positioned(line, top: 0, start: start, end: 0));
        return stack;
    }

    /// <summary>The node rows a carried node is placed among: the sortable's items, in the order the sortable counts
    /// them, the carried one left out, and with depths the rows of its subtree too, which it carries (#1594).</summary>
    private List<TreeViewRow> OthersThan(string carried)
    {
        var others = new List<TreeViewRow>();
        // The carried row's level while the rows of its subtree follow it; 0 once they end.
        var carries = 0;
        foreach (var row in _rows)
        {
            if (carries > 0 && row.Level > carries) continue;
            carries = 0;
            if (row.Node is null) continue;
            if (row.Key == carried)
            {
                if (_indent > 0) carries = row.Level;
                continue;
            }
            others.Add(row);
        }
        return others;
    }

    /// <summary>The parent a place at a depth means (#1594): the ancestor, one level shallower than the depth, of the row
    /// before the place, which is that row itself when the depth is one deeper than it; null at depth 0.</summary>
    private string? ParentAtDepth(List<TreeViewRow> others, int index, int depth)
    {
        if (depth == 0 || index == 0) return null;
        var key = others[index - 1].Key;
        while (LevelOf(key) > depth)
        {
            if (ParentOf(key) is not { } parent) return null;
            key = parent;
        }
        return key;
    }

    private int LevelOf(string key)
    {
        var at = IndexOfNode(key);
        return at >= 0 ? _rows[at].Level : 0;
    }

    /// <summary>The node a visible row shows, by its key.</summary>
    private TreeNode? NodeOf(string? key)
    {
        if (key is null) return null;
        var at = IndexOfNode(key);
        return at >= 0 ? _rows[at].Node : null;
    }

    /// <summary>The sibling a visible node comes after under its parent, or null when it is the first.</summary>
    private TreeNode? SiblingBefore(IReadOnlyList<TreeViewRow> rows, int index, string? parent)
    {
        for (var i = index - 1; i >= 0; i--)
        {
            if (rows[i].Node is not { } node) continue;
            if (node.Key == parent) return null;
            if (rows[i].ParentKey == parent) return node;
        }
        return null;
    }

    /// <summary>
    /// Where a sortable's place stands among the node rows a carried node is placed among: inside the row at
    /// <c>At</c>, before it, or after it as the last; <c>(0, -1, 0)</c> when there is no such row. <c>Level</c> is the
    /// level the node would take, the one its depth chose (#1594) or the row's own. The one reading both the drawn place
    /// and the reported move take, so the two never part.
    /// </summary>
    private static (int Kind, int At, int Level) PlaceOf(SortDrop drop, List<TreeViewRow> others)
    {
        if (drop.Into is { } into)
        {
            for (var i = 0; i < others.Count; i++)
                if (others[i].Key == into) return (DropInto, i, others[i].Level + 1);
            return (0, -1, 0);
        }
        if (drop.Index < others.Count)
            return (DropBefore, drop.Index, drop.Depth is { } depth ? depth + 1 : others[drop.Index].Level);
        if (others.Count == 0) return (0, -1, 0);
        var last = others.Count - 1;
        return (DropAfter, last, drop.Depth is { } after ? after + 1 : others[last].Level);
    }

    /// <summary>
    /// The move a sortable's place means: inside a node, as its first child; between two rows, under the parent the
    /// place's depth means (#1594), or without one the parent of the row after it (of the last row, at the end), after
    /// that parent's last child before the place. Null when the carried node is not a visible one.
    /// </summary>
    private TreeMove? MoveOf(SortDrop drop)
    {
        if (NodeOf(drop.Item) is not { } node) return null;
        var others = OthersThan(drop.Item);
        var (kind, at, _) = PlaceOf(drop, others);
        if (kind == DropInto) return new TreeMove(node, others[at].Node, null);
        // Nothing to land among: the top level, unless the node it was to land inside is gone.
        if (kind == 0) return drop.Into is null ? new TreeMove(node, null, null) : null;
        var index = kind == DropBefore ? at : others.Count;
        var parent = drop.Depth is { } depth ? ParentAtDepth(others, index, depth) : others[at].ParentKey;
        return new TreeMove(node, NodeOf(parent), SiblingBefore(others, index, parent));
    }

    /// <summary>The tree's guard, then the app's: a node never lands inside a leaf, nor inside itself or one of its
    /// descendants.</summary>
    private bool Accepts(SortDrop drop)
    {
        if (MoveOf(drop) is not { } move || move.Parent is { IsLeaf: true }) return false;
        for (var at = move.Parent?.Key; at is not null; at = ParentOf(at))
            if (at == move.Node.Key) return false;
        return CanMove?.Invoke(move) ?? true;
    }

    /// <summary>Where a carried node would land, drawn; and a closed folder it is held inside opens after a pause.</summary>
    private void Hovered(SortDrop? drop)
    {
        string? key = null;
        var kind = 0;
        var level = 0;
        if (drop is not null)
        {
            var others = OthersThan(drop.Item);
            var (placed, at, deep) = PlaceOf(drop, others);
            kind = placed;
            key = at >= 0 ? others[at].Key : null;
            level = deep;
        }
        var pause = kind == DropInto && NodeOf(key) is { } folder && !_open.Contains(folder.Key) && HasChevron(folder)
            ? key
            : null;
        SetState(() =>
        {
            _dropKey = key;
            _dropKind = kind;
            _dropLevel = level;
        });
        if (pause == _pauseKey) return;
        _pauseKey = pause;
        _pauseSerial++;
        if (pause is not null) OpenAfterPause(pause, _pauseSerial);
    }

    private async void OpenAfterPause(string key, int serial)
    {
        await Task.Delay(OpenUnderPauseMs);
        if (serial != _pauseSerial || _pauseKey != key || NodeOf(key) is not { } folder) return;
        SetState(() =>
        {
            Toggle(folder, true);
            _pauseKey = null;
        });
        OnToggled?.Invoke(folder, true);
    }

    /// <summary>A node dropped where the sortable says: the app moves it, unless it is where it already was.</summary>
    private void Dropped(SortDrop drop)
    {
        var move = MoveOf(drop);
        _pauseKey = null;
        _pauseSerial++;
        SetState(() =>
        {
            _dropKey = null;
            _dropKind = 0;
            _dropLevel = 0;
        });
        if (move is null || OnMoved is not { } handler) return;
        var from = ParentOf(move.Node.Key);
        var at = IndexOfNode(move.Node.Key);
        if (from == move.Parent?.Key && at >= 0 && SiblingBefore(_rows, at, from)?.Key == move.After?.Key) return;
        RunMove(handler, move, from);
    }

    /// <summary>The app's move, with the node busy while it runs and its new parent open; then the children the
    /// tree loaded for both parents are read again.</summary>
    private async void RunMove(Func<TreeMove, Task> handler, TreeMove move, string? from)
    {
        var key = move.Node.Key;
        var to = move.Parent;
        var opens = to is not null && !_open.Contains(to.Key);
        SetState(() =>
        {
            _working.Add(key);
            if (opens) Toggle(to!, true);
        });
        if (opens) OnToggled?.Invoke(to!, true);
        var done = false;
        try
        {
            await handler(move);
            done = true;
        }
        catch (Exception)
        {
            done = false;
        }
        SetState(() =>
        {
            _working.Remove(key);
            if (!done) return;
            if (from is not null) ReloadChildren(from);
            if (to is not null && to.Key != from) ReloadChildren(to.Key);
        });
    }

    // ---- The menu per node ------------------------------------------------------------------------

    /// <summary>A node asked for its menu: the app's items for it, open at the pointer's point or under the row,
    /// the node the keyboard's. No menu while a field is open, on a busy node, or with no item.</summary>
    private void RequestMenu(TreeNode node, ContextRequest request)
    {
        if (MenuBuilder is not { } builder || _busy.Contains(node.Key) || _editKey is not null) return;
        var items = builder(node);
        SetState(() =>
        {
            _activeKey = node.Key;
            _activeIsStatus = false;
            _keyboard = request.At is null;
            _menuKey = items.Count > 0 ? node.Key : null;
            _menuAt = request.At;
            _menuItems = items;
            _menuHighlight = MenuPanel.FirstEnabled(items);
        });
    }

    private void ChooseMenu(int index)
    {
        if (_menuKey is not { } key || index < 0 || index >= _menuItems.Count || _menuItems[index].Disabled) return;
        var node = FindNode(key);
        SetState(() => _menuKey = null);
        if (node is not null) OnMenuSelect?.Invoke(node, index);
    }

    private void CloseMenu() => SetState(() => _menuKey = null);

    /// <summary>The composite reports a direction and not a key, and the tree gives each its tree meaning
    /// (WAI-ARIA's tree pattern).</summary>
    private void Move(NavigableMove move)
    {
        if (_rows.Count == 0) return;
        var at = ActiveIndex();
        if (at < 0)
        {
            // The first key lands on the selected node, or on the first one, as the pattern asks.
            var selected = SelectedKey is null ? -1 : IndexOfNode(SelectedKey);
            Activate(selected >= 0 ? selected : 0, null, false);
            return;
        }
        var row = _rows[at];
        switch (move)
        {
            case NavigableMove.NextRow:
                Activate(Math.Min(at + 1, _rows.Count - 1), null, false);
                break;
            case NavigableMove.PreviousRow:
                Activate(Math.Max(at - 1, 0), null, false);
                break;
            case NavigableMove.NextItem:
                if (row.Node is { } closed && HasChevron(closed))
                {
                    if (!_open.Contains(closed.Key)) Activate(at, closed, true);
                    else if (at + 1 < _rows.Count && _rows[at + 1].ParentKey == closed.Key) Activate(at + 1, null, false);
                }
                break;
            case NavigableMove.PreviousItem:
                if (row.Node is { } opened && HasChevron(opened) && _open.Contains(opened.Key))
                    Activate(at, opened, false);
                else if (row.ParentKey is { } parent)
                    Activate(IndexOfNode(parent), null, false);
                break;
            case NavigableMove.RowStart:
                Activate(0, null, false);
                break;
            case NavigableMove.RowEnd:
                Activate(_rows.Count - 1, null, false);
                break;
            case NavigableMove.NextPage:
            case NavigableMove.NextSection:
                Activate(Math.Min(at + RowsPerPage(), _rows.Count - 1), null, false);
                break;
            case NavigableMove.PreviousPage:
            case NavigableMove.PreviousSection:
                Activate(Math.Max(at - RowsPerPage(), 0), null, false);
                break;
        }
    }

    /// <summary>Puts the keyboard on a row, opening or closing <paramref name="toggle"/> on the way.</summary>
    private void Activate(int index, TreeNode? toggle, bool open)
    {
        if (index < 0 || index >= _rows.Count) return;
        var target = _rows[index];
        SetState(() =>
        {
            _activeKey = target.Key;
            _activeIsStatus = target.IsStatus;
            _keyboard = true;
            _scrollToActive = true;
            if (toggle is not null) Toggle(toggle, open);
        });
        if (toggle is not null) OnToggled?.Invoke(toggle, open);
    }

    private int RowsPerPage() => _viewport > 0 && _rowHeight > 0
        ? Math.Max(1, (int)MathF.Floor(_viewport / _rowHeight))
        : RowsPerPageWithoutViewport;

    /// <summary>
    /// Type-ahead: the characters typed within <see cref="SearchPauseMs"/> of each other, matched against
    /// the start of each visible node's label, case-insensitively, from the item after the keyboard's
    /// (or from it, while a longer search narrows), wrapping around. One letter typed again cycles
    /// through the items that start with it.
    /// </summary>
    private void Search(string typed)
    {
        if (_rows.Count == 0) return;
        var now = DateTime.UtcNow;
        _search = (now - _searchAt).TotalMilliseconds <= SearchPauseMs ? _search + typed : typed;
        _searchAt = now;

        var prefix = _search;
        var repeated = true;
        for (var i = 1; i < prefix.Length; i++)
            if (char.ToLowerInvariant(prefix[i]) != char.ToLowerInvariant(prefix[0])) repeated = false;
        if (repeated) prefix = prefix.Substring(0, 1);

        var at = ActiveIndex();
        var start = prefix.Length > 1 ? Math.Max(at, 0) : at + 1;
        for (var step = 0; step < _rows.Count; step++)
        {
            var index = (start + step) % _rows.Count;
            if (_rows[index].Node is { } node && node.Label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                Activate(index, null, false);
                return;
            }
        }
    }
}
