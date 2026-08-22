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
        var previousFormat = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        try
        {
            // BOTH: the day and month names follow the FORMAT culture, and the chevron labels come
            // from SdkStrings through a ResourceManager, which reads the UI culture. Pinning only
            // the first passes here and fails on a machine whose language is not English.
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            return WebRealizer.Lower(node, Theme).Render();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousFormat;
            CultureInfo.CurrentUICulture = previousUi;
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

        // Read under the same pinned culture the tree was rendered in, so this compares the
        // string the component emitted rather than this machine's translation of it.
        labels.Should().Contain(UnderCulture(() => SdkStrings.PreviousMonth))
            .And.Contain(UnderCulture(() => SdkStrings.NextMonth));
    }

    private static string UnderCulture(Func<string> read, string culture = "en-US")
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            return read();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void OnceTheresACursor_TheActivedescendantNamesTheDayItIsOn()
    {
        // No case here had a cursor, which is why nothing noticed that the id was computed from the
        // COLUMN while the realizers number by counting CELLS. July 2026 starts on a Wednesday, so
        // the first week carries three holes: the 1st is column 3 and cell 0, and the pointer named
        // the 4th while the ring sat on the 1st.
        var calendar = new Calendar(July17);
        var first = Render(calendar);
        var firstCell = Cells(first)[0];
        firstCell.Attributes["aria-label"].Should().Be("Wednesday, 1 July 2026");

        // Pressing a day is what gives the grid a cursor.
        Press(firstCell);
        var after = Render(calendar);

        var grid = Walk(after).Single(n => n.Attributes.GetValueOrDefault("role") == "grid");
        var active = grid.Attributes["aria-activedescendant"];
        var target = Walk(after).Single(n => n.Attributes.GetValueOrDefault("id") == active);
        target.Attributes["aria-label"].Should().Be("Wednesday, 1 July 2026",
            "the activedescendant has to name the day the cursor is on, not the one at that column");
    }

    /// <summary>Invokes a lowered node's click handler — the same Action the realizer put there.</summary>
    private static void Press(HtmlNode node)
    {
        var handler = node.Events.Values.OfType<Action>().FirstOrDefault();
        handler.Should().NotBeNull("the cell has to carry a press for this test to mean anything");
        handler!();
    }

    [Fact]
    public void WhenTheAppMovesTheSelection_TheViewFollowsIt()
    {
        // A CONTROLLED calendar. Without this the instance adopted the new date and kept showing
        // the old month, so the selection was simply not on screen and the component looked like
        // it had ignored the app.
        var mounted = new Calendar(July17);
        Render(mounted);

        mounted.AdoptConfig(new Calendar(new DateOnly(2026, 9, 3)));
        var after = Render(mounted);

        Walk(after).Single(n => n.Attributes.GetValueOrDefault("role") == "grid")
            .Attributes["aria-label"].Should().Be("September 2026");
        Cells(after).Single(c => c.Attributes["aria-selected"] == "true")
            .Attributes["aria-label"].Should().Be("Thursday, 3 September 2026");
    }

    [Fact]
    public void AnUnrelatedReRender_DoesNotYankTheViewBack()
    {
        // The other half of the rule: re-syncing on EVERY adopt would drag the reader back from
        // whatever month they had paged to, on a re-render that changed nothing about the date.
        var mounted = new Calendar(July17);
        Render(mounted);
        Press(Walk(Render(mounted)).First(n =>
            n.Attributes.GetValueOrDefault("aria-label") == SdkStringUnderTest.NextMonth));

        mounted.AdoptConfig(new Calendar(July17));
        var after = Render(mounted);

        Walk(after).Single(n => n.Attributes.GetValueOrDefault("role") == "grid")
            .Attributes["aria-label"].Should().Be("August 2026");
    }

    private static class SdkStringUnderTest
    {
        internal static string NextMonth => UnderCulture(() => SdkStrings.NextMonth);
    }

    [Fact]
    public void ACalendarWithNothingSelected_Renders()
    {
        // Every other case here hands it a date, which is why the whole suite missed that an
        // unselected calendar crashed: `Selected == day` is a LIFTED comparison in C# — false for
        // null — and the twin lowered it to `selected.equals(day)`, which throws. It rendered on
        // the server and died in the browser, the worst place for a difference to live.
        var cells = Cells(Render(new Calendar()));

        cells.Should().NotBeEmpty();
        cells.Should().OnlyContain(cell => cell.Attributes["aria-selected"] == "false");
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
