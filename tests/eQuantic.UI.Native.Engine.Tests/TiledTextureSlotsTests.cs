using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A <see cref="DrawCommandKind.TiledTexture"/> command carries its tiling in slots other kinds use for
/// something else (#1937): the opacity in <see cref="DrawCommand.StrokeWidth"/>, the extent and the
/// repeat flags in the shape's radii, the origin and the period in the paint's gradient points. What
/// the builder writes there is what <see cref="DrawCommand.Tiling"/> reads back, and what
/// <see cref="DrawUniforms.TryBuildTiled"/> packs for the shader, so a change to one side of the reuse
/// that the other does not follow fails here rather than as a tile drawn in the wrong place.
/// </summary>
public class TiledTextureSlotsTests
{
    private static readonly TextureData Tile = TextureData.Rgba(4, 2, new byte[4 * 2 * 4]);

    /// <summary>Every field different from every other, and a period wider than the extent on one axis
    /// only, so a slot read in place of another shows.</summary>
    private static readonly Tiling Spaced = new(new Point(-7.25f, 3.5f), new Size(41.5f, 30), new Size(33.25f, 30),
        RepeatX: true, RepeatY: false);

    private static DrawCommand Recorded(Tiling tiling, float opacity)
    {
        var builder = new DisplayListBuilder();
        builder.TiledTexture(new Rect(10, 20, 300, 200), Tile, tiling, opacity);
        return builder.Build().Commands.ToArray().Single();
    }

    [Fact]
    public void TheTiling_TheBuilderWrites_IsTheTilingTheCommandReadsBack()
    {
        var command = Recorded(Spaced, 0.4f);

        command.Kind.Should().Be(DrawCommandKind.TiledTexture);
        command.Tiling.Should().Be(Spaced);
        command.Tiling.Origin.Should().Be(new Point(-7.25f, 3.5f));
        command.Tiling.Period.Should().Be(new Size(41.5f, 30));
        command.Tiling.Extent.Should().Be(new Size(33.25f, 30));
        command.Tiling.RepeatX.Should().BeTrue();
        command.Tiling.RepeatY.Should().BeFalse();
        command.StrokeWidth.Should().Be(0.4f, "the opacity rides the stroke width's slot");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void EachPairOfRepeatFlags_RoundTrips(bool across, bool down)
    {
        var tiling = Spaced with { RepeatX = across, RepeatY = down };
        Recorded(tiling, 1).Tiling.Should().Be(tiling);
    }

    [Theory]
    [InlineData(-0.5f, 0f)]
    [InlineData(1.5f, 1f)]
    public void AnOpacityOutsideZeroToOne_IsClampedWhereItIsWritten(float opacity, float written) =>
        Recorded(Spaced, opacity).StrokeWidth.Should().Be(written);

    [Fact]
    public void TheShaderBlock_PacksTheOpacityFromTheSlotTheBuilderWrites()
    {
        var command = Recorded(Spaced, 0.4f);

        DrawUniforms.TryBuildTiled(command, Tile, out var uniforms).Should().BeTrue();

        uniforms.ColorA.W.Should().BeApproximately(0.4f, 1e-6f,
            "the command's own opacity: a group's is the layer's composite, never the command's (#1952)");
        uniforms.ColorA.X.Should().Be(33.25f);
        uniforms.ColorA.Y.Should().Be(30);
        uniforms.Radii.Should().Be(new Float4(-7.25f, 3.5f, 41.5f, 30), "the origin, then the period");
        uniforms.ColorB.Should().Be(new Float4(1, 0, 0, 0),
            "it repeats across without touching the next tile, and holds cell 0 alone down");
        uniforms.Gradient.Should().Be(new Float4(4, 2, 0, 0), "the texture's size");
    }

    [Fact]
    public void EveryOtherKind_ReadsNoTiling()
    {
        var builder = new DisplayListBuilder();
        builder.FillRect(new Rect(0, 0, 10, 10), Paint.Solid(Color.White));
        builder.Build().Commands.ToArray().Single().Tiling.Should().Be(default(Tiling));
    }
}
