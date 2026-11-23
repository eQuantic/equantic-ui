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

    /// <summary>A track that hugs and is capped, and one that fills down: the two things a host that
    /// takes only the width would drop (#1517).</summary>
    private static Box CappedTrack() => new(new BoxStyle { Width = 120, MaxWidth = 200, Height = 8 });

    /// <summary>A track that fills across and is capped: the cap goes through on the fill branch too.</summary>
    private static Box FillCappedTrack() => new(new BoxStyle { Width = SizeValue.Fill, MaxWidth = 200, Height = 8 });

    private static Box FillDownTrack() => new(new BoxStyle { Width = 120, Height = SizeValue.Fill });

    /// <summary>Every wrapper the semantics host writes: the width contract holds for all six, the role
    /// only for the four that bear one.</summary>
    public static TheoryData<string, Func<VisualNode, VisualNode>> HostWrappers() => new()
    {
        { "Progress", child => new Progress(child) { Label = "Uploading" } },
        { "Adjustable", child => new Adjustable(child, _ => { }) },
        { "LiveRegion", child => new LiveRegion(child) { Label = "Upload status" } },
        { "Group", child => new Group(child) { Label = "Shipping" } },
        { "MergeSemantics", child => new MergeSemantics(child) },
        { "ExcludeSemantics", child => new ExcludeSemantics(child) },
    };

    public static TheoryData<string, VisualNode, VisualNode> RoleBearingWrappers() => new()
    {
        { "Progress", new Progress(HugTrack()) { Label = "Uploading" }, new Progress(FillTrack()) },
        { "Adjustable", new Adjustable(HugTrack(), _ => { }), new Adjustable(FillTrack(), _ => { }) },
        // A live region's box is what a reader outlines when it announces the change inside it, so
        // the same rule applies for the same reason — written down when the node was added rather
        // than found on a page later, which is what the two above cost.
        { "LiveRegion", new LiveRegion(HugTrack()) { Label = "Upload status" }, new LiveRegion(FillTrack()) },
        { "Group", new Group(HugTrack()) { Label = "Shipping" }, new Group(FillTrack()) { Label = "Shipping" } },
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
    /// The host takes the child's WHOLE contract, on every wrapper that writes it: the width, the cap
    /// through as <c>max-width</c> and a filling height as <c>height: 100%</c>. A host that kept the width
    /// and dropped the others is the half-contract that made the Link diverge, and the suite above reads
    /// only the width.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostWrappers))]
    public void TheHostTakesTheChildsWholeContract(string name, Func<VisualNode, VisualNode> wrap)
    {
        var plain = Render(wrap(HugTrack())).Attributes.GetValueOrDefault("style", "");
        var capped = Render(wrap(CappedTrack())).Attributes.GetValueOrDefault("style", "");
        var down = Render(wrap(FillDownTrack())).Attributes.GetValueOrDefault("style", "");

        plain.Should().Contain("width: fit-content", $"{name}'s host hugs a child that hugs")
            .And.NotContain("max-width").And.NotContain("height");
        Render(wrap(FillTrack())).Attributes.GetValueOrDefault("style", "").Should().Contain("width: 100%")
            .And.NotContain("fit-content");
        capped.Should().Contain("max-width: 200px", $"{name}'s host takes the child's cap through with the width");
        down.Should().Contain("height: 100%", $"{name}'s host fills down when its child does");
        var fillCapped = Render(wrap(FillCappedTrack())).Attributes.GetValueOrDefault("style", "");
        fillCapped.Should().Contain("width: 100%").And.Contain("max-width: 200px",
            $"{name}'s host takes the cap through on the fill branch too, never one without the other");

        // The pointer is taken by the one wrapper that is a tab stop and operated, and by none of the others.
        if (name == "Adjustable") plain.Should().Contain("pointer-events: auto");
        else plain.Should().NotContain("pointer-events", $"{name}'s host does not take the pointer");
    }

    /// <summary>
    /// The host writes the caller's attributes (the role's) and THEN the accessible name, as the tree wrote
    /// them before the helper: the delivery's claim is byte equality, and a host that wrote the name first
    /// passes every other test here (#1517).
    /// </summary>
    [Theory]
    [InlineData("Progress", "role,aria-label")]
    [InlineData("Adjustable", "role,tabindex,aria-label")]
    [InlineData("LiveRegion", "role,aria-live,aria-atomic,aria-label")]
    [InlineData("Group", "role,aria-label")]
    public void TheHostWritesTheRolesAttributesThenTheAccessibleName(string name, string order)
    {
        VisualNode node = name switch
        {
            "Progress" => new Progress(HugTrack()) { Label = "Uploading" },
            "Adjustable" => new Adjustable(HugTrack(), _ => { }) { Label = "Volume" },
            "LiveRegion" => new LiveRegion(HugTrack()) { Label = "Upload status" },
            _ => new Group(HugTrack()) { Label = "Shipping" },
        };

        Render(node).Attributes.Keys.Where(key => key != "style").Take(order.Split(',').Length)
            .Should().Equal(order.Split(','), $"{name}'s host writes the role's attributes, then its name");
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
