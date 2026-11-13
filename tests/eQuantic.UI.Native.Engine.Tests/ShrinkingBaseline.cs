namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A baseline that may ONLY SHRINK, as the semantics parity fixture and the variant matrix keep theirs: one
/// entry a line, running to the first two spaces, and its reason after them. An entry is written by hand;
/// the update keeps the reasons of the entries that still occur and drops the rest, and never adds one.
/// </summary>
internal static class ShrinkingBaseline
{
    /// <summary>The baseline file of <paramref name="name"/> under this project's <c>Coverage</c> folder.</summary>
    public static string PathOf(string name) =>
        Path.Combine(RepositoryRoot(), "tests", "eQuantic.UI.Native.Engine.Tests", "Coverage", name);

    /// <summary>Each entry and its reason.</summary>
    public static Dictionary<string, string> Read(string path)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path)) return entries;
        foreach (var line in File.ReadLines(path))
        {
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var split = line.IndexOf("  ", StringComparison.Ordinal);
            if (split < 0) entries[line] = "";
            else entries[line[..split]] = line[(split + 2)..];
        }
        return entries;
    }

    /// <summary>The file again, its header comment and the entries <paramref name="kept"/>, in order.</summary>
    public static void Write(string path, IEnumerable<KeyValuePair<string, string>> kept)
    {
        var header = File.ReadLines(path).TakeWhile(line => line.StartsWith('#')).ToList();
        File.WriteAllLines(path, header.Concat(kept.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}  {pair.Value}")));
    }

    /// <summary>The repository's root: the first folder above the build output with a Directory.Build.props.</summary>
    public static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props"))) dir = dir.Parent;
        return dir!.FullName;
    }
}
