using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A draggable's resting offset beside its box's own transform, on the web (#511). CSS keeps ONE
/// <c>transform</c> per element, and the offset used to be written into it, so the two replaced each
/// other: a box with a resting transform inside an open row lost it, and the box's hover transform
/// (0,2,0) beat the offset's class (0,1,0), sliding an open row closed under the pointer. The offset
/// rides the individual <c>translate</c> property now, which CSS applies together with
/// <c>transform</c>, as Photon translates the subtree it wraps and composes the box's own inside it.
/// </summary>
public class DraggableRealizerTests
{
    private static readonly PhotonTheme Theme = PhotonTheme.Instance;

    private static readonly Transform2D Half = Transform2D.Scale(0.5f);

    private static Draggable Draggable(BoxStyle style, float rest, DragAxis axis = DragAxis.Horizontal) =>
        new(new Box(style), _ => { }) { Axis = axis, Min = -80, Max = 80, RestOffset = rest };

    private static string StyleOf(VisualNode node) =>
        WebRealizer.Lower(node, Theme).Render().Attributes["style"]!;

    [Fact]
    public void TheRestOffset_KeepsTheBoxsOwnTransform()
    {
        var style = StyleOf(Draggable(new BoxStyle { Width = 40, Height = 40, Transform = Half }, rest: -80));

        style.Should().Contain($"transform: {TokenCss.Transform(Half)}", "the box's own transform stays")
            .And.Contain("translate: -80px", "and the offset rides beside it");
    }

    [Fact]
    public void AVerticalRestOffset_TranslatesAlongY()
    {
        var style = StyleOf(Draggable(new BoxStyle { Width = 40, Height = 40 }, rest: 24, DragAxis.Vertical));

        style.Should().Contain("translate: 0 24px");
        style.Should().NotContain("transform:", "a box with no transform of its own writes none");
    }

    [Fact]
    public void TheOffsetsGlide_JoinsTheBoxsOwnTransition()
    {
        var spec = new TransitionSpec(StyleChannels.Colors);
        var style = StyleOf(Draggable(new BoxStyle { Width = 40, Height = 40, Transition = spec }, rest: -80));

        style.Should().Contain($"transition: {TokenCss.Transition(spec)}, translate {Motion.BaseMs}ms",
            "one transition list: the offset's glide used to replace the box's colour fade");
    }

    /// <summary>
    /// At rest at zero the row still declares the glide: a release that leaves the offset where it
    /// was (a swipe short of the threshold) hands the position back to the markup, and the surface
    /// glides home instead of staying where the finger left it.
    /// </summary>
    [Fact]
    public void AFollowingDraggableAtZero_StillDeclaresTheGlide()
    {
        var style = StyleOf(Draggable(new BoxStyle { Width = 40, Height = 40 }, rest: 0));

        style.Should().Contain($"transition: translate {Motion.BaseMs}ms");
        style.Should().NotContain("translate: ", "nothing to offset at zero");
    }

    [Fact]
    public void ADraggableTheCallerPaints_WritesNoOffsetAndNoGlide()
    {
        var node = new Draggable(new Box(new BoxStyle { Width = 40, Height = 40 }), _ => { })
        {
            Axis = DragAxis.Horizontal, Min = 0, Max = 1, RestOffset = 0.5f, Follows = false,
        };

        StyleOf(node).Should().NotContain("translate");
    }

    /// <summary>The server-rendered row under the pointer: the box's hover moves it, and the open
    /// offset stays, since the two are different properties now.</summary>
    [Fact]
    public void AHoverTransform_DoesNotTakeTheOffsetsProperty()
    {
        var sink = new StyleSink();
        var node = WebRealizer.Lower(Draggable(new BoxStyle
        {
            Width = 40, Height = 40,
            Hover = new StyleDiff { Transform = Transform2D.Translate(0, -2) },
        }, rest: -80), Theme, styles: sink).Render();
        var css = sink.Css;

        var classes = node.Attributes["class"]!.Split(' ');
        classes.Should().Contain(cls => css.Contains($".{cls}{{translate:-80px}}"),
            "the offset is a class of its own");
        css.Should().Contain(":hover{transform:", "and the hover writes the box's transform, not the offset");
    }
}
