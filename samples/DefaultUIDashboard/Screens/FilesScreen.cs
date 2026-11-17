using eQuantic.UI.Components;
using eQuantic.UI.Lucide;
using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// A repository's files in a <c>TreeView</c> (#1530): the top folder arrives with the page, and every
/// folder below it loads through a server action the first time it opens. Tab reaches the tree, the
/// arrows walk and open it, Enter opens a file, and typing a name's first letters jumps to it. The
/// <c>flaky</c> folder fails its first load, to show the item that loads it again. The open file has a context
/// menu (#1531).
/// <para>
/// Every node has a menu, and the tree edits in place (#1532): F2 or "Rename" renames, "New file" and "New folder"
/// create under a folder, and "Delete" deletes, each busy for a moment as a server would make it. The changes are
/// this page's, laid over what the server lists: the sample's repository is every visitor's.
/// </para>
/// <para>
/// A node moves by drag, or by Space and the arrows on the keyboard's node (#1533): before, after or inside another,
/// never into the read-only <c>samples</c> folder nor out of it.
/// </para>
/// </summary>
[Page("/files", Title = "Files — eQuantic Console")]
public sealed class FilesScreen : StatefulComponent, IServerPrefetch
{
    /// <summary>The repository's top folder, listed by the server before the page exists.</summary>
    public List<RepositoryEntry> Top { get; private set; } = [];

    private string? _open;
    private string? _said;

    // This page's changes, laid over the server's listing.
    private readonly Dictionary<string, string> _renamed = new();
    private readonly Dictionary<string, List<RepositoryEntry>> _added = new();
    private readonly HashSet<string> _deleted = new();
    private List<MovedEntry> _moves = [];

    private TreeEditRequest? _edit;
    private TreeReloadRequest? _reload;
    private int _requests;
    private bool _creatingFolder;
    private List<string> _busy = [];

    /// <summary>How wide the tree is: the gap beside it drags it wider or narrower (#1562).</summary>
    private float _treeWidth = 320;

    /// <summary>Where across the gap the press took it, so the gap stays under the pointer as it moves.</summary>
    private float? _grabbedAt;

    [ServerOnly]
    public async Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        Top = await SampleRepository.ListAsync("");

