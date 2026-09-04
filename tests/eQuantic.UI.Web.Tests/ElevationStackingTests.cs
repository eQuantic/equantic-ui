using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Elevation decides what is ON TOP, not only how deep the shadow is.
/// <para>
/// It drew a shadow and nothing else, which is half a sentence: a raised surface that anything
/// painted after it covers is not raised. Chrome that pins while the page scrolls is the case that
/// names it — content passed over the header — and the cures on offer were both wrong. A `ZIndex`
/// on the node is CSS vocabulary, which the abstract layer does not speak; moving the chrome into a
/// layer of its own is what `Overlay` is, and a pinned header is not that (it is in the flow until
/// it pins).
/// </para>
/// <para>
/// This is the web realizer keeping a promise Photon already keeps for free: paint order IS child
/// order there, so a later sibling is on top and nothing has to be said. CSS does not work that way
/// once boxes acquire stacking contexts, so on the web the same sentence has to be spelled.
/// </para>
/// </summary>
public class ElevationStackingTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static string StyleOf(VisualNode node) =>
        WebRealizer.Lower(node, Theme).Style!.ToCssString();

    [Fact]
    public void AnElevatedBox_IsAlsoStackedAboveItsSiblings()
    {
        var css = StyleOf(new Box(new BoxStyle { Elevation = 2, Width = 40, Height = 40 }));

        css.Should().Contain("z-index: 2", "elevation is the design system's word for above");
        css.Should().Contain("position: relative", "z-index needs a positioned box");
        css.Should().Contain("box-shadow", "and it still casts the shadow it always did");
    }

    /// <summary>Level 0 is the page's own plane: no shadow, and nothing to stack.</summary>
    [Fact]
    public void AFlatBox_GainsNeitherAStackingOrderNorAPosition()
    {
        var css = StyleOf(new Box(new BoxStyle { Width = 40, Height = 40 }));

        css.Should().NotContain("z-index").And.NotContain("position");
    }

    /// <summary>
    /// THE REPORTED BUG, and the reason elevation alone would have made it worse.
    /// <para>
    /// Pinned chrome sat at z-index 1 — one step above nothing, a number competing with other
    /// numbers. Give elevation the 1–5 range and a merely RAISED card out-stacks the header and
    /// scrolls over it. Chrome is not slightly-raised content; it is a different plane, and the
    /// realizer says so instead of leaving the author to pick a bigger number.
    /// </para>
    /// </summary>
    [Fact]
    public void PinnedChrome_OutranksTheMostElevatedContentThereIs()
    {
        var chrome = Layer(StyleOf(new Pinned(new Box(new BoxStyle { Height = 56 }), offset: 0)));
        var floating = Layer(StyleOf(new Pinned(new Box(new BoxStyle { Height = 56 })) { Float = true }));
        // 5 is the top of the elevation scale — the highest a page can raise its own content.
        var raised = Layer(StyleOf(new Box(new BoxStyle { Elevation = 5, Width = 40, Height = 40 })));

        chrome.Should().BeGreaterThan(raised, "a pinned header is a plane, not a raised card");
        floating.Should().BeGreaterThan(raised, "and so is a floating one");
        // This line used to read `floating.Should().Be(chrome)` — both are chrome, differing only
        // in whether they take space. True right up until a page had BOTH, which the site then
        // did: a pinned rail and a floating header tied on one number, so the winner was whichever
        // came later in the document, and the rail painted over the header's open mega menu. The
        // header's own panel could not climb out either — its z-index lives inside the header's
        // stacking context and never rises above a sibling of the header. Two chrome elements on
        // one plane is the page's structure deciding a paint order nobody chose.
        floating.Should().BeGreaterThan(chrome,
            "a header whose dropdown falls behind the page is not a trade-off to offer an author");
    }

    private static int Layer(string css) =>
        int.Parse(System.Text.RegularExpressions.Regex.Match(css, @"z-index: (\d+)").Groups[1].Value);
}
