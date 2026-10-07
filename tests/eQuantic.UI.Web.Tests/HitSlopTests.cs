using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Spec §08 on the web: a control's HIT rect is at least <see cref="Touch.MinTarget"/> per side,
/// however small its visual is.
///
/// <para>
/// The token's own doc has promised this from the start — "visuals may be smaller, the framework
/// expands hit-slop symmetrically" — and Photon has kept it in one place since
/// (<c>EmitVisitor.ExpandHitRect</c>). On the web nothing kept it: every small pressable shipped
/// with its visual as its hit rect, and the three components that cared sized a target by hand. A
/// promise a framework makes and does not keep is worse than one it never made, because every
/// author who read it stopped checking.
/// </para>
/// </summary>
public class HitSlopTests
{
    private static string Css() => PhotonCssGenerator.Generate(PhotonTheme.Instance);

    /// <summary>The rules inside one pointer's gate, with the spaces taken out.</summary>
    private static string Gate(string pointer)
    {
        var css = Css();
        var gate = css.IndexOf($"@media (pointer: {pointer}) {{", StringComparison.Ordinal);
        gate.Should().BeGreaterThan(-1, $"a {pointer} pointer has its gate");
        return css[gate..css.IndexOf("\n}", gate, StringComparison.Ordinal)].Replace(" ", string.Empty);
    }

    /// <summary>
    /// The rule exists, and its size comes from the TOKEN. A literal 48 here would let the token
    /// move while the stylesheet kept promising the old number.
    /// </summary>
    [Fact]
    public void APressableCarriesTheMinimumTarget()
    {
        var css = Css().Replace(" ", string.Empty);

        css.Should().Contain("@media(pointer:coarse)");
        css.Should().Contain($"min-width:{Touch.MinTarget}px");
        css.Should().Contain($"min-height:{Touch.MinTarget}px");
        css.Should().Contain(".eq-pressable::before");
    }

    /// <summary>
    /// It grows the TARGET, not the control: a pseudo-element centred on the box, never padding.
    /// Padding would move every neighbour of every small button on the page.
    /// </summary>
    [Fact]
    public void TheTargetGrowsWithoutMovingAnything()
    {
        var css = Css().Replace(" ", string.Empty);

        css.Should().Contain("transform:translate(-50%,-50%)");
        css.Should().Contain(".eq-pressable{position:relative;}",
            "the pseudo-element needs a containing block, and it is the framework's own wrapper");
    }

    /// <summary>
    /// A POINTER lands where it is aimed. Expanding a dense toolbar's buttons to a finger's minimum
    /// would grow each one into its neighbour, so a fine pointer's gate grows a target only to the
    /// floor WCAG 2.2 SC 2.5.8 asks of any target, which a 20px checkbox needs (#430), and a coarse
    /// one's to the §08 minimum. Each minimum lives inside its own gate and comes from its token:
    /// Photon's Compact and Comfortable densities ask the same two.
    /// </summary>
    [Theory]
    [InlineData("coarse", Touch.MinTarget)]
    [InlineData("fine", Touch.MinPointerTarget)]
    public void EachPointerGetsItsOwnMinimum(string pointer, float minimum)
    {
        var block = Gate(pointer);

        block.Should().Contain(".eq-pressable::before")
            .And.Contain($"min-width:{minimum}px")
            .And.Contain($"min-height:{minimum}px");
    }

    /// <summary>
    /// The target lies UNDER the control's own content, inside each gate: it is the pseudo-element
    /// that comes FIRST in tree order, and the content is positioned like it, so the content paints
    /// and is hit after it. Measured in a browser under a fine pointer (#430): with the target over
    /// the content, the centre of a Button hit the button element itself, its box never matched
    /// <c>:hover</c> and the hover fill never showed, and a Pressable around an IconButton took every
    /// hit the inner control should have had. The lift has no specificity, so a child that positions
    /// itself (a raised box, a layer of a Stack) keeps its own position.
    /// </summary>
    [Theory]
    [InlineData("coarse")]
    [InlineData("fine")]
    public void TheControlsOwnContentStaysAboveItsTarget(string pointer)
    {
        var block = Gate(pointer);

        block.Should().Contain(":where(.eq-pressable)>*,:where(.eq-lift){position:relative;}",
            "the content is lifted to the target's level, where tree order puts it on top, and so is "
            + "the content behind a child that draws no box (#622)");
        Css().Should().NotContain(".eq-pressable::after",
            "a target that comes after the content in tree order paints, and is hit, over it");
    }

    /// <summary>
    /// Each slop lives inside its own gate and nowhere else. A slop outside both gates, or the
    /// finger's minimum inside the pointer's gate, would grow every 26px button of a dense toolbar
    /// under a mouse into its neighbour, which is the reason a pointer has a gate of its own.
    /// </summary>
    [Fact]
    public void EachSlopLivesOnlyInsideItsGate()
    {
        var slops = Css().Split(".eq-pressable::before").Length - 1;

        slops.Should().Be(2, "one slop per pointer, each inside its gate");
        Gate("coarse").Should().Contain(".eq-pressable::before");
        Gate("fine").Should().Contain(".eq-pressable::before")
            .And.NotContain($"min-width:{Touch.MinTarget}px", "a mouse never gets a finger's minimum");
    }
}
