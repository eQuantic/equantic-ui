namespace eQuantic.UI.Server;

/// <summary>Where an asset route's name is looked for on disk.</summary>
internal static class AssetPaths
{
    /// <summary>The file a route's name resolves to inside <paramref name="directory"/>.</summary>
    public static string? Resolve(string directory, string? name, string extension) =>
        Path.Combine(directory, $"{name}{extension}");
}
