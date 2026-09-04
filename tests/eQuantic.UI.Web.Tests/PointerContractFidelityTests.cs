using eQuantic.UI.Components;
using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The §10 pointer contract, where the audit found it missing: an interactive list row and every
/// chip kind had no hover at all, and the search field's clear affordance was a 20dp glyph with a
/// 20dp hit rect.
/// <para>
/// Hover is emitted as a style DIFF, so it costs no JavaScript and never fires on touch — the
/// mechanism was already there and used by Menu, Table, DataTable and Accordion. A list and a chip
/// were the two row-shaped surfaces that never picked it up.
/// </para>
/// </summary>
public class PointerContractFidelityTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    /// <summary>The hover diff rides the generated stylesheet, so the SINK is where it lands.</summary>
    private static string Css(VisualNode node)
    {
        var sink = new StyleSink();
        WebRealizer.Lower(node, Theme, 1f, sink);
        return sink.Css;
    }

    [Fact]
    public void AnInteractiveRowWashesUnderTheCursor()
    {
        Css(new ListItem("Wi-Fi", "Photon-5G", onPressed: () => { }))
            .Should().Contain(":hover", "B2: interactive rows = SurfaceSubtle wash (§10)");
    }

    /// <summary>An inert row is not a control, and a cursor over one that reacts is a promise of a
    /// press that never comes.</summary>
    [Fact]
    public void AnInertRowDoesNot()
    {
        Css(new ListItem("Wi-Fi", "Photon-5G")).Should().NotContain(":hover");
    }

    /// <summary>
    /// A SELECTED row keeps its fill: its background IS the state, and washing it to SurfaceSubtle
    /// would erase the selection for exactly as long as the pointer rests on it.
    /// </summary>
    [Fact]
    public void ASelectedRowKeepsItsFill()
    {
        Css(new ListItem("Wi-Fi", onPressed: () => { }) { Selected = true })
            .Should().NotContain(":hover");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AFilterChipHoversEitherWay(bool selected)
    {
        Css(new Chip("Income", ChipKind.Filter, selected, () => { }))
            .Should().Contain(":hover", "B8: chips answer the pointer");
    }

    /// <summary>A Tag is an annotation, not a control — nothing to press, so nothing to promise.</summary>
    [Fact]
    public void ATagChipDoesNotHover()
    {
        Css(new Chip("Crypto", ChipKind.Tag)).Should().NotContain(":hover");
    }

    /// <summary>
    /// B10 asks for "glyph 20 in Full circle, hit 48". The clear button was the bare glyph, so its
    /// hit rect was 20×20 — on the one control a one-handed user pokes at while walking.
    /// <para>
    /// The width reaches the §08 minimum; the height is the pill's own 40, because nothing can grow
    /// past it — <c>Touch.MinTarget</c>'s doc claims the framework expands hit-slop symmetrically and
    /// no realizer implements that. Pinned as it stands so the day slop lands, this changes with it.
    /// </para>
    /// </summary>
    [Fact]
    public void TheClearAffordanceIsATargetAndNotAGlyph()
    {
        var html = WebRealizer.Lower(new SearchField("beach", _ => { }), Theme).Render();
        var clear = Walk(html)
            .Single(node => node.Attributes.GetValueOrDefault("aria-label", "") == SdkStrings.ClearSearch);

        var target = Walk(clear).Select(node => node.Attributes.GetValueOrDefault("style", ""))
            .First(style => style.Contains("width:"));
        target.Should().Contain($"width: {Touch.MinTarget}px");
        target.Should().Contain("border-radius:", "a Full circle, per B10");
    }

    /// <summary>An empty field has nothing to clear, so the affordance is absent rather than
    /// disabled — a control that cannot do anything is one more thing to tab past.</summary>
    [Fact]
    public void AnEmptyFieldHasNoClearAffordance()
    {
        var html = WebRealizer.Lower(new SearchField("", _ => { }), Theme).Render();

        Walk(html).Should().NotContain(node =>
            node.Attributes.GetValueOrDefault("aria-label", "") == SdkStrings.ClearSearch);
    }
}
