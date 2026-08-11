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

/// <summary>
/// A positioned child that FILLS spans its stack, instead of shrink-wrapping to its content.
/// <para>
/// An absolutely-positioned box with one edge shrink-wraps; with two it spans between them. Native
/// measures a positioned child against the stack's full extent, so the same tree came out 768 wide
/// there and 64 here — a corner row whose Spacer had nothing to push against, so the button landed
/// at the top LEFT, on top of the first line of code.
/// </para>
/// </summary>
public class PositionedSpanTests
{
    private static string Css(VisualNode node)
    {
        var sink = new StyleSink();
        WebRealizer.Lower(node, PhotonTheme.Instance, 1f, sink);
        return sink.Css;
    }

    private static Stack Corner(VisualNode child)
    {
        var stack = new Stack { Width = SizeValue.Fill };
        stack.Add(new Primitives.Box(new BoxStyle { Width = SizeValue.Fill, Height = 100 }));
        stack.Add(new Positioned(child, top: 0, end: 0));
        return stack;
    }

    private static Row FillingRow()
    {
        var row = new Row(gap: 0) { Width = SizeValue.Fill };
        row.Add(new Spacer(1));
        row.Add(new Primitives.Box(new BoxStyle { Width = 32, Height = 32 }));
        return row;
    }

    [Fact]
    public void AFillingChild_SpansBetweenBothEdges()
    {
        var css = Css(Corner(FillingRow()));

        css.Should().Contain("left:0", "the opposite edge is what gives it a definite width");
        css.Should().Contain("right:0");
    }

    /// <summary>A child that does NOT fill still shrink-wraps — pinning both edges on a plain badge
    /// would stretch it across the slab.</summary>
    [Fact]
    public void AHuggingChild_StaysWhereItWasPut()
    {
        var css = Css(Corner(new Primitives.Box(new BoxStyle { Width = 32, Height = 32 })));

        css.Should().NotContain("left:0");
    }

    /// <summary>Both edges given explicitly is the author saying it already — nothing to infer.</summary>
    [Fact]
    public void BothEdgesGiven_AreLeftAlone()
    {
        var stack = new Stack { Width = SizeValue.Fill };
        stack.Add(new Positioned(FillingRow(), top: 0, start: 8, end: 8));

        Css(stack).Should().Contain("left:8px").And.Contain("right:8px");
    }
}
