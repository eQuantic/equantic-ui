using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A <c>Positioned</c> that comes out of a COMPONENT still positions.
/// <para>
/// It is a contract with the parent — like a flex weight, it means nothing anywhere else — and both
/// realizers asked <c>child is Positioned</c>, which a component wrapping one does not satisfy. The
/// child then degraded to the ordinary flow: a corner button rendered ABOVE the slab it belonged
/// to, hard against the left edge, silently. A button in the wrong place is worse than no button.
/// </para>
/// </summary>
public class PositionedThroughComponentTests
{
    private sealed class CornerAction : Primitives.StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            new Positioned(new Primitives.Box(new BoxStyle { Width = 32, Height = 32 }), top: 0, end: 0);
    }

    private static string Css(VisualNode node)
    {
        var sink = new StyleSink();
        WebRealizer.Lower(node, PhotonTheme.Instance, 1f, sink);
        return sink.Css;
    }

    private static Stack WithCorner(VisualNode corner)
    {
        var stack = new Stack();
        stack.Add(new Primitives.Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Background = PhotonTheme.Instance.Surface,
        }, new Text("code", TypeRole.BodyM)));
        stack.Add(corner);
        return stack;
    }

    [Fact]
    public void AComponentReturningPositioned_IsStillPositioned()
    {
        Css(WithCorner(new CornerAction())).Should().Contain("position:absolute");
    }

    /// <summary>The direct form was never broken — this is what the component form has to match.</summary>
    [Fact]
    public void ItMatchesTheDirectForm()
    {
        var direct = new Positioned(new Primitives.Box(new BoxStyle { Width = 32, Height = 32 }), top: 0, end: 0);

        Css(WithCorner(new CornerAction())).Should().Contain("position:absolute");
        Css(WithCorner(direct)).Should().Contain("position:absolute");
    }
}
