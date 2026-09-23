namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The repository this suite runs inside, found from the test's own output directory. The tests
/// that compare what a build wrote, or what a record in the tree says, read files by their
/// repository path.
/// </summary>
internal static class Repository
{
    internal static string Root()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;
        here.Should().NotBeNull("the suite runs inside the repository");
        return here!.FullName;
    }
}
