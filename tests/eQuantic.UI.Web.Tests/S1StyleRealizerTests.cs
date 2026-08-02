using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Spec S1 on the web realizer: group opacity → CSS opacity, the center-anchored Transform2D → the
/// CSS transform list (translate → rotate → scale, same order as the native matrix), aspect-ratio,
/// and align-self overriding the container's cross alignment. Values are pinned as the LITERAL
/// strings the TS lowering mirrors (hydration parity by class identity).
/// </summary>
public class S1StyleRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static string StyleOf(VisualNode node)
    {
        var element = WebRealizer.Lower(node, Theme);
        return element.Style!.ToCssString();
    }

    [Fact]
    public void Opacity_LowersToTheCssDeclaration()
    {
        StyleOf(new Primitives.Box(new BoxStyle { Opacity = 0.85f, Width = 10, Height = 10 }))
            .Should().Contain("opacity: 0.85");
    }

    [Fact]
    public void Transform_EmitsTheOrderedList_OnlyNonNeutralParts()
    {
        StyleOf(new Primitives.Box(new BoxStyle
        {
            Transform = Transform2D.Translate(4, -2).WithRotate(8).WithScale(1.05f),
            Width = 10, Height = 10,
        })).Should().Contain("transform: translate(4px, -2px) rotate(8deg) scale(1.05)");

        StyleOf(new Primitives.Box(new BoxStyle { Transform = Transform2D.Rotate(90), Width = 10, Height = 10 }))
            .Should().Contain("transform: rotate(90deg)")
            .And.NotContain("translate(").And.NotContain("scale(");
    }

    [Fact]
    public void AspectRatio_LowersToTheCssDeclaration()
    {
        StyleOf(new Primitives.Box(new BoxStyle { AspectRatio = 16f / 9f, Width = SizeValue.Fill }))
            .Should().Contain("aspect-ratio: 1.7778");
    }

    [Fact]
    public void AlignSelf_OverridesTheContainerCross_OnTheChildOnly()
    {
        var row = new Row(gap: Space.S2) { Cross = CrossAlign.End };
        row.Add(new Primitives.Box(new BoxStyle { Width = 10, Height = 10 }));
        row.Add(new Primitives.Box(new BoxStyle { Width = 10, Height = 10 }) { AlignSelf = CrossAlign.Start });

        var element = WebRealizer.Lower(row, Theme);
        var first = (Core.HtmlElement)element.Children[0];
        var second = (Core.HtmlElement)element.Children[1];

        first.Style!.ToCssString().Should().NotContain("align-self");
        second.Style!.ToCssString().Should().Contain("align-self: flex-start");
    }

    [Fact]
    public void S1Declarations_RideTheAtomicPipeline()
    {
        var sink = new StyleSink();
        WebRealizer.Lower(new Primitives.Box(new BoxStyle
        {
            Opacity = 0.5f,
            Transform = Transform2D.Rotate(8),
            AspectRatio = 1f,
            Width = SizeValue.Fill,
        }), Theme, 1f, sink);

        sink.Css.Should().Contain("{opacity:0.5}")
            .And.Contain("{transform:rotate(8deg)}")
            .And.Contain("{aspect-ratio:1}");
    }

    [Fact]
    public void MonoText_KeepsItsIndentation()
    {
        // Mono text is CODE: leading spaces are meaning, not formatting. HTML collapses them by
        // default, which turned every snippet on the site into a flat left-aligned block.
        var node = WebRealizer.Lower(
            new Text("    row.Add(child);", TypeRole.BodyM) { Mono = true }, PhotonTheme.Instance).Render();

        node.Attributes["style"].Should().Contain("white-space: pre-wrap");
    }
}
