using FluentAssertions;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The repository's root, found by walking up from the test assembly to the folder that holds the
/// runtime's sources: the fixtures the runtime's own specs read live there, and a test that pins one
/// reads the same file.
/// </summary>
internal static class RepoRoot
{
    public static string Find()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;
        here.Should().NotBeNull("the suite runs inside the repository");
        return here!.FullName;
    }
}
