using System.Globalization;
using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;
// The component, not System.Globalization's — this file needs both namespaces.
using Calendar = eQuantic.UI.Components.Calendar;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The month grid (design system C15), asserted through what it RENDERS rather than through its
/// private state — a calendar is right when a screen reader and a keyboard agree with it, and both
/// of those read the tree.
/// </summary>
public class CalendarTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;
    private static readonly DateOnly July17 = new(2026, 7, 17);

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    /// <summary>Renders under a fixed culture, so a machine in São Paulo and one in Berlin assert
    /// the same tree.</summary>
    private static HtmlNode Render(VisualNode node, string culture = "en-US")
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            return WebRealizer.Lower(node, Theme).Render();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static IReadOnlyList<HtmlNode> Cells(HtmlNode tree) =>
        Walk(tree).Where(n => n.Attributes.GetValueOrDefault("role") == "gridcell").ToList();

    private static IReadOnlyList<string> Headers(HtmlNode tree) =>
        Walk(tree).Where(n => n.Attributes.GetValueOrDefault("role") == "columnheader")
            .Select(TextOf).ToList();

    private static string TextOf(HtmlNode node) =>
        node.TextContent is { Length: > 0 } own
            ? own
            : string.Concat(node.Children.Select(TextOf));

    [Fact]
    public void TheMonthIsAGrid_WithSevenColumnHeadersAndTheMonthAsItsName()
    {
        var tree = Render(new Calendar(July17));

        var grid = Walk(tree).Single(n => n.Attributes.GetValueOrDefault("role") == "grid");
        grid.Attributes["aria-label"].Should().Be("July 2026");
        Headers(tree).Should().Equal("Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat");
        // Six weeks always: a five-week month must not change the grid's height as the user pages.
        Walk(tree).Count(n => n.Attributes.GetValueOrDefault("role") == "row").Should().Be(7);
    }

    [Fact]
    public void OnlyThisMonthsDaysAreCells_AndTheSelectedOneSaysSo()
    {
        var cells = Cells(Render(new Calendar(July17)));

        // July has 31 days; the surrounding weeks are holes, not greyed numbers (C15).
        cells.Should().HaveCount(31);
        cells.Select(TextOf).Should().Equal(Enumerable.Range(1, 31).Select(d => d.ToString()));
        cells.Count(c => c.Attributes.GetValueOrDefault("aria-selected") == "true").Should().Be(1);
        cells.Single(c => c.Attributes.GetValueOrDefault("aria-selected") == "true")
            .Attributes["aria-label"].Should().Be("Friday, 17 July 2026");
    }

    [Fact]
    public void ACellIsNamedByItsFullDayName_BecauseAScreenReaderSpellsTheAbbreviation()
    {
        var cells = Cells(Render(new Calendar(July17)));
        cells[0].Attributes["aria-label"].Should().Be("Wednesday, 1 July 2026");
        cells[^1].Attributes["aria-label"].Should().Be("Friday, 31 July 2026");
    }

    [Fact]
    public void TheWeekStartsWhereTheCultureSaysItDoes()
    {
        // en-US starts on Sunday, fr-FR on Monday — the same array, rotated exactly once.
        Headers(Render(new Calendar(July17))).First().Should().Be("Sun");
        Headers(Render(new Calendar(July17), "fr-FR")).First().Should().Be("lun.");
        Headers(Render(new Calendar(July17), "fr-FR")).Should().HaveCount(7);
    }

    [Fact]
    public void OutOfRangeDaysAreNotTargets()
    {
        var bounded = new Calendar(July17, min: new DateOnly(2026, 7, 10), max: new DateOnly(2026, 7, 20));
        var cells = Cells(Render(bounded));

        // The 31 days still DRAW — the month keeps its shape — but only the allowed span is
        // pressable, so the keyboard cannot walk somewhere Enter would refuse.
        cells.Should().HaveCount(11);
        cells.Select(TextOf).Should().Equal(Enumerable.Range(10, 11).Select(d => d.ToString()));
    }

    [Fact]
    public void TheChevronsAreNamedInTheReadersLanguage()
    {
        var labels = Walk(Render(new Calendar(July17)))
            .Select(n => n.Attributes.GetValueOrDefault("aria-label"))
            .Where(l => l is not null)
            .ToList();

        labels.Should().Contain(SdkStrings.PreviousMonth).And.Contain(SdkStrings.NextMonth);
    }

    [Fact]
    public void NothingIsFocusedUntilTheGridIsEntered()
    {
        var grid = Walk(Render(new Calendar(July17)))
            .Single(n => n.Attributes.GetValueOrDefault("role") == "grid");

        // A calendar nobody has touched shows no focus ring — activedescendant points at nothing
        // because there is nothing to point at, which is different from pointing at a missing id.
        grid.Attributes.ContainsKey("aria-activedescendant").Should().BeFalse();
    }
}
