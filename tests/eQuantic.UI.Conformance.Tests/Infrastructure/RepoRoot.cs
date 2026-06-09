using System.IO;

namespace eQuantic.UI.Conformance.Tests.Infrastructure;

/// <summary>
/// Locates the repository root (the directory containing eQuantic.UI.sln) by walking up
/// from the test assembly's base directory.
/// </summary>
internal static class RepoRoot
{
    private static string? _cached;

    public static string? Find()
    {
        if (_cached != null) return _cached;

        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "eQuantic.UI.sln")))
            {
                _cached = dir.FullName;
                return _cached;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
