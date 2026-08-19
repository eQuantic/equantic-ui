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
/// (<c>PhotonRealizer.ExpandHitRect</c>). On the web nothing kept it: every small pressable shipped
/// with its visual as its hit rect, and the three components that cared sized a target by hand. A
/// promise a framework makes and does not keep is worse than one it never made, because every
/// author who read it stopped checking.
/// </para>
/// </summary>
public class HitSlopTests
{
    private static string Css() => PhotonCssGenerator.Generate(PhotonTheme.Instance);

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
        css.Should().Contain(".eq-pressable::after");
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
    /// A POINTER lands where it is aimed. Expanding a dense toolbar's buttons would grow each one
    /// into its neighbour, which is exactly why Photon skips the expansion in Compact — the media
    /// query is the browser answering the same question.
    /// </summary>
    [Fact]
    public void APointerDeviceIsLeftAlone()
    {
        var css = Css();
        var slop = css.IndexOf(".eq-pressable::after", StringComparison.Ordinal);
        var gate = css.IndexOf("@media (pointer: coarse)", StringComparison.Ordinal);

        gate.Should().BeGreaterThan(-1);
        slop.Should().BeGreaterThan(gate, "the slop lives inside the coarse-pointer gate");
    }
}
