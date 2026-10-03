using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Coverage;

/// <summary>
/// A test reads an assembly's metadata through <see cref="TestReferences"/> alone. A
/// <c>MetadataReference.CreateFromFile</c> copies the assembly into native memory the GC does not count,
/// and the tests that made one per compilation held up to 35 GB in one test host (#481). The guard
/// parses every C# file under <c>tests/</c> and looks for a CALL of the factory, however it is spaced or
/// broken across lines, so a new test that reaches for it fails here, named, and a mention of it in a
/// comment or a string does not.
/// </summary>
public class TestReferencesGuardTests
{
    private const string Owner = "tests/Shared/TestReferences.cs";

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "eQuantic.UI.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    /// <summary>The calls of <c>CreateFromFile</c> in <paramref name="source"/>, by the syntax tree:
    /// an invocation whose callee's last name is the factory's, whatever trivia separates them.</summary>
    internal static int FactoryCalls(string source) =>
        CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Count(invocation => invocation.Expression switch
            {
                MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText == "CreateFromFile",
                IdentifierNameSyntax name => name.Identifier.ValueText == "CreateFromFile",
                _ => false,
            });

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
            .Where(file => FactoryCalls(File.ReadAllText(Path.Combine(root, file))) > 0)
            .ToList();

        copies.Should().BeEmpty(
            $"a test reads an assembly through {Owner} (TestReferences.Of), which keeps one reference per "
            + "assembly for the process, never through MetadataReference.CreateFromFile, which copies the "
            + "assembly into native memory for every call (#481)");
        File.Exists(Path.Combine(root, Owner)).Should().BeTrue("the guard names the owner it protects");
    }

    /// <summary>The guard's own reading, both ways: a call is found however it is spaced, and a mention
    /// that runs nothing is not.</summary>
    [Theory]
    [InlineData("var r = MetadataReference.CreateFromFile(path);", 1)]
    [InlineData("var r = MetadataReference.CreateFromFile (path);", 1)]
    [InlineData("var r = MetadataReference\n    .CreateFromFile(\n        path);", 1)]
    [InlineData("var r = Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(path);", 1)]
    [InlineData("// MetadataReference.CreateFromFile(path) copies the assembly", 0)]
    [InlineData("var text = \"MetadataReference.CreateFromFile(path)\";", 0)]
    public void TheGuard_FindsACallAndNothingElse(string statement, int calls)
    {
        FactoryCalls($"class C {{ void M(string path) {{ {statement} }} }}").Should().Be(calls);
    }
}
