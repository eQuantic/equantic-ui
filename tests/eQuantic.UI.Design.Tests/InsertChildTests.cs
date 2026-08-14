using eQuantic.UI.Design;
using FluentAssertions;

namespace eQuantic.UI.Design.Tests;

/// <summary>
/// Inserting a component into a declarative <c>children: [ … ]</c>.
/// <para>
/// The one structural gesture inside the fence. A collection expression is the only shape an
/// insertion can be spliced into safely — its elements are a list with real spans, so a new one goes
/// between two of them without touching anything else. The other two shapes a container is written
/// in are not lists in the place that matters, and editing them is statement-level dataflow.
/// </para>
/// </summary>
public sealed class InsertChildTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "eq-insert-" + Guid.NewGuid().ToString("N"));
    private readonly string _probe;
    private readonly DesignSession _session = new();

    private const string Source = """
        using eQuantic.UI.Core;
        using eQuantic.UI.Primitives;
        // The component TYPES, not only the factory surface — the SDK puts this in every file of a
        // real project, and a palette entry naming an enum (Skeleton(SkeletonShape.Line, …)) needs
        // it. A probe without it is stricter than any project the editor will ever open.
        using eQuantic.UI.Components;
        using static eQuantic.UI.Components.UI;
        using StatefulComponent = eQuantic.UI.Primitives.StatefulComponent;

        [Page("/probe")]
        public sealed class Probe : StatefulComponent
        {
            public override VisualNode Build(ComponentContext context)
            {
                // Imperative: a container whose children are statements, and therefore outside the fence.
                var built = new Column(gap: Space.S2);
                built.Add(Text("built", TypeRole.BodyM, context.Theme.TextPrimary));

                return Column(gap: Space.S4, children: [
                    Text("first", TypeRole.BodyM, context.Theme.TextPrimary),
                    Text("second", TypeRole.BodyM, context.Theme.TextPrimary),
                    // A plain reference, never a construction — so it carries no origin, and the
                    // editor can no more select it than the compiler could stamp it.
                    built,
                    Text("omega", TypeRole.BodyM, context.Theme.TextPrimary),
                ]);
            }
        }
        """;

    public InsertChildTests()
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

    private string OriginOf(string fragment)
    {
        var lines = Source.Replace("\r\n", "\n").Split('\n');
        var line = Array.FindIndex(lines, l => l.Contains(fragment, StringComparison.Ordinal));
        line.Should().BeGreaterThanOrEqualTo(0, $"the probe should contain '{fragment}'");
        var column = lines[line].IndexOf(fragment, StringComparison.Ordinal);
        return $"{_probe}|{line}:{column}|{line}:{column + fragment.Length}";
    }

    private static string Apply(string text, EditResult edit)
    {
        var source = Microsoft.CodeAnalysis.Text.SourceText.From(text);
        var start = source.Lines[edit.StartLine].Start + edit.StartColumn;
        var end = source.Lines[edit.EndLine].Start + edit.EndColumn;
        return source.Replace(Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(start, end), edit.NewText).ToString();
    }

    private string Declarative => OriginOf("Column(gap: Space.S4");
    private string Imperative => OriginOf("new Column(gap: Space.S2");

    [Fact]
    public void ADeclarativeContainer_ReportsItsChildCount()
    {
        var node = _session.Inspect(_probe, Source, Declarative);

        node!.ChildCount.Should().Be(4);
        node.InsertReason.Should().BeNull();
    }

    /// <summary>
    /// A container with no list ALWAYS says why. An affordance that appears and then refuses reads as
    /// a bug, so the panel has to be able to ask before it draws the control — and the first version
    /// of this returned null for `new Column(…)`, whose constructor takes no children parameter at
    /// all, which is the commonest container shape in the repo.
    /// </summary>
    [Fact]
    public void AContainerWithNoList_SaysWhyRatherThanNothing()
    {
        var node = _session.Inspect(_probe, Source, Imperative);

        node!.ChildCount.Should().Be(-1);
        node.InsertReason.Should().NotBeNullOrWhiteSpace();
        node.InsertReason.Should().NotContain(".ctor", "the reader recognises the type, not the metadata name");
    }

    [Fact]
    public void InsertingAtTheStart_PutsItBeforeTheFirstChild()
    {
        var edit = _session.InsertChild(_probe, Source, Declarative, 0, "Divider()");

        edit.Applied.Should().BeTrue(edit.Reason);
        var after = Apply(Source, edit);
        after.Should().Contain("children: [\n            Divider(),\n            Text(\"first\"");
    }

    [Fact]
    public void InsertingAtTheEnd_PutsItAfterTheLastChild()
    {
        var edit = _session.InsertChild(_probe, Source, Declarative, 4, "Divider()");

        edit.Applied.Should().BeTrue(edit.Reason);
        var after = Apply(Source, edit);
        after.IndexOf("Divider()", StringComparison.Ordinal)
            .Should().BeGreaterThan(after.IndexOf("\"omega\"", StringComparison.Ordinal));
    }

    /// <summary>Past the end is the same as at the end — a panel offering "+ last" should not have to
    /// know the count exactly to be right.</summary>
    [Fact]
    public void AnIndexPastTheEnd_LandsAtTheEnd()
    {
        var edit = _session.InsertChild(_probe, Source, Declarative, 99, "Divider()");

        edit.Applied.Should().BeTrue(edit.Reason);
        var after = Apply(Source, edit);
        after.IndexOf("Divider()", StringComparison.Ordinal)
            .Should().BeGreaterThan(after.IndexOf("\"omega\"", StringComparison.Ordinal));
    }

    [Fact]
    public void InsertingIntoAnImperativeContainer_IsRefusedWithTheReason()
    {
        var edit = _session.InsertChild(_probe, Source, Imperative, 0, "Divider()");

        edit.Applied.Should().BeFalse();
        edit.Reason.Should().Contain("statement");
    }

    private string SecondChild => OriginOf("Text(\"second\"");

    [Fact]
    public void AnElementOfAList_KnowsWhereItSitsAmongItsSiblings()
    {
        var node = _session.Inspect(_probe, Source, SecondChild);

        node!.SiblingIndex.Should().Be(1);
        node.SiblingCount.Should().Be(4);
    }

    /// <summary>A node that is not an element of a list has no order to be moved within, and the
    /// panel hides the controls rather than explaining an arrangement that does not exist.</summary>
    [Fact]
    public void ANodeThatIsNotAnElement_HasNoSiblingPosition()
    {
        _session.Inspect(_probe, Source, Declarative)!.SiblingIndex.Should().Be(-1);
    }

    /// <summary>
    /// The two elements swap and everything BETWEEN them stays where it is — the comma, the newline,
    /// the indentation. Rewriting the list from its element texts would be simpler and would silently
    /// discard a comment sitting between two children.
    /// </summary>
    [Fact]
    public void MovingAChildUp_SwapsItWithThePreviousSibling()
    {
        var edit = _session.MoveChild(_probe, Source, SecondChild, -1);

        edit.Applied.Should().BeTrue(edit.Reason);
        Apply(Source, edit).Should().Contain("Text(\"second\"").And.Contain("Text(\"first\"");
        var after = Apply(Source, edit);
        after.IndexOf("\"second\"", StringComparison.Ordinal)
            .Should().BeLessThan(after.IndexOf("\"first\"", StringComparison.Ordinal));
    }

    [Fact]
    public void MovingTheFirstChildUp_IsRefusedWithASentenceRatherThanSilence()
    {
        var edit = _session.MoveChild(_probe, Source, OriginOf("Text(\"first\""), -1);

        edit.Applied.Should().BeFalse();
        edit.Reason.Should().Contain("already first");
    }

    /// <summary>The element goes and so does ONE separator. Taking the element alone leaves a stray
    /// comma; taking both leaves a hole.</summary>
    [Fact]
    public void RemovingAChild_TakesItsSeparatorWithIt()
    {
        var edit = _session.RemoveChild(_probe, Source, SecondChild);

        edit.Applied.Should().BeTrue(edit.Reason);
        var after = Apply(Source, edit);
        after.Should().NotContain("\"second\"");
        after.Should().NotContain(",,").And.NotContain("[,");
    }

    [Fact]
    public void RemovingTheLastChild_LeavesNoOrphanedComma()
    {
        var edit = _session.RemoveChild(_probe, Source, OriginOf("Text(\"omega\""));

        edit.Applied.Should().BeTrue(edit.Reason);
        var after = Apply(Source, edit);
        after.Should().NotContain("\"omega\"");
        after.Should().NotContain(",\n                ]").And.NotContain(",,");
    }

    /// <summary>
    /// EVERY entry the palette offers has to compile where it lands.
    /// <para>
    /// This is the check that keeps the snippet synthesis honest as the component library grows: the
    /// palette is derived from the factory surface, so a new component with a parameter shape nobody
    /// anticipated shows up here as a refusal rather than as a broken file on someone's screen.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryPaletteEntry_InsertsIntoARealListAndStillCompiles()
    {
        var palette = _session.Palette();
        palette.Should().HaveCountGreaterThan(20, "the factory surface is much larger than a handful");

        var refused = palette
            .Select(entry => (entry, edit: _session.InsertChild(_probe, Source, Declarative, 1, entry.Snippet)))
            .Where(pair => !pair.edit.Applied)
            .Select(pair => $"{pair.entry.Name}: {pair.entry.Snippet} — {pair.edit.Reason}")
            .ToArray();

        refused.Should().BeEmpty(
            "the palette must only offer what it can actually write:\n" + string.Join("\n", refused));
    }
}
