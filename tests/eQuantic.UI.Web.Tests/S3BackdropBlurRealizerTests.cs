using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Spec S3 frosted glass on the web realizer: BoxStyle.BackdropBlur → CSS backdrop-filter, with the
/// -webkit- twin Safari still requires. The declaration strings are pinned LITERALLY — the TS
/// lowering mirrors them character-for-character (hydration parity by class identity), and the
/// native realizer consumes the same property as a BackdropBlur pass split.
/// </summary>
public class S3BackdropBlurRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static string StyleOf(VisualNode node)
    {
        var element = WebRealizer.Lower(node, Theme);
        return element.Style!.ToCssString();
    }

    [Fact]
    public void BackdropBlur_LowersToBothDeclarations()
    {
        var css = StyleOf(new Primitives.Box(new BoxStyle { BackdropBlur = 12, Width = 10, Height = 10 }));
        css.Should().Contain("backdrop-filter: blur(12px)")
            .And.Contain("-webkit-backdrop-filter: blur(12px)");
    }

    [Fact]
    public void ZeroBlur_EmitsNothing()
    {
        StyleOf(new Primitives.Box(new BoxStyle { Width = 10, Height = 10 }))
            .Should().NotContain("backdrop-filter");
    }

    [Fact]
    public void BackdropBlur_RidesTheAtomicPipeline_AsTwoAtoms()
    {
        var sink = new StyleSink();
        WebRealizer.Lower(new Primitives.Box(new BoxStyle { BackdropBlur = 8, Width = SizeValue.Fill }), Theme, 1f, sink);

        sink.Css.Should().Contain("{backdrop-filter:blur(8px)}")
            .And.Contain("{-webkit-backdrop-filter:blur(8px)}");
    }
}
