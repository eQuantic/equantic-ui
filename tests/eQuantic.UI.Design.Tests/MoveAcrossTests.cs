using eQuantic.UI.Design;
using FluentAssertions;

namespace eQuantic.UI.Design.Tests;

/// <summary>
/// Moving a node out of one container's children and into another's.
/// <para>
/// A reorder cannot express this — the node leaves one list and joins a different one — and it is the
/// first edit that touches two places at once. Written as ONE replacement spanning both, because one
/// span is one <c>WorkspaceEdit</c> and therefore one Ctrl+Z.
/// </para>
/// </summary>
public sealed class MoveAcrossTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "eq-across-" + Guid.NewGuid().ToString("N"));
    private readonly string _probe;
    private readonly DesignSession _session = new();

    private const string Source = """
        using eQuantic.UI.Core;
        using eQuantic.UI.Primitives;
        using eQuantic.UI.Components;
        using static eQuantic.UI.Components.UI;
        using StatefulComponent = eQuantic.UI.Primitives.StatefulComponent;

        [Page("/probe")]
        public sealed class Probe : StatefulComponent
        {
            public override VisualNode Build(ComponentContext context)
            {
                return Column(gap: Space.S4, children: [
                    Column(gap: Space.S2, children: [
                        Text("alpha", TypeRole.BodyM, context.Theme.TextPrimary),
                        Text("beta", TypeRole.BodyM, context.Theme.TextPrimary),
                        Column(gap: Space.S5, children: [
                            Text("deep", TypeRole.BodyM, context.Theme.TextPrimary),
                        ]),
                    ]),
                    Column(gap: Space.S2, children: [
                        Text("gamma", TypeRole.BodyM, context.Theme.TextPrimary),
                    ]),
                    Column(gap: Space.S3, children: [
                        // The leading whitespace of the second line is VALUE, not layout.
                        Text(@"one
                        two", TypeRole.BodyM, context.Theme.TextPrimary),
                    ]),
                    new Column(gap: Space.S1),
                ]);
            }

            // Written somewhere else, and against something only IT has: the node here compiles where
            // it stands and nowhere else.
            private VisualNode Side(string label, ComponentContext context) =>
                Column(gap: Space.S1, children: [
                    Text(label, TypeRole.BodyM, context.Theme.TextPrimary),
                ]);
        }
        """;

    public MoveAcrossTests()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "Screens"));
        _probe = Path.Combine(_directory, "Screens", "Probe.cs");
        File.WriteAllText(_probe, Source);
        File.WriteAllText(Path.Combine(_directory, "Probe.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var references = new[]
            {
                typeof(eQuantic.UI.Primitives.VisualNode).Assembly,
                typeof(eQuantic.UI.Components.UI).Assembly,
                typeof(eQuantic.UI.Core.PageAttribute).Assembly,
                typeof(object).Assembly,
            }
            .Concat(AppDomain.CurrentDomain.GetAssemblies())
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(assembly => assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var refsFile = Path.Combine(_directory, "equantic.refs.txt");
        File.WriteAllLines(refsFile, references);
        _session.Initialize(_directory, refsFile, generatedDir: null);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private string OriginOf(string fragment, int occurrence = 0)
    {
        var lines = Source.Replace("\r\n", "\n").Split('\n');
        var seen = -1;
        for (var line = 0; line < lines.Length; line++)
        {
            var column = lines[line].IndexOf(fragment, StringComparison.Ordinal);
            if (column < 0) continue;
            if (++seen != occurrence) continue;
            return $"{_probe}|{line}:{column}|{line}:{column + fragment.Length}";
        }

        throw new InvalidOperationException($"the probe has no occurrence {occurrence} of '{fragment}'");
    }

    private static string Apply(string text, EditResult edit)
    {
        var source = Microsoft.CodeAnalysis.Text.SourceText.From(text);
        var start = source.Lines[edit.StartLine].Start + edit.StartColumn;
        var end = source.Lines[edit.EndLine].Start + edit.EndColumn;
        return source.Replace(Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(start, end), edit.NewText).ToString();
    }

    private string Outer => OriginOf("Column(gap: Space.S4");
    private string FirstGroup => OriginOf("Column(gap: Space.S2", 0);
    private string SecondGroup => OriginOf("Column(gap: Space.S2", 1);
    private string Alpha => OriginOf("Text(\"alpha\"");
    private string Imperative => OriginOf("new Column(gap: Space.S1");

    [Fact]
    public void MovingAChildIntoAnotherContainer_TakesItOutOfTheFirstAndPutsItInTheSecond()
    {
        var edit = _session.MoveAcross(_probe, Source, Alpha, SecondGroup, 1);

        edit.Applied.Should().BeTrue(edit.Reason);
        var after = Apply(Source, edit);

        // Gone from where it was, and after gamma where it went.
        after.Should().Contain("\"alpha\"");
        after.IndexOf("\"alpha\"", StringComparison.Ordinal)
            .Should().BeGreaterThan(after.IndexOf("\"gamma\"", StringComparison.Ordinal));
        after.IndexOf("\"beta\"", StringComparison.Ordinal)
            .Should().BeLessThan(after.IndexOf("\"gamma\"", StringComparison.Ordinal));
        after.Should().NotContain(",,").And.NotContain("[,");
    }

    /// <summary>Applied means it COMPILED — <c>Guarded</c> refuses anything that introduces an error,
    /// so a move that produced a hole in the list could not report success.</summary>
    [Fact]
    public void MovingIntoAnEarlierContainer_AlsoWorks()
    {
        var edit = _session.MoveAcross(_probe, Source, OriginOf("Text(\"gamma\""), FirstGroup, 0);

        edit.Applied.Should().BeTrue(edit.Reason);
        var after = Apply(Source, edit);
        after.IndexOf("\"gamma\"", StringComparison.Ordinal)
            .Should().BeLessThan(after.IndexOf("\"alpha\"", StringComparison.Ordinal));
    }

    /// <summary>
    /// The moved text arrives at its new depth. Carried across verbatim it would keep the indentation
    /// of the list it left, which reads as a file someone gave up halfway through editing.
    /// </summary>
    [Fact]
    public void AMultiLineNode_IsReindentedToItsNewDepth()
    {
        var edit = _session.MoveAcross(_probe, Source, FirstGroup, SecondGroup, 1);

        edit.Applied.Should().BeTrue(edit.Reason);
        var after = Apply(Source, edit);

        // Its children were at 16 in a list indented 12; the list it joins is indented 16, so they
        // land at 20 — and the block's own closing bracket comes back to 16.
        after.Should().Contain("\n" + new string(' ', 20) + "Text(\"alpha\"");
        after.Should().Contain("\n" + new string(' ', 16) + "]),");
    }

    [Fact]
    public void AMove_SaysWhereTheNodeLanded()
    {
        var edit = _session.MoveAcross(_probe, Source, Alpha, SecondGroup, 1);

        edit.Applied.Should().BeTrue(edit.Reason);
        edit.Origin.Should().NotBeNull();

        var after = Apply(Source, edit);
        _session.Inspect(_probe, after, edit.Origin!)!.Properties
            .Should().Contain(property => property.Value == "\"alpha\"");
    }

    /// <summary>
    /// The landed span is measured against the text that was WRITTEN, not the text that was read.
    /// <para>
    /// A node that changes depth is re-indented, so the two differ by exactly the indentation the move
    /// added or removed — and an origin that overshoots its node resolves to the smallest node
    /// CONTAINING it, which is the container it was just dropped into. The panel would then be
    /// pointed at the parent, and the next property edit would change the parent's.
    /// </para>
    /// </summary>
    [Fact]
    public void AReindentedMove_LandsOnTheNodeItselfAndNotItsNewParent()
    {
        // OUTWARDS, which is the direction that bites: the text written is SHORTER than the text read,
        // so a span measured against the original overshoots the node and swallows what follows it.
        // Inwards it undershoots, and a span that starts at the node and ends inside it still resolves
        // to the node — which is how the first version of this test passed with the defect in place.
        var edit = _session.MoveAcross(_probe, Source, OriginOf("Column(gap: Space.S5"), Outer, 0);

        edit.Applied.Should().BeTrue(edit.Reason);
        var landed = _session.Inspect(_probe, Apply(Source, edit), edit.Origin!);

        landed!.Properties.Should().Contain(property => property.Value == "Space.S5");
        landed.Properties.Should().NotContain(
            property => property.Value == "Space.S4", "that is the container it was dropped into");
    }

    /// <summary>
    /// The ONLY child of a list, removed. Every list in this repo is written with a trailing comma, so
    /// taking the element alone leaves <c>[ , ]</c> — which does not parse.
    /// <para>
    /// Found by the move, which removes as its first half, but reachable all along from the panel's
    /// own remove button: the existing test emptied a slot in a list of four, where the comma before
    /// it was taken instead.
    /// </para>
    /// </summary>
    [Fact]
    public void RemovingTheOnlyChildOfAList_TakesItsTrailingCommaWithIt()
    {
        var edit = _session.RemoveChild(_probe, Source, OriginOf("Text(\"gamma\""));

        edit.Applied.Should().BeTrue(edit.Reason);
        var after = Apply(Source, edit);
        after.Should().NotContain("\"gamma\"");
        after.Should().NotContain("[,").And.NotContain(",,");
    }

    [Fact]
    public void MovingIntoTheListItIsAlreadyIn_IsRefusedAsAReorder()
    {
        var edit = _session.MoveAcross(_probe, Source, Alpha, FirstGroup, 0);

        edit.Applied.Should().BeFalse();
        edit.Reason.Should().Contain("reorder");
    }

    /// <summary>The one drop a canvas can offer that has no meaning at all: a container into its own
    /// child. Refused on the syntax, before any of the text arithmetic could produce nonsense.</summary>
    [Fact]
    public void MovingAContainerIntoItsOwnChild_IsRefused()
    {
        var edit = _session.MoveAcross(_probe, Source, FirstGroup, FirstGroup, 0);

        edit.Applied.Should().BeFalse();
        edit.Reason.Should().Contain("itself");
    }

    [Fact]
    public void MovingIntoAContainerWithNoList_IsRefusedWithTheSameReasonInsertGives()
    {
        var edit = _session.MoveAcross(_probe, Source, Alpha, Imperative, 0);

        edit.Applied.Should().BeFalse();
        edit.Reason.Should().Contain("statement");
    }

    [Fact]
    public void MovingIntoAContainerInAnotherFile_IsRefusedRatherThanEditingTwoFiles()
    {
        var elsewhere = SecondGroup.Replace("Probe.cs", "Other.cs", StringComparison.Ordinal);
        var edit = _session.MoveAcross(_probe, Source, Alpha, elsewhere, 0);

        edit.Applied.Should().BeFalse();
        edit.Reason.Should().Contain("another file");
    }

    /// <summary>
    /// A move re-indents the text it carries, and inside a verbatim literal the leading whitespace is
    /// what the component RENDERS. Re-basing it changes the screen, and nothing catches that: the file
    /// still compiles, so the compile guard has nothing to object to and the diff shows whitespace.
    /// </summary>
    [Fact]
    public void AMultiLineStringLiteral_KeepsItsOwnIndentationWhenTheNodeMoves()
    {
        var edit = _session.MoveAcross(_probe, Source, OriginOf("Text(@\"one"), Outer, 0);

        edit.Applied.Should().BeTrue(edit.Reason);
        Apply(Source, edit).Should().Contain("one\n" + new string(' ', 16) + "two");
    }

    /// <summary>
    /// THE fence, and the reason there is no hand-written rule about what may move where: a node
    /// written against something only its own method has stops compiling in its new home, and the
    /// refusal quotes the compiler instead of guessing.
    /// </summary>
    [Fact]
    public void MovingANodeThatDependsOnWhereItWasWritten_IsRefusedByTheCompiler()
    {
        var edit = _session.MoveAcross(_probe, Source, OriginOf("Text(label"), Outer, 3);

        edit.Applied.Should().BeFalse();
        edit.Reason.Should().Contain("CS0103").And.Contain("label");
    }
}
