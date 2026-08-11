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

    /// <summary>
    /// ONE atom carrying BOTH names — not two.
    /// <para>
    /// Two atoms is what shipped, and the prefixed one was dead on arrival: alone in a rule, an
    /// engine that only takes the standard name drops the whole declaration (measured —
    /// <c>insertRule</c> leaves an empty rule in Chromium), so the class was computed, hashed,
    /// emitted, put on the element, and did nothing. The standard property LAST, so an engine that
    /// understands both lands on it.
    /// </para>
    /// </summary>
    [Fact]
    public void BackdropBlur_RidesTheAtomicPipeline_AsOneAtomNamingBothEngines()
    {
        var sink = new StyleSink();
        WebRealizer.Lower(new Primitives.Box(new BoxStyle { BackdropBlur = 8, Width = SizeValue.Fill }), Theme, 1f, sink);

        sink.Css.Should().Contain("{-webkit-backdrop-filter:blur(8px);backdrop-filter:blur(8px)}");
        sink.Css.Should().NotContain("{-webkit-backdrop-filter:blur(8px)}",
            "the prefix alone is the rule that gets dropped");
    }
}
