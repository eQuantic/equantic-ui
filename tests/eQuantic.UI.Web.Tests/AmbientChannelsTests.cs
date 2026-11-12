using System.Runtime.CompilerServices;
using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The ambient channels the SDK keeps (docs/ELEMENT-TREE-PLAN.md, 3.5): every <c>AsyncLocal</c> field in
/// its source, named, each with the reason it is still one. An <c>AsyncLocal</c> is read by any code
/// running in the flow that armed it, and overwritten by any code there too, so what belongs to one
/// tree or one render is handed to it instead: the web's five render channels became one object a
/// render owns (#1404).
/// <para>
/// The list may only SHRINK. A new channel fails here until its entry is written by hand with its
/// reason, and a channel that goes fails until its entry goes with it, so the list is always the
/// source's own.
/// </para>
/// </summary>
public class AmbientChannelsTests
{
    /// <summary>Each channel the source keeps, as <c>Type.field</c>, and why it is still one.</summary>
    private static readonly IReadOnlyDictionary<string, string> Kept = new Dictionary<string, string>
    {
        ["CapabilityScope.Scoped"] =
            "the request's container; it becomes the root element's services, and the server's middleware keeps "
            + "the channel for server actions, which run outside any tree",
        ["ComponentBoundary._diagnostics"] = "the error boundary's, which moves to the owner with E4c (#1452)",
        ["ComponentBoundary._report"] = "the error boundary's, which moves to the owner with E4c (#1452)",
        ["ComponentBoundary._contained"] = "the error boundary's, which moves to the owner with E4c (#1452)",
        ["FaceResolution._unresolved"] = "stays: the text services' diagnostic, outside the tree",
        ["RenderChannels.Bridging"] =
            "stays: the escape hatch's bridge, inside an HtmlElement's Render(), which takes no parameter, "
            + "finds its render's channels here",
    };

    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    /// <summary>Every source file under <c>src/</c>, with what the build writes under <c>obj/</c> and
    /// <c>bin/</c> left out.</summary>
    private static IReadOnlyList<string> SourceFiles()
    {
        var separator = Path.DirectorySeparatorChar;
        return Directory.GetFiles(Path.Combine(RepoRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
                && !path.Contains($"{separator}bin{separator}", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The <c>AsyncLocal</c> fields declared in <paramref name="path"/>, as <c>Type.field</c>.</summary>
    private static IEnumerable<string> ChannelsIn(string path)
    {
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(path), ParseDefaults.Options, path).GetRoot();
        return root.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .Where(field => IsAsyncLocal(field.Declaration.Type))
            .SelectMany(field => field.Declaration.Variables.Select(variable =>
                $"{field.Ancestors().OfType<BaseTypeDeclarationSyntax>().First().Identifier.Text}.{variable.Identifier.Text}"));
    }

    private static bool IsAsyncLocal(TypeSyntax type) => type switch
    {
        GenericNameSyntax generic => generic.Identifier.Text == "AsyncLocal",
        QualifiedNameSyntax qualified => IsAsyncLocal(qualified.Right),
        AliasQualifiedNameSyntax aliased => IsAsyncLocal(aliased.Name),
        _ => false,
    };

    [Fact]
    public void EveryAmbientChannel_IsOneTheListNamesWithItsReason()
    {
        var files = SourceFiles();
        var found = files.SelectMany(ChannelsIn).ToList();

        files.Should().HaveCountGreaterThan(1000, "the scan reads the SDK's whole source, not a corner of it");
        found.Should().Contain("RenderChannels.Bridging", "the scan finds a channel it knows is there");
        found.Should().OnlyHaveUniqueItems();
        found.Should().BeEquivalentTo(Kept.Keys,
            "a new channel is named here with its reason, and one that goes leaves the list with it");
    }

    [Fact]
    public void TheWebRenderChannels_AreNoLongerAmbient()
    {
        var web = SourceFiles()
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}eQuantic.UI.Web{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
            .SelectMany(ChannelsIn)
            .ToList();

        web.Should().Equal(["RenderChannels.Bridging"],
            "a render's sinks, scope, density and link policy are handed to it, and the bridge is the one reader of an ambient");
    }
}
