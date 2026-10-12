namespace SharedLibrary.Domain;

/// <summary>
/// Server code beside the types a page uses: it reads a file, which a browser cannot. No page names
/// it, so the build never compiles it for the browser and has nothing to say about it. A project
/// holds both kinds side by side, and only what the browser code reaches crosses.
/// </summary>
public sealed class ReceiptArchive(string root)
{
    public IReadOnlyList<string> Lines(string name) => File.ReadAllLines(Path.Combine(root, name));

    public bool Holds(string name) => File.Exists(Path.Combine(root, name));
}
