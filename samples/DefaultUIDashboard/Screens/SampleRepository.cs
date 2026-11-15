using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// The repository the files screen browses, held by the server: folders, in the order a listing gives
/// them, and their files. It answers a folder at a time, a little slowly, so the tree shows its
/// loading item, and the <c>flaky</c> folder fails the first time it is asked, so the tree shows the
/// item that loads it again.
/// </summary>
[ServerOnly]
public static class SampleRepository
{
    private static readonly Dictionary<string, string[]> Folders = new(StringComparer.Ordinal)
    {
        [""] = ["src/", "docs/", "samples/", "flaky/", "README.md", "LICENSE"],
        ["src"] = ["Primitives/", "Components/", "Web/", "Directory.Build.props"],
        ["src/Primitives"] = ["Nodes/", "Theme/", "VisualNode.cs"],
        ["src/Primitives/Nodes"] = ["Navigable.cs", "Pressable.cs", "TreePlace.cs"],
        ["src/Primitives/Theme"] = ["IAppTheme.cs", "Tokens.cs"],
        ["src/Components"] = ["TreeView.cs", "TreeNode.cs", "Menu.cs", "Select.cs"],
        ["src/Web"] = ["WebLoweringVisitor.cs", "WebRealizer.cs"],
        ["docs"] = ["PRINCIPLES.md", "PROVING.md", "FLUTTER-PARITY.md"],
        ["samples"] = ["DefaultUIDashboard/", "PhotonDesktop/"],
        ["samples/DefaultUIDashboard"] = ["Program.cs", "Screens/"],
        ["samples/DefaultUIDashboard/Screens"] = ["FilesScreen.cs", "PaymentsPage.cs"],
        ["samples/PhotonDesktop"] = ["Program.cs"],
        ["flaky"] = ["second-time-lucky.txt"],
    };

    private static int _flakyCalls;

    /// <summary>The entries of a folder, by its path from the repository's root ("" for the root).</summary>
    public static async Task<List<RepositoryEntry>> ListAsync(string path)
    {
        await Task.Delay(300);
        if (path == "flaky" && Interlocked.Increment(ref _flakyCalls) == 1)
            throw new IOException("The folder could not be read.");
        if (!Folders.TryGetValue(path, out var names)) return [];
        return names
            .Select(name =>
            {
                var directory = name.EndsWith('/');
                var bare = directory ? name[..^1] : name;
                return new RepositoryEntry(path.Length == 0 ? bare : $"{path}/{bare}", bare, directory);
            })
            .ToList();
    }
}
