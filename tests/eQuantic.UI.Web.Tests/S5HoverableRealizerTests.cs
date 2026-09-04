using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Spec S5's PROGRAMMABLE hover on web — <see cref="Hoverable"/> lowers to a layout-transparent
/// div whose mouseenter/mouseleave feed the boolean callback (the low-level element events the
/// hydration pipeline already attaches). The native twin rides the PhotonHost pointer pipeline
/// (S5HoverableNativeTests).
/// </summary>
public class S5HoverableRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    [Fact]
    public void Hoverable_WiresEnterAndLeave_ToTheBooleanCallback()
    {
        var log = new List<bool>();
        var element = WebRealizer.Lower(
            new Hoverable(new Box(new BoxStyle { Width = 10, Height = 10 }), log.Add), Theme);

        element.OnMouseEnter.Should().NotBeNull();
        element.OnMouseLeave.Should().NotBeNull();

        element.OnMouseEnter!(new MouseEventArgs());
        element.OnMouseLeave!(new MouseEventArgs());
        log.Should().Equal(true, false);
    }

    [Fact]
    public void FillChild_PassesThePercentChainThrough()
    {
        var element = WebRealizer.Lower(
            new Hoverable(new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill }), _ => { }), Theme);
        element.Style!.Width.Should().Be("100%");
        element.Style.Height.Should().Be("100%");
    }

    [Fact]
    public void AnchoredScrimStyle_PaintsTheDismissScrim()
    {
        var element = WebRealizer.Lower(new Anchored(
            new Text("t", TypeRole.Label), new Text("p", TypeRole.Label))
        {
            Open = true,
            OnDismiss = () => { },
            ScrimStyle = new BoxStyle { Background = new ColorToken(new Color(5, 6, 13, 140)) },
        }, Theme);

        var scrim = (HtmlElement)element.Children[1];
        scrim.ClassName.Should().Contain("eq-anchor-scrim");
        var veil = (HtmlElement)scrim.Children[0];
        veil.Style!.BackgroundColor.Should().NotBeNull("the scrim paints the page veil");
        veil.Style.Width.Should().Be("100%", "the veil fills the fixed scrim");
    }
}
