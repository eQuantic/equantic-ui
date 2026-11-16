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
/// </summary>
[Page("/files", Title = "Files — eQuantic Console")]
public sealed class FilesScreen : StatefulComponent, IServerPrefetch
{
    /// <summary>The repository's top folder, listed by the server before the page exists.</summary>
    public List<RepositoryEntry> Top { get; private set; } = [];

    private string? _open;
    private string? _said;

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
                + "walk and open it, Enter opens a file, and typing a name jumps to it. The open file has a menu: "
                + "right-click it, hold it, or press the menu key on it. Drag the gap beside the tree to resize it.",
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
                    roots: Top.Select(Node).ToList(),
                    selectedKey: _open,
                    onPressed: Open,
                    loadChildren: Load,
                    label: "Repository",
                    height: SizeValue.Fill)),
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
        return entries.Select(Node).ToList();
    }

    private void Open(TreeNode node)
    {
        if (node.IsLeaf) SetState(() => _open = node.Key);
    }
}
