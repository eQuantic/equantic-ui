namespace eQuantic.UI.Server;

/// <summary>Where an asset route's name is looked for on disk.</summary>
internal static class AssetPaths
{
    private static readonly char[] Separators = ['/', '\\'];

    /// <summary>
    /// The file a route's name resolves to inside <paramref name="directory"/>, or null when the name
    /// is not a plain file name. A route value is the request's text, and these routes serve anyone
    /// (#673): a separator in it (on Windows an encoded backslash decodes into one) or a rooted name
    /// combined into a path outside the directory (found by Copilot on #686). Both separators are
    /// refused on every OS, so the rule is the same wherever the app runs, and the resolved path is
    /// checked against the directory besides.
    /// </summary>
    public static string? Resolve(string directory, string? name, string extension)
    {
        if (string.IsNullOrEmpty(name) || name.IndexOfAny(Separators) >= 0 || Path.IsPathRooted(name)) return null;
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;
        var file = Path.GetFullPath(Path.Combine(root, name + extension));
        return file.StartsWith(root, StringComparison.Ordinal) ? file : null;
    }
}