    [ServerAction]
    public Task<List<RepositoryEntry>> ListFolder(string path) => SampleRepository.ListAsync(path);

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        return Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
            Padding = EdgeInsets.All(Space.S6),
        },
        Column(gap: Space.S4, children: [
            Text("Files", TypeRole.Heading, theme.TextPrimary),
            Text("Folders load from the server the first time they open. Tab reaches the tree; the arrows "
                + "walk and open it, Enter opens a file, and typing a name jumps to it. Every node has a menu: "
                + "right-click it, hold it, or press the menu key on it. F2 renames. Drag a node, or press Space "
                + "on it and walk it with the arrows, to move it. Drag the gap beside the tree to resize it.",
                TypeRole.BodyM, theme.TextSecondary),
            Row(gap: 0, children: [
                Box(new BoxStyle
                {
                    Width = _treeWidth,
                    Height = 440,
                    Padding = EdgeInsets.All(Space.S2),
                    BorderWidth = 1,
                    BorderColor = theme.Border,
                    CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
                },
                TreeView(
                    roots: Apply("", Top).Select(Node).ToList(),
                    selectedKey: _open,
                    onPressed: Open,
                    loadChildren: Load,
                    label: "Repository",
                    height: SizeValue.Fill,
                    onRenamed: Rename,
                    onCreated: Create,
                    edit: _edit,
                    reload: _reload,
                    busyKeys: _busy,
                    menuBuilder: MenuFor,
                    onMenuSelect: Choose,
                    onMoved: Move,
                    canMove: CanMove)),
                Splitter(),
                Column(gap: Space.S2, children: [
                    OpenFile(context),
                    Text(_said ?? "", TypeRole.BodyM, theme.TextSecondary),
                ]),
            ]),
        ]));
    }

    /// <summary>
    /// The gap between the tree and the file, which a pointer drags to resize the tree (#1562). The listener is
    /// <see cref="HitTestBehavior.Opaque"/> because the gap draws nothing, where a deferring one would hear nothing,
    /// and it follows the press wherever it goes, so a drag that runs past the gap still resizes.
    /// </summary>
    private VisualNode Splitter() =>
        Listener(Box(new BoxStyle { Width = Space.S4, Height = 440, Cursor = PointerCursor.ColResize }),
            onPointerDown: e => _grabbedAt = e.Position.X,
            onPointerMove: e =>
            {
                if (_grabbedAt is not { } grabbed) return;
                SetState(() => _treeWidth = Math.Clamp(_treeWidth + e.Position.X - grabbed, 200, 560));
            },
            onPointerUp: _ => _grabbedAt = null,
            onPointerCancel: _ => _grabbedAt = null,
            behavior: HitTestBehavior.Opaque);

    /// <summary>
    /// The open file, whose menu a right-click, a long press or the menu key opens (#1531): where the pointer
    /// asked, or under the card when the keyboard did.
    /// </summary>
    private VisualNode OpenFile(ComponentContext context)
    {
        var theme = context.Theme;
        var card = Pressable(Box(new BoxStyle
        {
            Width = 320,
            Padding = EdgeInsets.All(Space.S3),
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Small)),
        },
        Text(_open is null ? "Press a file to open it." : $"Open: {_open}",
            TypeRole.BodyL, _open is null ? theme.TextSecondary : theme.TextPrimary)), () => { });
        return ContextMenu(card,
            [new MenuItem("Copy path"), new MenuItem("Close") { Destructive = true }],
            index => SetState(() =>
            {
                if (index == 0) _said = $"Copied {_open}";
                else (_open, _said) = (null, "Closed the file.");
            }),
            enabled: _open is not null);
    }

    private static TreeNode Node(RepositoryEntry entry) => new(entry.Path, entry.Name)
    {
        Icon = entry.IsDirectory ? LucideIcons.Folder : LucideIcons.File,
        IsLeaf = !entry.IsDirectory,
    };

    private async Task<IReadOnlyList<TreeNode>> Load(TreeNode folder)
    {
        var entries = await ListFolder(folder.Key);
        return Apply(folder.Key, entries).Select(Node).ToList();
    }

    /// <summary>A folder's entries as this page left them: renamed, added to, deleted from, moved.</summary>
    private List<RepositoryEntry> Apply(string folder, List<RepositoryEntry> entries)
    {
        var shown = new List<RepositoryEntry>();
        foreach (var entry in entries)
            if (!_deleted.Contains(entry.Path) && !Moved(entry.Path)) shown.Add(Named(entry));
        if (_added.TryGetValue(folder, out var added))
            foreach (var entry in added)
                if (!_deleted.Contains(entry.Path) && !Moved(entry.Path)) shown.Add(Named(entry));
        // What this page moved here, each after the sibling it was dropped after.
        foreach (var moved in _moves)
        {
            if (moved.Parent != folder || _deleted.Contains(moved.Entry.Path)) continue;
            var at = 0;
            if (moved.After is { } after)
                for (var i = 0; i < shown.Count; i++)
                    if (shown[i].Path == after) at = i + 1;
            shown.Insert(at, Named(moved.Entry));
        }
        return shown;
    }

    private RepositoryEntry Named(RepositoryEntry entry) =>
        _renamed.TryGetValue(entry.Path, out var name) ? entry with { Name = name } : entry;

    private bool Moved(string path) => _moves.Any(moved => moved.Entry.Path == path);

    /// <summary>The samples are read-only: the folder stays where it is, nothing lands in it and nothing leaves it.</summary>
    private static bool CanMove(TreeMove move) =>
        !InSamples(move.Node.Key) && !(move.Parent is { } parent && InSamples(parent.Key));

    private static bool InSamples(string path) => path == "samples" || path.StartsWith("samples/", StringComparison.Ordinal);

    /// <summary>A move as a server makes it: a moment's work, then the node shown where it was dropped.</summary>
    private async Task Move(TreeMove move)
    {
        await Task.Delay(400);
        var node = move.Node;
        var entry = new RepositoryEntry(node.Key, node.Label, !node.IsLeaf);
        SetState(() =>
        {
            _moves = _moves.Where(moved => moved.Entry.Path != node.Key).ToList();
            _moves.Add(new MovedEntry(entry, move.Parent?.Key ?? "", move.After?.Key));
            _said = move.Parent is { } parent ? $"Moved {node.Label} into {parent.Label}." : $"Moved {node.Label} to the top.";
        });
    }

    private static IReadOnlyList<MenuItem> MenuFor(TreeNode node) => node.IsLeaf
        ? [new MenuItem("Rename"), new MenuItem("Copy path"), new MenuItem("Delete") { Destructive = true }]
        : [new MenuItem("New file"), new MenuItem("New folder"), new MenuItem("Rename"),
            new MenuItem("Delete") { Destructive = true }];

    private void Choose(TreeNode node, int index)
    {
        switch (MenuFor(node)[index].Label)
        {
            case "Rename":
                SetState(() => _edit = new TreeEditRequest(++_requests, node.Key));
                break;
            case "New file":
            case "New folder":
                var folder = MenuFor(node)[index].Label == "New folder";
                SetState(() =>
                {
                    _creatingFolder = folder;
                    _edit = new TreeEditRequest(++_requests, node.Key)
                    {
                        Creates = true,
                        Icon = folder ? LucideIcons.Folder : LucideIcons.File,
                    };
                });
                break;
            case "Copy path":
                SetState(() => _said = $"Copied {node.Key}");
                break;
            case "Delete":
                Delete(node);
                break;
        }
    }

    /// <summary>A rename as a server makes it: a moment's work, then the name this page shows.</summary>
    private async Task Rename(TreeNode node, string name)
    {
        await Task.Delay(400);
        SetState(() =>
        {
            _renamed[node.Key] = name;
            _said = $"Renamed {node.Label} to {name}.";
        });
    }

    private async Task Create(TreeNode folder, string name)
    {
        // The kind asked for, read before the work: another creation may start before this one ends.
        var isFolder = _creatingFolder;
        await Task.Delay(400);
        var path = $"{folder.Key}/{name}";
        SetState(() =>
        {
            if (!_added.TryGetValue(folder.Key, out var added)) _added[folder.Key] = added = [];
            added.Add(new RepositoryEntry(path, name, isFolder));
            _said = $"Created {path}.";
        });
    }

    /// <summary>A deletion the app makes itself: the node busy for a moment, then gone, and its folder read again.</summary>
    private async void Delete(TreeNode node)
    {
        SetState(() => _busy = [node.Key]);
        await Task.Delay(400);
        var cut = node.Key.LastIndexOf('/');
        var parent = cut < 0 ? "" : node.Key.Substring(0, cut);
        SetState(() =>
        {
            _deleted.Add(node.Key);
            _busy = [];
            if (_open == node.Key) _open = null;
            _reload = new TreeReloadRequest(++_requests, [parent]);
            _said = $"Deleted {node.Key}.";
        });
    }

    private void Open(TreeNode node)
    {
        if (node.IsLeaf) SetState(() => _open = node.Key);
    }
}
