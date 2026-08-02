namespace eQuantic.UI.Build;

/// <summary>Making an icon smaller, once, for everyone who needs a smaller icon.</summary>
public static class Downscale
{
    /// <summary>
    /// A box filter: every destination pixel is the AVERAGE of the source pixels it covers. Point
    /// sampling would be one line shorter and would shred an icon's edges at 32dp, which is the one
    /// size a user sees most.
    /// </summary>
    public static byte[] Box(byte[] source, int sourceSize, int size)
    {
        var result = new byte[size * size * 4];
        var ratio = (double)sourceSize / size;

        for (var y = 0; y < size; y++)
        {
            var top = (int)(y * ratio);
            var bottom = Math.Max(top + 1, (int)((y + 1) * ratio));

            for (var x = 0; x < size; x++)
            {
                var left = (int)(x * ratio);
                var right = Math.Max(left + 1, (int)((x + 1) * ratio));

                long r = 0, g = 0, b = 0, a = 0;
                var count = 0;
                for (var sy = top; sy < bottom && sy < sourceSize; sy++)
                {
                    for (var sx = left; sx < right && sx < sourceSize; sx++)
                    {
                        var i = (sy * sourceSize + sx) * 4;
                        r += source[i];
                        g += source[i + 1];
                        b += source[i + 2];
                        a += source[i + 3];
                        count++;
                    }
                }

                var destination = (y * size + x) * 4;
                result[destination] = (byte)(r / count);
                result[destination + 1] = (byte)(g / count);
                result[destination + 2] = (byte)(b / count);
                result[destination + 3] = (byte)(a / count);
            }
        }

        return result;
    }
}
