using eQuantic.UI.Native.Shell.Apple;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// MONO IS A FACE, not a hint. Every character takes the same advance — that is the whole point of
/// a code column — so the test is the only one that cannot be faked: an 'i' and an 'M' measure the
/// same, and in the proportional face they must not.
/// <para>
/// The fence this replaced said "the native rasterizer keeps the system face"; the give-away was a
/// code block on screen with proportional letters while the web twin had it right.
/// </para>
/// </summary>
public class MonoFaceTests
{
    private static readonly CoreTextService Service = new();

    private static float Width(string content, bool mono) =>
        Service.Measure(content, new TypeStyle(13, 16, FontWeight.Regular, 0f, 1.3f, mono),
            1f, float.PositiveInfinity, 1).Width;

    [Fact]
    public void EveryCharacterTakesTheSameAdvance()
    {
        Width("iiiiiiii", mono: true).Should().BeApproximately(Width("MMMMMMMM", mono: true), 0.5f,
            "a monospaced face gives every glyph one advance");
    }

    [Fact]
    public void TheProportionalFaceStillFitsToTheGlyphs()
    {
        Width("iiiiiiii", mono: false).Should().BeLessThan(Width("MMMMMMMM", mono: false) - 5,
            "asking for mono must not change the face everything else uses");
    }

    /// <summary>
    /// The style is what the measurer, the rasterizer and the raster cache all key on — so the
    /// two faces are two cache entries, and neither can serve the other's pixels.
    /// </summary>
    [Fact]
    public void TheFaceTravelsOnTheStyle()
    {
        var proportional = new TypeStyle(13, 16, FontWeight.Regular, 0f, 1.3f);
        var mono = proportional with { Mono = true };

        mono.Should().NotBe(proportional, "the cache keys on the style; two faces are two keys");
        Service.Rasterize("if (x) return;", mono, 1f, float.PositiveInfinity, 1, 2f)!.Width
            .Should().NotBe(
                Service.Rasterize("if (x) return;", proportional, 1f, float.PositiveInfinity, 1, 2f)!.Width);
    }
}
