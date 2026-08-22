using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The GRID vocabulary — the piece a calendar needs and the composite vocabulary did not have
/// (design system C15: <c>grid</c> / <c>gridcell</c> + selected, day names as column headers).
/// <para>
/// Three facts are asserted here because each was a way to ship a broken tree: a grid whose cells
/// are not inside ROWS is invalid ARIA; a row that is a real box would break the caller's layout,
/// so it must be transparent (<c>display:contents</c>); and 42 day cells must cost ONE tab stop,
/// not 42 — the host owns the stop and the cells rove, exactly as an Adjustable's radios do.
/// </para>
/// </summary>
public class GridSemanticsTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    private static Row Row(params VisualNode[] children)
    {
        var row = new Row();
        foreach (var child in children) row.Add(child);
        return row;
    }

    private static VisualNode Cell(string day, bool selected) =>
        new Pressable(new Text(day), () => { })
        {
            Role = PressableRole.GridCell,
            Selected = selected,
            Label = $"July {day}",
        };

    private static Navigable Month(Action<NavigableMove>? onMove = null) =>
        new(
            [
                Row(new Text("S"), new Text("M"), new Text("T")),
                Row(Cell("1", false), Cell("2", true), Cell("3", false)),
            ],
            onMove ?? (_ => { }))
        {
            Label = "July 2026",
            HasHeaderRow = true,
            ActiveCell = (1, 1),
        };

    [Fact]
    public void TheHostIsTheOneTabStop_AndCarriesTheGridIdentity()
    {
        var host = Render(Month());

        host.Attributes["role"].Should().Be("grid");
        host.Attributes["tabindex"].Should().Be("0");
        host.Attributes["aria-label"].Should().Be("July 2026");
        // The arrows move a CELL without the focus ever leaving the host's one stop.
        host.Attributes["aria-activedescendant"].Should().Be("eq-cell-1-1");
    }

    [Fact]
    public void CellsAreGridCells_StatingSelectionAndLeavingTheTabOrder()
    {
        var cells = Walk(Render(Month()))
            .Where(node => node.Attributes.TryGetValue("role", out var role) && role == "gridcell")
            .ToList();

        cells.Should().HaveCount(3);
        cells.Select(cell => cell.Attributes["aria-selected"])
            .Should().Equal("false", "true", "false");
        // Roving: the composite is one stop, so no cell is one.
        cells.Should().OnlyContain(cell => cell.Attributes["tabindex"] == "-1");
        // Selection is an ATTRIBUTE, never smuggled into the name (spec §10).
        cells[1].Attributes["aria-label"].Should().Be("July 2");
    }

    [Fact]
    public void EveryRowIsARow_AndIsTransparentToLayout()
    {
        var rows = Walk(Render(Month()))
            .Where(node => node.Attributes.TryGetValue("role", out var role) && role == "row")
            .ToList();

        // A grid whose cells are not inside rows is an invalid tree — one row per declared row.
        rows.Should().HaveCount(2);
        // …and the row must not become a box: the caller's Grid/Column keeps placing the cells.
        rows.Should().OnlyContain(row =>
            row.Attributes.ContainsKey("style") && row.Attributes["style"]!.Replace(" ", "").Contains("display:contents"));
    }

    [Fact]
    public void AGridPanelIsADialog_BecauseFocusMovesIntoIt()
    {
        var anchored = new Anchored(new Pressable(new Text("17 Jul"), () => { }), Month())
        {
            Open = true,
            PanelRole = AnchorPanelRole.Dialog,
        };

        var nodes = Walk(Render(anchored)).ToList();

        // The trigger says what it opens…
        nodes.Should().Contain(node =>
            node.Attributes.ContainsKey("aria-haspopup") && node.Attributes["aria-haspopup"] == "dialog");
        // …and the panel is a dialog, not a listbox the trigger would drive from outside.
        nodes.Should().Contain(node =>
            node.Attributes.ContainsKey("role") && node.Attributes["role"] == "dialog");
    }
}
