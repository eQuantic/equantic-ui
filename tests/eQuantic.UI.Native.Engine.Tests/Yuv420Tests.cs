using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The camera frame's arithmetic, away from any camera: the colour matrix, the strides a sensor
/// really uses, and the turn that stands a sideways frame upright. Each of the three fails silently
/// on a device, as a tinted, sheared or lying-down picture nobody can diff.
/// </summary>
public class Yuv420Tests
{
    /// <summary>A 4x2 frame whose four 2x2 blocks are four colours, in planar 4:2:0.</summary>
    private static (byte[] Y, byte[] U, byte[] V) Frame((byte Y, byte U, byte V) left, (byte Y, byte U, byte V) right)
    {
        byte[] y = [left.Y, left.Y, right.Y, right.Y, left.Y, left.Y, right.Y, right.Y];
        byte[] u = [left.U, right.U];
        byte[] v = [left.V, right.V];
        return (y, u, v);
    }

    private static (byte R, byte G, byte B, byte A) Pixel(byte[] rgba, int width, int x, int y) =>
        (rgba[(y * width + x) * 4], rgba[(y * width + x) * 4 + 1], rgba[(y * width + x) * 4 + 2], rgba[(y * width + x) * 4 + 3]);

    [Fact]
    public void GreyHasNoChroma_AndTheExtremesAreBlackAndWhite()
    {
        var (y, u, v) = Frame((0, 128, 128), (255, 128, 128));
        var rgba = new byte[4 * 2 * 4];

        Yuv420.ToRgba(y, 4, u, v, 2, 1, 4, 2, 0, rgba);

        Pixel(rgba, 4, 0, 0).Should().Be(((byte)0, (byte)0, (byte)0, (byte)255));
        Pixel(rgba, 4, 3, 1).Should().Be(((byte)255, (byte)255, (byte)255, (byte)255));
    }

    /// <summary>
    /// The JFIF primaries, as full-range BT.601 gives them. Red and blue swapped is the classic
    /// defect (Cb read where Cr is), and it shows as a blue face, so both are pinned.
    /// </summary>
    [Fact]
    public void ThePrimariesComeOutAsThemselves()
    {
        var (y, u, v) = Frame((76, 85, 255), (29, 255, 107));   // red, blue
        var rgba = new byte[4 * 2 * 4];

        Yuv420.ToRgba(y, 4, u, v, 2, 1, 4, 2, 0, rgba);

        var red = Pixel(rgba, 4, 0, 0);
        red.R.Should().BeGreaterThan(250);
        red.G.Should().BeLessThan(5);
        red.B.Should().BeLessThan(5);

        var blue = Pixel(rgba, 4, 3, 0);
        blue.R.Should().BeLessThan(5);
        blue.G.Should().BeLessThan(5);
        blue.B.Should().BeGreaterThan(250);
    }

    /// <summary>
    /// What a phone's camera hands over: rows padded past the width, and the two chroma planes as
    /// views of ONE interleaved buffer, a byte apart, each stepping two bytes per sample.
    /// </summary>
    [Fact]
    public void PaddedRowsAndInterleavedChromaReadTheSamePicture()
    {
        var (planarY, planarU, planarV) = Frame((76, 85, 255), (29, 255, 107));
        var expected = new byte[4 * 2 * 4];
        Yuv420.ToRgba(planarY, 4, planarU, planarV, 2, 1, 4, 2, 0, expected);

        // Luma rows of 4 padded to 6; chroma interleaved U V U V, padded to a row of 6.
        byte[] y = [76, 76, 29, 29, 9, 9, 76, 76, 29, 29, 9, 9];
        byte[] uv = [85, 255, 255, 107, 9, 9];
        var actual = new byte[4 * 2 * 4];

        Yuv420.ToRgba(y, 6, uv, uv.AsSpan(1), 6, 2, 4, 2, 0, actual);

        actual.Should().Equal(expected);
    }

    /// <summary>
    /// A frame marked in one corner, turned each way. A quarter turn clockwise takes the top-left of
    /// a 4x2 frame to the top-right of a 2x4 one, which is where a back camera's first pixel belongs
    /// on a phone held upright.
    /// </summary>
    [Theory]
    [InlineData(0, 4, 2, 0, 0)]
    [InlineData(1, 2, 4, 1, 0)]
    [InlineData(2, 4, 2, 3, 1)]
    [InlineData(3, 2, 4, 0, 3)]
    [InlineData(5, 2, 4, 1, 0)]
    [InlineData(-1, 2, 4, 0, 3)]
    public void AQuarterTurnMovesTheMarkedCorner(int turns, int width, int height, int x, int y)
    {
        // Luma 255 at the source's top-left pixel, 0 everywhere else; no chroma.
        byte[] luma = [255, 0, 0, 0, 0, 0, 0, 0];
        byte[] grey = [128, 128];

        Yuv420.Turned(4, 2, turns).Should().Be((width, height));
        var rgba = new byte[4 * 2 * 4];
        Yuv420.ToRgba(luma, 4, grey, grey, 2, 1, 4, 2, turns, rgba);

        var lit = Enumerable.Range(0, 8).Where(index => rgba[index * 4] == 255).ToArray();
        lit.Should().Equal([y * width + x], "exactly the marked pixel is lit, where the turn put it");
    }

    /// <summary>Every pixel is written once: a turn that lost or doubled one would leave a hole.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ATurnWritesEveryPixelOnce(int turns)
    {
        const int width = 6, height = 4;
        var luma = Enumerable.Range(0, width * height).Select(index => (byte)(index + 1)).ToArray();
        var grey = Enumerable.Repeat((byte)128, width * height / 4).ToArray();
        var rgba = new byte[width * height * 4];

        Yuv420.ToRgba(luma, width, grey, grey, width / 2, 1, width, height, turns, rgba);

        Enumerable.Range(0, width * height).Select(index => rgba[index * 4]).OrderBy(value => value)
            .Should().Equal(luma.OrderBy(value => value), "the turned frame holds the same pixels, each once");
        Enumerable.Range(0, width * height).Select(index => rgba[index * 4 + 3]).Should().OnlyContain(alpha => alpha == 255);
    }

    [Fact]
    public void ATargetSmallerThanTheFrameIsRefused()
    {
        var act = () => Yuv420.ToRgba(new byte[8], 4, new byte[2], new byte[2], 2, 1, 4, 2, 0, new byte[8]);
        act.Should().Throw<ArgumentException>();
    }
}
