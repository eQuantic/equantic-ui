using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Coverage;

/// <summary>
/// A test reads an assembly's metadata through <see cref="TestReferences"/> alone. A
/// <c>MetadataReference.CreateFromFile</c> copies the assembly into native memory the GC does not count,
/// and the tests that made one per compilation held up to 32.6 GB in one test host (#481). The guard
/// reads every C# file under <c>tests/</c>, so a new test that reaches for the factory fails here,
/// named, instead of putting the gigabytes back one test at a time.
/// </summary>
public class TestReferencesGuardTests
{
    private const string Owner = "tests/Shared/TestReferences.cs";

    /// <summary>The factory's call, spelled in two parts so this file is not its own finding.</summary>
    private static readonly string Factory = string.Concat("CreateFromFile", "(");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "eQuantic.UI.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    [Fact]
    public void NoTestReadsAnAssemblyButThroughTheOwner()
    {
        var root = RepoRoot();
        var tests = Path.Combine(root, "tests");
        var separator = Path.DirectorySeparatorChar;

        var copies = Directory.EnumerateFiles(tests, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{separator}bin{separator}") && !file.Contains($"{separator}obj{separator}"))
            .Select(file => Path.GetRelativePath(root, file).Replace(separator, '/'))
            .Where(file => file != Owner)
            .Where(file => File.ReadAllText(Path.Combine(root, file)).Contains(Factory, StringComparison.Ordinal))
            .ToList();

        copies.Should().BeEmpty(
            $"a test reads an assembly through {Owner} (TestReferences.Of), which "
            + "keeps one reference per assembly for the process, never through MetadataReference.CreateFromFile, "
            + "which copies the assembly into native memory for every call (#481)");
        File.Exists(Path.Combine(root, Owner)).Should().BeTrue("the guard names the owner it protects");
    }
}
