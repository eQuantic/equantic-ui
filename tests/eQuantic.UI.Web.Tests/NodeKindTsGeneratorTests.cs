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
    /// The generator REFUSES rather than emitting a union that lies, and each refusal is PROVOKED
    /// here rather than described. These three were assertions in the source-reading coverage pin;
    /// they live in the generator now, in the code that cannot proceed without them, so that pin can
    /// retire without losing a check.
    ///
    /// <para>
    /// A healthy assembly cannot exercise a refusal — which is exactly how the first version of this
    /// test went wrong: it ran <c>Kinds()</c> against the real vocabulary and then GREPPED the
    /// generator's source for its message strings. That would have passed for a generator whose
    /// checks had been deleted and whose comments remained. Review caught it, and it is the same
    /// defect the whole slice is about: an instrument that cannot fail is not an instrument. So the
    /// rules take a declaration set, and these build the malformed vocabularies the real one is not
    /// allowed to have.
    /// </para>
    /// </summary>
    [Fact]
    public void ANodeWithNoWireKind_IsRefusedAndNamed()
    {
        var refuse = () => NodeKindTsGenerator.Kinds([("Box", "box"), ("Mystery", "")]);

        refuse.Should().Throw<InvalidOperationException>(
                "an empty kind would generate '' into the union, which matches nothing the client sends")
            .WithMessage("*Mystery*", "a refusal that does not name the node is a puzzle");
    }

    [Fact]
    public void TwoNodesOnOneWireKind_AreRefusedAndBothNamed()
    {
        var refuse = () => NodeKindTsGenerator.Kinds([("Box", "box"), ("Crate", "box")]);

        refuse.Should().Throw<InvalidOperationException>(
                "the kind is a node's identity on the client; two on one kind lower as the same thing")
            .WithMessage("*Box*").WithMessage("*Crate*");
    }

    [Fact]
    public void AConcreteNodeClaimingTheSeamsWord_IsRefused()
    {
        var refuse = () => NodeKindTsGenerator.Kinds([("Box", "box"), ("Impostor", NodeKindTsGenerator.Seam)]);

        refuse.Should().Throw<InvalidOperationException>(
                "\"component\" is UiComponent's, and a concrete node taking it would collide with the "
                + "one case the client's own expansion seam needs")
            .WithMessage("*Impostor*");
    }

    /// <summary>
    /// A kind that cannot be WRITTEN is refused rather than escaped. It is quoted into this union
    /// and written as a <c>case '…':</c> in the browser's lowering, and the two have to match
    /// character for character — so a kind carrying a quote could be escaped into a union that
    /// compiles and still be a kind nobody can write a case for. Refusing it says so at the
    /// declaration instead of as a TypeScript syntax error two build steps away.
    /// </summary>
    [Theory]
    [InlineData("foo'bar", "a quote would end the literal early")]
    [InlineData("foo\\bar", "a backslash escapes whatever follows it")]
    [InlineData("foo\nbar", "a newline cannot sit inside a single-quoted literal at all")]
    [InlineData("Box", "a capital initial is not the camelCase the transpiler emits")]
    [InlineData("code-surface", "a hyphen is not an identifier character")]
    public void AKindThatCannotBeWrittenAsATypeScriptLiteral_IsRefused(string kind, string why)
    {
        var refuse = () => NodeKindTsGenerator.Kinds([("Box", "box"), ("Odd", kind)]);

        refuse.Should().Throw<InvalidOperationException>(why)
            .WithMessage("*Odd*", "a refusal that does not name the node is a puzzle");
    }

    /// <summary>
    /// The other direction, without which the three above could pass on a generator that refuses
    /// EVERYTHING: a well-formed set is accepted, sorted, and given the seam last.
    /// </summary>
    [Fact]
    public void AWellFormedSet_IsAcceptedSortedWithTheSeamLast()
    {
        NodeKindTsGenerator.Kinds([("Row", "row"), ("Box", "box"), ("Anchored", "anchored")])
            .Should().Equal("anchored", "box", "row", NodeKindTsGenerator.Seam);
    }

    /// <summary>And the real vocabulary is one of those: the case that must keep passing.</summary>
    [Fact]
    public void TheRealVocabulary_IsWellFormed()
    {
        var declared = NodeKindTsGenerator.Declared().ToList();

        declared.Should().HaveCount(NodeKindTsGenerator.Vocabulary().Count());
        NodeKindTsGenerator.Kinds(declared).Should().Equal(NodeKindTsGenerator.Kinds());
    }
}
