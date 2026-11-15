using eQuantic.UI.Components;
using eQuantic.UI.Lucide;
using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// A repository's files in a <c>TreeView</c> (#1530): the top folder arrives with the page, and every
/// folder below it loads through a server action the first time it opens. Tab reaches the tree, the
/// arrows walk and open it, Enter opens a file, and typing a name's first letters jumps to it. The
/// <c>flaky</c> folder fails its first load, to show the item that loads it again.
/// </summary>
[Page("/files", Title = "Files — eQuantic Console")]
public sealed class FilesScreen : StatefulComponent, IServerPrefetch
{
    /// <summary>The repository's top folder, listed by the server before the page exists.</summary>
    public List<RepositoryEntry> Top { get; private set; } = [];

    private string? _open;

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
                + "walk and open it, Enter opens a file, and typing a name jumps to it.",
                TypeRole.BodyM, theme.TextSecondary),
            Row(gap: Space.S4, children: [
                Box(new BoxStyle
                {
                    Width = 320,
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
                Text(_open is null ? "Press a file to open it." : $"Open: {_open}",
                    TypeRole.BodyL, _open is null ? theme.TextSecondary : theme.TextPrimary),
            ]),
        ]));
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
