using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A region whose content CHANGES is announced where the user is (specs B18, C4).
///
/// <para>
/// Both rows were the same hole under two component shapes. A Banner returned an unannotated Box —
/// the status variant picked a glyph and a fill and nothing else — so a warning that appeared said
/// nothing at all. A Toast lowered to <c>new Overlay(anchor) { Modal = false }</c>, and both
/// realizers gate every semantic on the layer being MODAL, so a non-modal layer emitted a bare
/// passthrough div. Neither could be fixed in its component: the vocabulary had no live region, and
/// the only <c>aria-live</c> anywhere in the write-once path was the text field's description.
/// </para>
///
/// <para>
/// The severity split is the spec's own and is the reason the urgency is a PROPERTY rather than two
/// nodes: Warning and Destructive are alerts that interrupt, Info and Success are statuses that
/// wait for a pause. Assertive is a cost every other announcement on the page pays, so a component
/// has to ask for it.
/// </para>
/// </summary>
public class LiveRegionSemanticsTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Lower(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static HtmlNode Region(VisualNode node) =>
        Walk(Lower(node)).Should().ContainSingle(
            n => n.Attributes.ContainsKey("aria-live"),
            "a live region is announced exactly once — two nested ones announce the same change twice")
            .Which;

    /// <summary>
    /// The role and the live value are BOTH stated. The role is what a reader reports the region as;
    /// the live value is what makes it watched. <c>role="alert"</c> does imply assertive in the
    /// spec, and implementations have historically disagreed about whether an alert inserted after
    /// load is announced at all — so neither is inferred from the other.
    /// </summary>
    [Theory]
    [InlineData(LiveRegionUrgency.Polite, "status", "polite")]
    [InlineData(LiveRegionUrgency.Assertive, "alert", "assertive")]
    public void TheUrgencySetsTheRoleAndTheLiveValueTogether(
        LiveRegionUrgency urgency, string role, string live)
    {
        var host = Region(new LiveRegion(new Text("saved", TypeRole.BodyM)) { Urgency = urgency });

        host.Attributes["role"].Should().Be(role);
        host.Attributes["aria-live"].Should().Be(live);
    }

    /// <summary>
    /// The WHOLE region is re-read when any part of it changes. Without this a reader announces only
    /// the node that changed, so a banner whose lead-in and body both move is announced as a
    /// fragment of itself.
    /// </summary>
    [Fact]
    public void TheRegionIsAnnouncedWhole()
    {
        Region(new LiveRegion(new Text("x", TypeRole.BodyM))).Attributes["aria-atomic"]
            .Should().Be("true");
    }

    /// <summary>
    /// Polite by default, because the default is what every caller that does not think about it
    /// gets, and an assertive default makes a screen reader unusable.
    /// </summary>
    [Fact]
    public void TheDefaultIsPolite()
    {
        var host = Region(new LiveRegion(new Text("x", TypeRole.BodyM)));

        host.Attributes["aria-live"].Should().Be("polite");
        host.Attributes["role"].Should().Be("status");
    }

    /// <summary>The region's own name, for when the content alone does not say what it is for — and
    /// absent rather than empty when it has none, so a reader falls back to the content.</summary>
    [Fact]
    public void ALabelNamesTheRegion_AndAnEmptyOneIsNotEmitted()
    {
        Region(new LiveRegion(new Text("40%", TypeRole.BodyM)) { Label = "Upload status" })
            .Attributes["aria-label"].Should().Be("Upload status");

        Region(new LiveRegion(new Text("40%", TypeRole.BodyM)))
            .Attributes.Should().NotContainKey("aria-label");
    }

    /// <summary>Spec B18: the banner IS the announcement, and the severity decides how hard it
    /// interrupts.</summary>
    [Theory]
    [InlineData(Variant.Info, "status", "polite")]
    [InlineData(Variant.Success, "status", "polite")]
    [InlineData(Variant.Warning, "alert", "assertive")]
    [InlineData(Variant.Destructive, "alert", "assertive")]
    public void ABannerAnnouncesItselfAtItsOwnSeverity(Variant status, string role, string live)
    {
        var host = Region(new Banner(status, "Your card expires this month.", "Renew it."));

        host.Attributes["role"].Should().Be(role);
        host.Attributes["aria-live"].Should().Be(live);
    }

    /// <summary>
    /// The banner's content is INSIDE the region — which is the half that makes "content change
    /// re-announces" true without the component tracking anything. A region announced beside the
    /// surface rather than around it would say nothing when the text changed.
    /// </summary>
    [Fact]
    public void TheBannersOwnWordsAreInsideTheRegion()
    {
        var host = Region(new Banner(Variant.Info, "Maintenance tonight", "From 22:00 UTC."));

        Walk(host).Should().Contain(n => n.TextContent == "Maintenance tonight");
        Walk(host).Should().Contain(n => n.TextContent == "From 22:00 UTC.");
    }

    /// <summary>
    /// Spec C4: a POLITE live region. The toast tells the user what just happened and must not cut
    /// off whatever a reader is saying.
    /// </summary>
    [Fact]
    public void AToastAnnouncesPolitely()
    {
        var host = Region(new Toast("Card removed", Variant.Info, "Undo", () => { }));

        host.Attributes["role"].Should().Be("status");
        host.Attributes["aria-live"].Should().Be("polite");
        Walk(host).Should().Contain(n => n.TextContent == "Card removed",
            "the message is what gets announced");
    }

    /// <summary>
    /// The region wraps the PILL, not the layer. The layer fills the viewport, and a live region
    /// that size hands a reader the whole screen as the announcement — every toast would re-read
    /// the page behind it.
    /// </summary>
    [Fact]
    public void TheToastsRegionIsThePillRatherThanTheLayer()
    {
        var layer = Lower(new Toast("Card removed"));

        layer.Attributes.Should().NotContainKey("aria-live",
            "the outermost element is the non-modal Overlay layer");
        // Taken from the SAME tree, so "not the layer" is a real statement about where the region
        // sits rather than about two separate lowerings being different objects.
        Walk(layer).Should()
            .ContainSingle(n => n.Attributes.ContainsKey("aria-live")).Which
            .Should().NotBeSameAs(layer, "the region is inside the layer, not the layer itself");
    }

    /// <summary>
    /// The wrapper keeps the child's layout CONTRACT on the way through — and this is the test that
    /// exercises the <c>LiveRegion</c> arms in <c>Fills</c> and <c>CapsAt</c> at all.
    /// <para>
    /// NESTED on purpose. <see cref="WrapperLayoutTransparencyTests"/> puts the region OUTERMOST,
    /// where <c>LowerLiveRegion</c> calls both helpers on its own child directly and the recursive
    /// arms are never consulted: deleting either one leaves that sweep green (measured — 16 passed
    /// with each arm removed). Progress learned this the same way and the note is on its test too.
    /// </para>
    /// </summary>
    [Fact]
    public void TheRegionCarriesTheChildsLayoutContractThrough()
    {
        var capped = new Box(new BoxStyle { Width = SizeValue.Fill, MaxWidth = 320 });
        var pressed = new Pressable(new LiveRegion(capped) { Label = "Upload status" }, () => { });

        // A Pressable lowers to a real <button>, which carries no role attribute of its own.
        var host = Lower(pressed);
        host.Tag.Should().Be("button");

        host.Attributes["style"].Should().Contain("width: 100%",
            "the fill reaches the Pressable through the region");
        host.Attributes["style"].Should().Contain("max-width: 320px",
            "and so does the cap — a wrapper that takes the width and drops the maximum is half a contract");
    }

    /// <summary>
    /// A live region is READ, never operated: no tab stop and no handler of its own. It is the
    /// same rule the progress bar has, and for the same reason — promising a gesture that does
    /// nothing is worse than promising none.
    /// </summary>
    [Fact]
    public void TheRegionIsNotAControl()
    {
        var host = Region(new LiveRegion(new Text("x", TypeRole.BodyM)) { Label = "Status" });

        host.Attributes.Should().NotContainKey("tabindex");
        host.Events.Should().BeEmpty("nothing here is operable");
    }
}
