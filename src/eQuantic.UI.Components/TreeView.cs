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
/// keyboard on it. v1 fences: one selected node, no virtualization, no theme slot of its own.
/// </para>
/// </summary>
public sealed class TreeView : StatefulComponent
{
    /// <summary>How long a pause ends a type-ahead search, the WAI-ARIA examples' own figure.</summary>
    private const double SearchPauseMs = 700;

    /// <summary>The rows a page moves by when the tree does not know its viewport.</summary>
    private const int RowsPerPageWithoutViewport = 10;

    private static readonly IReadOnlyList<TreeNode> NoChildren = new List<TreeNode>();

    private readonly HashSet<string> _open = new();
    private readonly Dictionary<string, IReadOnlyList<TreeNode>> _loaded = new();
    private readonly HashSet<string> _loading = new();
    private readonly HashSet<string> _failed = new();

    /// <summary>The loads whose loader has been called: a load marked during a build waits for the tree
    /// to be mounted, and the server's one-frame render, which never mounts it, calls no loader.</summary>
    private readonly HashSet<string> _started = new();
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

    private bool _scrollToActive;
    private float _offset;
    private float _viewport;
    private float _rowHeight;
    private int _scrollVersion;
    private ScrollRequest? _scrollRequest;

    public TreeView(IReadOnlyList<TreeNode> roots, string? selectedKey = null, Action<TreeNode>? onPressed = null)
    {
        Roots = roots;
        SelectedKey = selectedKey;
        OnPressed = onPressed;
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

        _rowHeight = ControlMetrics.Height(context, SizeVariant.Small);
        _rows = Flatten();
        // A status item that went away (its load finished) hands the keyboard to the node it stood under,
        // which is where the person was.
        if (_activeIsStatus && _activeKey is { } statusKey && !HasStatusRow(statusKey)) _activeIsStatus = false;
        var active = ActiveIndex();

        var items = new List<VisualNode>();
        for (var i = 0; i < _rows.Count; i++)
            items.Add(BuildRow(context, _rows[i], ringed: i == active && _keyboard));

        VisualNode tree = new Navigable(Move, items)
        {
            Role = NavigableRole.Tree,
            Label = Label,
            ActiveCell = active >= 0 ? (active, 0) : null,
            OnTextSearch = Search,
        };
        // Enter presses the keyboard's item. Scoped to the tree, so two trees on a screen never both
        // answer it, and live only while there is an item to press.
        tree = new Shortcut(tree, KeyChord.Enter, PressActive) { FocusScoped = true, Enabled = active >= 0 };

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

    private VisualNode BuildRow(ComponentContext context, TreeViewRow row, bool ringed)
    {
        var theme = context.Theme;
        var node = row.Node;
        var chevron = node is not null && HasChevron(node);
        var open = node is not null && _open.Contains(node.Key);
        var selected = node is not null && SelectedKey is not null && node.Key == SelectedKey;
        var failed = node is null && _failed.Contains(row.Key);

        var content = new Row(gap: Space.S1) { Cross = CrossAlign.Center, Width = SizeValue.Fill, Height = SizeValue.Fill };
        // The chevron's slot is always there, so a leaf's label lines up with its siblings'.
        content.Add(new Box(new BoxStyle { Width = SizeValue.Fixed(IconSize.Dense), Height = SizeValue.Fixed(IconSize.Dense) },
            chevron
                ? new Icon(open ? Icons.ChevronDown : Icons.ChevronRight, IconSize.Dense, theme.TextSecondary)
                : null));
        if (node?.Icon is { } glyph)
            content.Add(new Icon(glyph, IconSize.Dense, theme.TextSecondary));
        else if (failed)
            content.Add(new Icon(Icons.Refresh, IconSize.Dense, theme.TextSecondary));
        var label = node?.Label ?? (failed ? SdkStrings.TreeLoadFailed : SdkStrings.TreeLoading);
        content.Add(new Flexible(new Text(label, TypeRole.BodyM, node is null ? theme.TextSecondary : theme.TextPrimary,
            maxLines: 1)));

        var quietFill = theme.List?.QuietFill ?? theme.SurfaceSubtle;
        var surface = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fixed(_rowHeight),
            // Indented by its level, so the depth a reader hears is the depth a person sees.
            Padding = new EdgeInsets(Space.S2 + (row.Level - 1) * Space.S4, 0, Space.S2, 0),
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Small)),
            Background = selected ? theme.Colors(Variant.Primary).Subtle : null,
            // The ring is always drawn, transparent unless the keyboard is here, so it never moves the
            // label when it appears.
            BorderWidth = 2,
            BorderColor = ringed ? theme.FocusRing : new ColorToken(Color.Transparent),
            // NOT on the selected row, whose fill IS its state (the list's rule, #616).
            Hover = selected ? null : new StyleDiff { Background = quietFill },
            Pressed = selected ? null : new StyleDiff { Background = quietFill },
        }, content);

        return new Pressable(surface, () => Press(row, fromKeyboard: false))
        {
            Role = PressableRole.TreeItem,
            Label = label,
            // A status item is not a node: it is neither picked nor opened.
            Selected = node is null ? null : selected,
            Expanded = chevron ? open : null,
            TreePlace = new TreePlace(row.Level, row.Position, row.Count),
        };
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
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            rows.Add(new TreeViewRow(node.Key, node, parent, level, i + 1, nodes.Count));
            if (!_open.Contains(node.Key)) continue;
            var children = ChildrenOf(node);
            if (children is not null)
            {
                AddLevel(rows, children, node.Key, level + 1);
                continue;
            }
            // Open and still to load: an app's ExpandedKeys can name such a node before any press did.
            if (!_loading.Contains(node.Key) && !_failed.Contains(node.Key)) StartLoad(node);
            rows.Add(new TreeViewRow(node.Key, null, node.Key, level + 1, 1, 1));
        }
    }

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
            });
        }
        catch (Exception)
        {
            SetState(() =>
            {
                _loading.Remove(node.Key);
                _started.Remove(node.Key);
                _failed.Add(node.Key);
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
        if (at >= 0) Press(_rows[at], fromKeyboard: true);
    }

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
