using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Wave 3 anchored overlay lowering (parity pair of anchored.spec.ts): the class strings are the
/// LITERAL contract the TS lowering mirrors — hydration adopts by class identity.
/// </summary>
public class AnchoredRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static Core.HtmlElement Lower(Anchored anchored) =>
        WebRealizer.Lower(anchored, Theme);

    [Fact]
    public void Closed_LowersHostAndAnchorOnly()
    {
        var element = Lower(new Anchored(new Text("t", TypeRole.Label), new Text("p", TypeRole.Label)));

        element.ClassName.Should().Be("eq-anchorhost");
        element.Children.Should().HaveCount(1, "the panel and scrim exist only while Open");
    }

    [Fact]
    public void Open_AddsPlacementPanel_AndGapMargin()
    {
        var element = Lower(new Anchored(new Text("t", TypeRole.Label), new Text("p", TypeRole.Label))
        {
            Open = true,
            Placement = AnchorPlacement.BottomEnd,
        });

        element.Children.Should().HaveCount(2, "no OnDismiss → no scrim");
        var panel = (Core.HtmlElement)element.Children[^1];
        panel.ClassName.Should().StartWith("eq-anchor-panel eq-anchor-b-end");
        panel.Style!.MarginTop.Should().Be("4px", "the default gap rides the placement axis");
    }

    [Fact]
    public void TopPlacement_PutsGapOnMarginBottom()
    {
        var element = Lower(new Anchored(new Text("t", TypeRole.Label), new Text("p", TypeRole.Label))
        {
            Open = true,
            Placement = AnchorPlacement.TopStart,
            Gap = 8,
        });

        var panel = (Core.HtmlElement)element.Children[^1];
        panel.ClassName.Should().StartWith("eq-anchor-panel eq-anchor-t-start");
        panel.Style!.MarginBottom.Should().Be("8px");
        panel.Style.MarginTop.Should().BeNull();
    }

    [Fact]
    public void Dismissible_GetsScrimPressable_BeforeThePanel()
    {
        var dismissed = false;
        var element = Lower(new Anchored(new Text("t", TypeRole.Label), new Text("p", TypeRole.Label))
        {
            Open = true,
            OnDismiss = () => dismissed = true,
        });

        element.Children.Should().HaveCount(3);
        var scrim = (Core.HtmlElement)element.Children[1];
        scrim.Render().Tag.Should().Be("button", "the scrim is a REAL pressable — ordinary event pipeline");
        scrim.ClassName.Should().EndWith("eq-anchor-scrim");
        scrim.ClassName.Should().StartWith("eq-pressable");
        scrim.OnClick.Should().NotBeNull();
        scrim.OnClick!();
        dismissed.Should().BeTrue();
    }

    [Fact]
    public void MatchAnchorWidth_AddsTheMatchClass()
    {
        var element = Lower(new Anchored(new Text("t", TypeRole.Label), new Text("p", TypeRole.Label))
        {
            Open = true,
            MatchAnchorWidth = true,
        });

        var panel = (Core.HtmlElement)element.Children[^1];
        panel.ClassName.Should().Be("eq-anchor-panel eq-anchor-b-start eq-anchor-match");
    }

    [Fact]
    public void OpenOnHover_LowersRevealHost_PanelAlwaysPresent_NoScrim()
    {
        var element = Lower(new Anchored(new Text("t", TypeRole.Label), new Text("p", TypeRole.Label))
        {
            OpenOnHover = true,
            Placement = AnchorPlacement.TopCenter,
        });

        element.ClassName.Should().Be("eq-anchorhost eq-hoverreveal");
        element.Children.Should().HaveCount(2, "anchor + panel; CSS owns visibility — no scrim ever");
        var panel = (Core.HtmlElement)element.Children[^1];
        panel.ClassName.Should().StartWith("eq-anchor-panel eq-anchor-t-center");
        // HOVER BRIDGE: hover-open panels carry the gap as PADDING (part of the hoverable area) —
        // a margin gap drops the host's :hover for a few pixels and the panel vanishes en route.
        panel.Style!.PaddingBottom.Should().Be("4px", "TopCenter rides the bottom of the placement axis");
        panel.Style.MarginBottom.Should().BeNull();
    }

    [Fact]
    public void Motion_KeepsPanelMountedWhileClosed_HiddenAndLifted()
    {
        var element = Lower(new Anchored(new Text("t", TypeRole.Label), new Text("p", TypeRole.Label))
        {
            Motion = new TransitionSpec(StyleChannels.Opacity | StyleChannels.Transform, 300),
        });

        element.Children.Should().HaveCount(2, "motion keeps the panel MOUNTED across open/close");
        var panel = (Core.HtmlElement)element.Children[^1];
        panel.Style!.Opacity.Should().Be("0");
        panel.Style.Visibility.Should().Be("hidden", "a closed panel leaves hit-testing and the Tab order");
        panel.Style.PointerEvents.Should().Be("none");
        panel.Style.Transform.Should().Be("translateY(-8px)", "bottom placements lift TOWARD the anchor");
        panel.Style.Transition.Should()
            .Be("opacity 300ms cubic-bezier(0.2, 0, 0, 1), transform 300ms cubic-bezier(0.2, 0, 0, 1), "
                + "visibility 300ms cubic-bezier(0.2, 0, 0, 1)");
    }

    [Fact]
    public void Motion_Open_ClearsTheClosedState_AndKeepsTheTransition()
    {
        var element = Lower(new Anchored(new Text("t", TypeRole.Label), new Text("p", TypeRole.Label))
        {
            Open = true,
            Motion = new TransitionSpec(StyleChannels.Opacity, 200),
        });

        var panel = (Core.HtmlElement)element.Children[^1];
        panel.Style!.Opacity.Should().BeNull();
        panel.Style.Visibility.Should().BeNull();
        panel.Style.Transform.Should().BeNull();
        panel.Style.Transition.Should().NotBeNull("the glide must exist BEFORE the state flips back");
    }

    [Fact]
    public void Motion_CrossFadesTheScrim_AndKeepsTheAnchorLiftedWhileClosed()
    {
        var element = Lower(new Anchored(new Text("t", TypeRole.Label), new Text("p", TypeRole.Label))
        {
            OnDismiss = () => { },
            ScrimStyle = new BoxStyle { Background = Theme.Scrim },
            Motion = new TransitionSpec(StyleChannels.Opacity, 300),
        });

        element.Children.Should().HaveCount(3, "anchor + scrim + panel stay mounted");
        var anchor = (Core.HtmlElement)element.Children[0];
        anchor.ClassName.Should().Contain("eq-anchor-above",
            "the scrim is still fading out after close — the anchor must not dip behind it");
        var scrim = (Core.HtmlElement)element.Children[1];
        scrim.Style!.Opacity.Should().Be("0");
        scrim.Style.Visibility.Should().Be("hidden");
        scrim.Style.Transition.Should()
            .Be("opacity 300ms cubic-bezier(0.2, 0, 0, 1), visibility 300ms cubic-bezier(0.2, 0, 0, 1)");
    }
}
