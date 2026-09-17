using System.Runtime.CompilerServices;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web.Build;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Pins the runtime's <c>node-kinds.generated.ts</c> byte-for-byte to the C# generator, and holds
/// the three rules that make a wire kind an IDENTITY rather than a label.
///
/// <para>
/// This union is the browser's half of "one door per node". The C# realizers are closing over a
/// visitor, where a new node is a method that must be filled in; the client cannot have one, because
/// class names do not survive bundling and dispatch is by string. Typing that string as this union
/// is what lets <c>lowerNodeKind</c> end in <c>assertNever</c> — so a node added to the vocabulary
/// stops the runtime's build instead of falling into a default arm and rendering nothing.
/// </para>
///
/// <para>
/// Which is why a DRIFTED union would be worse than the bare <c>string</c> it replaces: it would
/// make the browser's switch look exhaustive while a kind it has never heard of walks past. The
/// pin is what makes that impossible. Refresh with <c>EQ_UPDATE_NODE_KINDS_TS=1</c>.
/// </para>
/// </summary>
public class NodeKindTsGeneratorTests
{
    private static string GeneratedFilePath([CallerFilePath] string sourcePath = "")
    {
        var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));
        return Path.Combine(repoRoot, "src", "eQuantic.UI.Runtime", "src", "shared", "node-kinds.generated.ts");
    }

    [Fact]
    public void GeneratedModule_MatchesCommittedFile()
    {
        var generated = NodeKindTsGenerator.Generate();
        var path = GeneratedFilePath();

        if (Environment.GetEnvironmentVariable("EQ_UPDATE_NODE_KINDS_TS") == "1")
        {
            File.WriteAllText(path, generated);
            return;
        }

        File.Exists(path).Should().BeTrue(
            $"the runtime ships the generated union at {path} — run once with EQ_UPDATE_NODE_KINDS_TS=1");
        File.ReadAllText(path).Should().Be(generated,
            "the union mirrors VisualNode.NodeKind and is regenerated, never edited "
            + "(EQ_UPDATE_NODE_KINDS_TS=1 dotnet test eQuantic.UI.Web.Tests)");
    }

    /// <summary>
    /// The union is the WHOLE vocabulary and one word more. The count is asked of the assembly on
    /// both sides, because a union that silently covered thirty-nine of forty nodes would type-check
    /// every switch in the runtime and still let one node through.
    /// </summary>
    [Fact]
    public void TheUnionIsEveryConcreteNode_PlusTheSeam()
    {
        var kinds = NodeKindTsGenerator.Kinds();

        kinds.Should().HaveCount(NodeKindTsGenerator.Vocabulary().Count() + 1,
            "every concrete node contributes its kind, and the seam is the one word no concrete node declares");
        kinds.Should().OnlyHaveUniqueItems();
        kinds.Should().EndWith([NodeKindTsGenerator.Seam]);
        kinds.Should().Contain("box").And.Contain("webFrame").And.Contain("sheetSurface");
        NodeKindTsGenerator.Vocabulary().Should().HaveCountGreaterThan(20,
            "the vocabulary has to be real for this to mean anything");
    }

    /// <summary>
    /// The seam is added by the generator and cannot be reached by reflection: <c>UiComponent</c> is
    /// abstract, so the scan of concrete types never sees the <c>"component"</c> every app component
    /// inherits. Forgetting it would drop the one kind the client's own <c>case 'component'</c> needs.
    /// </summary>
    [Fact]
    public void TheSeamIsNotAConcreteNode_AndIsInTheUnionAnyway()
    {
        NodeKindTsGenerator.Vocabulary().Should().NotContain(typeof(UiComponent));
        typeof(UiComponent).IsAbstract.Should().BeTrue(
            "the seam is abstract, which is exactly why the generator has to name it");
        NodeKindTsGenerator.Generate().Should().Contain($"'{NodeKindTsGenerator.Seam}'");
    }

    /// <summary>
    /// The generator REFUSES rather than emitting a union that lies. These three were assertions in
    /// the source-reading coverage pin; they live here now, in the code that cannot proceed without
    /// them, so that pin can retire without losing a check. The messages are what a maintainer sees,
    /// so they name the node and say what it broke.
    /// </summary>
    [Fact]
    public void TheGeneratorNamesWhatWouldMakeAKindStopBeingAnIdentity()
    {
        var refuse = () => NodeKindTsGenerator.Kinds();

        // Today the vocabulary is well-formed, so the refusals cannot be provoked without a broken
        // build. What CAN be asserted is that they are the reasons the generator gives — each one
        // spelled where a reader looking for it will find it.
        refuse.Should().NotThrow("the vocabulary is well-formed; this is the case that must keep passing");

        var source = File.ReadAllText(GeneratorSourcePath());
        source.Should().Contain("A node without a wire kind cannot cross to the browser");
        source.Should().Contain("belongs to no concrete ");
        source.Should().Contain("two nodes on one kind lower as ");
    }

    private static string GeneratorSourcePath([CallerFilePath] string sourcePath = "")
    {
        var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));
        return Path.Combine(repoRoot, "src", "eQuantic.UI.Web.Build", "NodeKindTsGenerator.cs");
    }
}
