using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A translation in fractions of the box's own size on the web (#614): a CSS translate percentage is
/// read against the element itself, which is what a fraction means, so it crosses as a percentage, and
/// as a <c>calc</c> where units are added. The Photon half is <c>FractionalTranslationTests</c>, and the
/// TypeScript producer's is <c>s1-style.spec.ts</c>.
/// </summary>
public class FractionalTranslationRealizerTests
{
    [Theory]
    [InlineData(-1.3f, 0f, 0f, 0f, "translate(-130%, 0)")]
    [InlineData(0f, 0f, 0.5f, 0f, "translate(0, 50%)")]
    [InlineData(-1f, 2f, 0f, 0f, "translate(calc(-100% + 2px), 0)")]
    [InlineData(0f, 0f, 0f, 3f, "translate(0, 3px)")]
    public void AFraction_IsAPercentageOfTheElement(float fx, float x, float fy, float y, string expected) =>
        TokenCss.Transform(Transform2D.Translate(x, y).WithFractionalTranslate(fx, fy)).Should().Be(expected);

    [Fact]
    public void AFraction_RidesWithTheRestOfTheTransform() =>
        TokenCss.Transform(Transform2D.FractionalTranslate(-1.3f).WithRotate(12)).Should()
            .Be("translate(-130%, 0) rotate(12deg)");
}
