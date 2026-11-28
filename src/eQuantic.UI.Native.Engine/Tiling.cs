using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine;

/// <summary>
/// How a <see cref="DrawCommandKind.TiledTexture"/> repeats its texture over its area (#1937): the
/// NORMATIVE math, which the Reference backend calls and <c>tiled_rgba_fragment</c> in
/// <c>Sdf.slang</c> transliterates, as that file does for <see cref="Sdf"/>.
/// <para>
/// Every length is in the command's LOCAL space, the space its <see cref="DrawCommand.Shape"/> is in,
/// and none is ever moved to a device pixel: the period and the phase are the exact ones layout gives,
/// so the tenth tile of 330 dp at 2.625 device pixels a dp starts at 7796.25, where a browser that
/// does not snap draws it, and not at 7794. The texture holds one tile at its device size rounded up,
/// and a pixel samples it at the fraction of the tile it falls in, with linear filtering, so no pixel
/// falls between two tiles and none is drawn by both.
/// </para>
/// </summary>
/// <param name="Origin">Where the tile of cell (0, 0) starts: the anchored tile's top-left corner.</param>
/// <param name="Period">From one tile's start to the next on each axis: the tile, and the spaced gap.</param>
/// <param name="Extent">The tile itself on each axis; at most the period, and less by the spaced gap.</param>
/// <param name="RepeatX">Whether the tiles repeat across; false keeps cell 0 alone.</param>
/// <param name="RepeatY">Whether the tiles repeat down; false keeps cell 0 alone.</param>
public readonly record struct Tiling(Point Origin, Size Period, Size Extent, bool RepeatX, bool RepeatY)
{
    /// <summary>Whether a tile touches the next one across, so the filter reads across the seam into the
    /// next tile's first column (repeat addressing) rather than holding its own last one (clamp).</summary>
    public bool WrapsX => RepeatX && Extent.Width >= Period.Width;

    /// <summary>Whether a tile touches the next one down: <see cref="WrapsX"/>'s rule on the other axis.</summary>
    public bool WrapsY => RepeatY && Extent.Height >= Period.Height;

    /// <summary>
    /// Where <paramref name="local"/> falls in a texture of <paramref name="width"/> by
    /// <paramref name="height"/> texels that holds one tile, in texel units (0 to the size, the texel
    /// centres at halves), or false where no tile is: a cell an axis that does not repeat leaves out,
    /// or the spaced gap after a tile. The cell is <c>floor((local − origin) ÷ period)</c>, so it is
    /// negative left of and above the origin, and the tiles continue there.
    /// </summary>
    public bool TryTexel(Point local, int width, int height, out float x, out float y)
    {
        y = 0;
        return Axis(local.X - Origin.X, Period.Width, Extent.Width, RepeatX, width, out x)
            && Axis(local.Y - Origin.Y, Period.Height, Extent.Height, RepeatY, height, out y);
    }

    /// <summary>
    /// The texture at texel position (<paramref name="x"/>, <paramref name="y"/>), filtered linearly
    /// between the four texels around it in PREMULTIPLIED linear light, so a transparent texel lends its
    /// neighbour no colour. An axis that wraps reads past its edge into the texture's other edge; one
    /// that does not holds its edge texel. <paramref name="texture"/> is <see cref="TextureFormat.Rgba8"/>,
    /// straight sRGB.
    /// </summary>
    public LinearColor Sample(TextureData texture, float x, float y)
    {
        var fx = x - 0.5f;
        var fy = y - 0.5f;
        var x0 = MathF.Floor(fx);
        var y0 = MathF.Floor(fy);
        var ax = fx - x0;
        var ay = fy - y0;
        var left = Index((int)x0, texture.Width, WrapsX);
        var right = Index((int)x0 + 1, texture.Width, WrapsX);
        var top = Index((int)y0, texture.Height, WrapsY);
        var bottom = Index((int)y0 + 1, texture.Height, WrapsY);
        var above = Lerp(Texel(texture, left, top), Texel(texture, right, top), ax);
        var below = Lerp(Texel(texture, left, bottom), Texel(texture, right, bottom), ax);
        return Lerp(above, below, ay);
    }

    /// <summary>One axis of <see cref="TryTexel"/>: the cell, the position inside it, and the texel.</summary>
    private static bool Axis(float offset, float period, float extent, bool repeats, int size, out float texel)
    {
        texel = 0;
        if (!(period > 0) || !(extent > 0) || size <= 0) return false;
        var cell = MathF.Floor(offset / period);
        var within = offset - cell * period;
        // A division a GPU computes through a reciprocal can land a hair either side of a whole cell, so the
        // cell is settled by the remainder, which both sides compute alike: a point exactly on a tile's start
        // is in that tile on every backend.
        if (within < 0) { cell -= 1; within += period; }
        else if (within >= period) { cell += 1; within -= period; }
        if (!repeats && cell != 0) return false;
        // The spaced gap, where a tile is shorter than its period. A tile that fills its period has no
        // gap, so a rounding that lands on its far edge is the seam, not a hole.
        if (within >= extent && extent < period) return false;
        texel = MathF.Min(within / extent, 1f) * size;
        return true;
    }

    /// <summary>A texel index on an axis of <paramref name="size"/>: wrapped, or held at the edge.</summary>
    private static int Index(int index, int size, bool wraps) => wraps
        ? ((index % size) + size) % size
        : Math.Clamp(index, 0, size - 1);

    private static LinearColor Texel(TextureData texture, int x, int y)
    {
        var i = (y * texture.Width + x) * 4;
        var pixels = texture.Alpha;
        return ColorSpace.ToPremultipliedLinear(new Color(pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]));
    }

    private static LinearColor Lerp(LinearColor a, LinearColor b, float t) => new(
        a.R + (b.R - a.R) * t,
        a.G + (b.G - a.G) * t,
        a.B + (b.B - a.B) * t,
        a.A + (b.A - a.A) * t);
}
