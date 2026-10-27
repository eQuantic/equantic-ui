using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;
using FluentAssertions;
using eQuantic.UI.Spreadsheets;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A host that CARRIES A ROLE has the bounds of the thing it names.
///
/// <para>
/// A screen reader draws its touch-exploration rectangle and a browser draws its focus ring from
/// the role-bearing element's box, so the box is part of the announcement rather than a styling
/// detail. Both of these lower to a <c>div</c>, which is block-level: with a child that hugs, the
/// host stretched to the container while the control stayed its own width, and the two stopped
/// describing the same thing. Photon has never had the defect — <c>MeasureWrapper</c> gives a
/// layout-transparent wrapper exactly <c>inner.Bounds</c> — so this was a web-only divergence in
/// the one place the two targets must agree.
/// </para>
///
/// <para>
/// The component library hid it: <c>ProgressBar</c> and <c>Slider</c> both hand their wrapper a
/// FILL track, which takes the <c>width: 100%</c> branch. It is reachable only through the
/// vocabulary directly — <c>new Progress(...)</c>, <c>UI.Progress(...)</c> — which is public
/// surface, and through any future component whose track hugs.
/// </para>
///
/// <para>
/// <c>SheetSurface</c> was the third instance (#241) until it stopped bearing a role (#1081): the table it
/// holds is the grid a reader walks, and the case below finds the grid there. It keeps the width contract,
/// because its box is still the one its door and its carets are placed in.
/// </para>
/// </summary>
public class RoleBearingHostBoundsTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    /// <summary>A track that states its own size — the case the fill branch never reaches.</summary>
    private static Box HugTrack() => new(new BoxStyle { Width = 120, Height = 8 });

    private static Box FillTrack() => new(new BoxStyle { Width = SizeValue.Fill, Height = 8 });

    public static TheoryData<string, VisualNode, VisualNode> RoleBearingWrappers() => new()
    {
        { "Progress", new Progress(HugTrack()) { Label = "Uploading" }, new Progress(FillTrack()) },
        { "Adjustable", new Adjustable(HugTrack(), _ => { }), new Adjustable(FillTrack(), _ => { }) },
        // A live region's box is what a reader outlines when it announces the change inside it, so
        // the same rule applies for the same reason — written down when the node was added rather
        // than found on a page later, which is what the two above cost.
        { "LiveRegion", new LiveRegion(HugTrack()) { Label = "Upload status" }, new LiveRegion(FillTrack()) },
    };

    [Theory]
    [MemberData(nameof(RoleBearingWrappers))]
    public void AHugChildLeavesTheRoleBearingHostHuggingToo(string name, VisualNode hug, VisualNode fill)
    {
        var hugHost = Render(hug);
        var fillHost = Render(fill);

        hugHost.Attributes.Should().ContainKey("role", $"{name} is here because its host announces one");
        hugHost.Attributes.GetValueOrDefault("style", "").Should().Contain("width: fit-content",
            $"{name}'s host is what a reader outlines, so a child that hugs leaves it hugging — a "
            + "block div would announce the container's width for a control that is 120px wide");

        fillHost.Attributes.GetValueOrDefault("style", "").Should().Contain("width: 100%",
            "and the fill branch is untouched: a filling track still makes the host fill, or the "
            + "child's own 100% would resolve against a shrink-to-fit box and collapse");
        fillHost.Attributes.GetValueOrDefault("style", "").Should().NotContain("fit-content",
            "the two are exclusive — a host that said both would be deciding nothing");
    }

    /// <summary>
    /// THE STEP THE OTHER TWO DID NOT NEED. <c>Progress</c> and <c>Adjustable</c> are handed a
    /// FILLING track by every component that uses them, so the rule changed their announcement and
    /// nothing a user could see. A <see cref="Spreadsheet"/> moved a real page: its window of cells
    /// hugged, and #241 measured its host going from the container's width to the grid's own box.
    /// <para>
    /// Since #995 the grid is a table viewport that takes the room it is given and scrolls inside it,
    /// so the grid's own box IS that room, and the host fills it: the box a reader outlines and the one
    /// the browser draws this tab stop's focus ring on is still the grid's, now on both axes.
    /// </para>
    /// </summary>
    [Fact]
    public void ARealSpreadsheetsGridHostIsTheGridsOwnBox()
    {
        var grid = Find(Render(new Spreadsheet(new SheetController())), "grid");

        grid.Should().NotBeNull("the component announces its window of cells as a grid");
        grid!.Attributes.GetValueOrDefault("style", "").Should().Contain("width: 100%")
            .And.Contain("height: 100%", "the grid takes the room it is given, so the host that names it is that room")
            .And.NotContain("fit-content");
    }

    /// <summary>The first element carrying <paramref name="role"/>, depth first — the page's own
    /// order, which is the order a reader walks.</summary>
    private static HtmlNode? Find(HtmlNode node, string role)
    {
        if (node.Attributes.GetValueOrDefault("role") == role) return node;
        foreach (var child in node.Children)
            if (Find(child, role) is { } found) return found;
        return null;
    }
}
