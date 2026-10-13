namespace eQuantic.UI.Native.Engine;

/// <summary>
/// A camera frame as most sensors hand it over — one luma plane and two chroma planes at half
/// resolution, each with its own strides — turned into the straight RGBA the engine uploads as a
/// texture, and turned UPRIGHT on the way: a phone's sensor is mounted sideways, so the frame of a
/// phone held in portrait arrives lying on its side.
/// <para>
/// Full-range BT.601, the JFIF matrix a camera's preview stream is in. Fixed point, because this runs
/// once per pixel per frame on a phone.
/// </para>
/// <para>
/// HERE, in the portable engine, rather than in the one shell that calls it today: the arithmetic has
/// nothing of a platform in it, and a shell is the one assembly no test can load.
/// </para>
/// </summary>
public static class Yuv420
{
    /// <summary>The size of a frame after <see cref="ToRgba"/> turned it.</summary>
    public static (int Width, int Height) Turned(int width, int height, int quarterTurns) =>
        (quarterTurns & 1) == 0 ? (width, height) : (height, width);

    /// <summary>
    /// Converts one frame, turning it clockwise by <paramref name="quarterTurns"/> × 90°.
    /// </summary>
    /// <param name="y">The luma plane: <paramref name="height"/> rows of <paramref name="yRowStride"/> bytes.</param>
    /// <param name="yRowStride">Bytes from one luma row to the next; at least <paramref name="width"/>.</param>
    /// <param name="u">The Cb plane, at half resolution in both directions.</param>
    /// <param name="v">The Cr plane, laid out as <paramref name="u"/> is.</param>
    /// <param name="chromaRowStride">Bytes from one chroma row to the next.</param>
    /// <param name="chromaPixelStride">
    /// Bytes from one chroma sample to the next in a row: 1 for planar frames, 2 where the two
    /// chroma planes are views of one interleaved buffer.
    /// </param>
    /// <param name="width">The frame's width as the sensor delivers it.</param>
    /// <param name="height">The frame's height as the sensor delivers it.</param>
    /// <param name="quarterTurns">Clockwise quarter turns, any integer; taken modulo four.</param>
    /// <param name="rgba">
    /// Receives <see cref="Turned"/> width × height pixels, four bytes each, alpha 255.
    /// </param>
    public static void ToRgba(ReadOnlySpan<byte> y, int yRowStride,
        ReadOnlySpan<byte> u, ReadOnlySpan<byte> v, int chromaRowStride, int chromaPixelStride,
        int width, int height, int quarterTurns, Span<byte> rgba)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (rgba.Length < width * height * 4)
            throw new ArgumentException("The target is smaller than the frame.", nameof(rgba));

        var turns = ((quarterTurns % 4) + 4) % 4;
        var (outWidth, _) = Turned(width, height, turns);

        for (var row = 0; row < height; row++)
        {
            var lumaRow = row * yRowStride;
            var chromaRow = (row >> 1) * chromaRowStride;

            // Where this row's first pixel lands once the frame is upright, and how far the next
            // one is from it, in pixels of the target. Settled per row, not per pixel.
            var (target, step) = turns switch
            {
                1 => (height - 1 - row, outWidth),
                2 => ((height - 1 - row) * outWidth + width - 1, -1),
                3 => ((width - 1) * outWidth + row, -outWidth),
                _ => (row * outWidth, 1),
            };

            for (var column = 0; column < width; column++, target += step)
            {
                var luma = y[lumaRow + column];
                var chroma = chromaRow + (column >> 1) * chromaPixelStride;
                var cb = u[chroma] - 128;
                var cr = v[chroma] - 128;

                // BT.601 full range, in 16.16 fixed point: 1.402, 0.344136, 0.714136, 1.772.
                var pixel = target * 4;
                rgba[pixel] = Clamp(luma + ((91881 * cr) >> 16));
                rgba[pixel + 1] = Clamp(luma - ((22554 * cb + 46802 * cr) >> 16));
                rgba[pixel + 2] = Clamp(luma + ((116130 * cb) >> 16));
                rgba[pixel + 3] = 255;
            }
        }
    }

    private static byte Clamp(int value) => (byte)(value < 0 ? 0 : value > 255 ? 255 : value);
}
