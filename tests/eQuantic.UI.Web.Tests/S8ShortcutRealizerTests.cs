using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Spec S8 (parity pair of s8-shortcut.spec.ts): the chord's wire form is the LITERAL contract both
/// the realizer and the runtime controller compare, and adaptive gates carry their range in dp so a
/// design's own breakpoints need no shared registry.
/// </summary>
public class S8ShortcutRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    /// <summary>The RENDERED node — the public surface (RealizedElement is internal).</summary>
    private static Core.HtmlNode Lower(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    private static VisualNode Marker() => new Primitives.Box(new BoxStyle { Width = 10, Height = 10 });

    [Fact]
    public void Shortcut_IsLayoutTransparent_AndMarksTheChord()
    {
        var element = Lower(new Shortcut(Marker(), KeyChord.Command("k"), () => { }));

        element.Tag.Should().Be("div", "the CHILD lowers — a Shortcut adds no wrapper");
        element.Attributes["data-eq-shortcut"].Should().Be("command+k");
    }

    [Fact]
    public void Modifiers_SerializeInAFixedOrder()
    {
        var chord = new KeyChord("K", KeyModifiers.Command | KeyModifiers.Shift);
        Lower(new Shortcut(Marker(), chord, () => { }))
            .Attributes["data-eq-shortcut"].Should().Be("command+shift+k");
    }

    [Fact]
    public void BareKey_HasNoModifierPrefix()
    {
        Lower(new Shortcut(Marker(), KeyChord.Escape, () => { }))
            .Attributes["data-eq-shortcut"].Should().Be("escape");
    }

    [Fact]
    public void AdaptiveGates_CarryTheirRangeInDp()
    {
        AdaptiveGates.CompactUntil(600).Should().Be("eq-vc600");
        AdaptiveGates.MediumFrom(600, 840).Should().Be("eq-vm600-840");
        AdaptiveGates.MediumFrom(600, 0).Should().Be("eq-vm600", "no larger variant → open-ended");
        AdaptiveGates.ExpandedFrom(840).Should().Be("eq-vx840");
    }

    [Fact]
    public void CustomThresholds_GateAtTheDesignsBreakpoint()
    {
        // The handoff header swaps nav↔hamburger at 1024; gating at the spec's 840 is what let the
        // nav overlap the logo on tablets.
        var sink = new StyleSink();
        var element = WebRealizer.Lower(
            new AdaptiveNode(Marker(), null, Marker()) { ExpandedFrom = 1024 }, Theme, 1f, sink);

        ((Core.HtmlElement)element.Children[0]).ClassName.Should().Be("eq-vc1024");
        ((Core.HtmlElement)element.Children[1]).ClassName.Should().Be("eq-vx1024");
        sink.Css.Should().Contain("@media (min-width: 1024px){.eq-vx1024{display:contents}}");
        AdaptiveGates.Css("eq-vc1024").Should()
            .Be(".eq-vc1024{display:contents}@media (min-width: 1024px){.eq-vc1024{display:none}}");
    }

    [Fact]
    public void MediumRange_ClosesJustBelowTheNextThreshold()
    {
        AdaptiveGates.Css("eq-vm600-840").Should()
            .Be(".eq-vm600-840{display:none}@media (min-width: 600px) and (max-width: 839.98px)"
                + "{.eq-vm600-840{display:contents}}");
    }

    [Fact]
    public void Autofocus_MarksTheEntry()
    {
        // The entry lowers inside the stable .eq-field shell — the attribute rides the input.
        var element = Lower(new TextEntry("") { Autofocus = true, Placeholder = "Search…" });
        element.Children[0].Attributes.Should().ContainKey("autofocus");
    }

    [Fact]
    public void ResolveWidth_HonorsCustomThresholds()
    {
        var compact = Marker();
        var expanded = Marker();
        var adaptive = new AdaptiveNode(compact, null, expanded) { ExpandedFrom = 1024 };

        adaptive.ResolveWidth(900).Should().BeSameAs(compact, "the design still wants the drawer at 900");
        adaptive.ResolveWidth(1024).Should().BeSameAs(expanded);
    }
}
