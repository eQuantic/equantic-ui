using eQuantic.UI.Design;
using FluentAssertions;

namespace eQuantic.UI.Design.Tests;

/// <summary>
/// The lists a call is written with that are NOT children — a <c>Grid</c>'s <c>columns</c> first.
/// <para>
/// Structurally they are the same thing: a collection expression whose elements have real spans. So
/// everything built for children works on them unchanged, and withholding it would have been a fence
/// around the word "children" rather than around anything real.
/// </para>
/// <para>
/// What differs is what goes IN them. A <c>GridTrack</c> is data: it never renders, so nothing on the
/// canvas carries its span, and a palette of components has nothing it could offer. Both follow from
/// reading the signature rather than from a rule about grids.
/// </para>
/// </summary>
public sealed class NamedListTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "eq-lists-" + Guid.NewGuid().ToString("N"));
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
                return Grid(columns: [GridTrack.Flex(), GridTrack.Fixed(120)], gap: 12, children: [
                    Text("a", TypeRole.BodyM, context.Theme.TextPrimary),
                    Text("b", TypeRole.BodyM, context.Theme.TextPrimary),
                ]);
            }
        }
        """;

    public NamedListTests()
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

    private string TheGrid => OriginOf("Grid(columns:");

    [Fact]
    public void ACallWithTwoLists_ReportsBothOfThem()
    {
        var node = _session.Inspect(_probe, Source, TheGrid);

        node!.Lists.Should().NotBeNull();
        node.Lists.Should().Contain(list => list.Name == "columns" && list.Count == 2 && !list.Visual);
        node.Lists.Should().Contain(list => list.Name == "children" && list.Count == 2 && list.Visual);
        node.Lists!.Single(list => list.Name == "columns").ElementType.Should().Be("GridTrack");
    }

    /// <summary>
    /// A list of DATA is offered its own type's ways of being written. Offering a Button for a column
    /// would be a palette that has not read the signature it is filling in.
    /// </summary>
    [Fact]
    public void ThePaletteForAValueList_OffersTheTypesOwnWaysOfBeingWritten()
    {
        var palette = _session.Palette(_probe, Source, TheGrid, "columns");

        palette.Should().NotBeEmpty();
        palette.Should().OnlyContain(entry => entry.Source == "value");
        palette.Should().Contain(entry => entry.Snippet == "GridTrack.Auto");
        palette.Should().Contain(entry => entry.Snippet.StartsWith("GridTrack.Flex(", StringComparison.Ordinal));
        // The framework gives value records no factory of their own, deliberately: they are data, and
        // data reads fine target-typed inside a list whose type is already known.
        palette.Should().Contain(entry => entry.Snippet.StartsWith("new(", StringComparison.Ordinal));
        palette.Should().NotContain(entry => entry.Name == "Button");
    }

    [Fact]
    public void ThePaletteForTheChildren_IsStillTheComponentSurface()
    {
        var palette = _session.Palette(_probe, Source, TheGrid, "children");

        palette.Should().Contain(entry => entry.Name == "Button");
        palette.Should().NotContain(entry => entry.Source == "value");
    }

    [Fact]
    public void AColumnCanBeAdded()
    {
        var edit = _session.InsertChild(_probe, Source, TheGrid, 2, "GridTrack.Auto", "columns");

        edit.Applied.Should().BeTrue(edit.Reason);
        var after = Apply(Source, edit);
        // On the SAME line: the list was written on one, and a tool should not reformat a file to
        // make room for itself.
        after.Should().Contain("columns: [GridTrack.Flex(), GridTrack.Fixed(120), GridTrack.Auto]");
        // Untouched: adding a column re-flows the children, it does not add one.
        _session.Inspect(_probe, after, OriginOf("Grid(columns:"))!.Lists!
            .Single(list => list.Name == "children").Count.Should().Be(2);
    }

    [Fact]
    public void AColumnCanBeRemovedByItsPosition()
    {
        // By INDEX, because a GridTrack never renders: nothing on the canvas carries its span, so
        // there is no origin to point at.
        var edit = _session.RemoveAt(_probe, Source, TheGrid, "columns", 0);

        edit.Applied.Should().BeTrue(edit.Reason);
        var after = Apply(Source, edit);
        after.Should().Contain("columns: [GridTrack.Fixed(120)]");
        after.Should().NotContain(",,").And.NotContain("[,");
    }

    [Fact]
    public void RemovingAPositionThatIsNotThere_IsRefusedWithTheCount()
    {
        var edit = _session.RemoveAt(_probe, Source, TheGrid, "columns", 7);

        edit.Applied.Should().BeFalse();
        edit.Reason.Should().Contain("2 entries");
    }

    [Fact]
    public void AListTheCallDoesNotHave_IsRefusedByName()
    {
        var edit = _session.InsertChild(_probe, Source, TheGrid, 0, "GridTrack.Auto", "rows");

        edit.Applied.Should().BeFalse();
        edit.Reason.Should().Contain("rows");
    }

    /// <summary>The same guard the component palette has: everything offered has to compile where it
    /// lands, or the palette has broken the file to show a menu.</summary>
    [Fact]
    public void EveryColumnThePaletteOffers_CompilesWhereItLands()
    {
        var refused = _session.Palette(_probe, Source, TheGrid, "columns")
            .Select(entry => (entry, edit: _session.InsertChild(_probe, Source, TheGrid, 1, entry.Snippet, "columns")))
            .Where(pair => !pair.edit.Applied)
            .Select(pair => $"{pair.entry.Name}: {pair.entry.Snippet} — {pair.edit.Reason}")
            .ToArray();

        refused.Should().BeEmpty(string.Join("\n", refused));
    }
}
