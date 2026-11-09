using System.Buffers.Binary;
using eQuantic.UI.Build;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The tab's icon (#1240): <c>favicon.ico</c> in the web set, holding the three sizes a browser picks
/// from, written from the icon's small cut with its transparency kept, or from the icon when there is
/// no cut. And the downscale every size goes through, which leaves a transparent edge clean.
/// </summary>
public class FaviconTests
{
    private static readonly byte[] Blue = [30, 80, 220, 255];
    private static readonly byte[] Red = [220, 40, 30, 255];

    private static byte[] Field(int size, Func<int, int, byte[]> pixel)
    {
        var rgba = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                pixel(x, y).CopyTo(rgba, (y * size + x) * 4);
        return rgba;
    }

    private static IReadOnlyList<(int Size, byte[] Rgba)> WrittenFavicon(string dir, (int, byte[])? small)
    {
        WebIcons.Write(dir, "App", 512, Field(512, (_, _) => Red), small);
        return Read(File.ReadAllBytes(Path.Combine(dir, "favicon.ico")));
    }

    [Fact]
    public void TheFaviconHoldsTheTabsThreeSizes_FromTheSmallCut_ItsTransparencyKept()
    {
        var dir = Path.Combine(Path.GetTempPath(), "eq-favicon-" + Guid.NewGuid().ToString("N"));
        try
        {
            // The small cut: its left half transparent, its right half the mark.
            var images = WrittenFavicon(dir, (256, Field(256, (x, _) => x < 128 ? [0, 0, 0, 0] : Blue)));

            images.Select(image => image.Size).Should().Equal(16, 32, 48);
            foreach (var (size, rgba) in images)
            {
                Pixel(rgba, size, 0, size / 2).Should().Equal([0, 0, 0, 0], $"the cut's transparency is kept at {size}");
                Pixel(rgba, size, size - 1, size / 2).Should().Equal(Blue, $"the mark is the small cut's at {size}");
            }

            // The launcher's sizes are the icon's, whatever the tab draws.
            var touch = eQuantic.UI.Native.Engine.PngCodec.Decode(File.ReadAllBytes(Path.Combine(dir, "apple-touch-icon.png")));
            touch.Rgba.Take(4).Should().Equal(Red);
            File.Exists(Path.Combine(dir, "favicon-32.png")).Should().BeFalse("the ICO carries the tab's 32");

            // A folder written by an older build loses its PNG favicon on the next.
            File.WriteAllBytes(Path.Combine(dir, "favicon-32.png"), [1]);
            WrittenFavicon(dir, null);
            File.Exists(Path.Combine(dir, "favicon-32.png")).Should().BeFalse("nothing links it any more");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void WithNoSmallCut_TheFaviconIsTheIcon()
    {
        var dir = Path.Combine(Path.GetTempPath(), "eq-favicon-" + Guid.NewGuid().ToString("N"));
        try
        {
            var images = WrittenFavicon(dir, null);

            images.Select(image => image.Size).Should().Equal(16, 32, 48);
            images.Should().OnlyContain(image => Pixel(image.Rgba, image.Size, image.Size / 2, image.Size / 2).SequenceEqual(Red));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ADownscale_WeighsColourByAlpha_SoATransparentEdgeKeepsNoDarkRim()
    {
        // Half transparent black, half opaque white: the reduction is white at half coverage. Averaged
        // unweighted it was mid grey, the dark rim around every transparent mark.
        var source = Field(2, (x, _) => x == 0 ? [0, 0, 0, 0] : [255, 255, 255, 255]);

        Downscale.Box(source, 2, 1).Should().Equal([255, 255, 255, 127]);
    }

    [Fact]
    public void AnOpaqueDownscale_IsThePlainAverage()
    {
        var source = Field(2, (x, y) => [(byte)(x * 100), (byte)(y * 50), 7, 255]);

        Downscale.Box(source, 2, 1).Should().Equal([50, 25, 7, 255]);
    }

    private static byte[] Pixel(byte[] rgba, int size, int x, int y) => rgba.AsSpan((y * size + x) * 4, 4).ToArray();

    /// <summary>The images of an ICO whose entries are 32-bit DIBs, as straight RGBA, top-down.</summary>
    private static IReadOnlyList<(int Size, byte[] Rgba)> Read(byte[] file)
    {
        BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(2)).Should().Be(1, "an icon, not a cursor");
        var count = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(4));
        var images = new List<(int, byte[])>();
        for (var i = 0; i < count; i++)
        {
            var entry = file.AsSpan(6 + 16 * i);
            var size = entry[0] == 0 ? 256 : entry[0];
            var offset = BinaryPrimitives.ReadInt32LittleEndian(entry[12..]);
            var rgba = new byte[size * size * 4];
            for (var y = 0; y < size; y++)
            {
                var row = offset + 40 + (size - 1 - y) * size * 4;
                for (var x = 0; x < size; x++)
                {
                    var s = row + x * 4;
                    var d = (y * size + x) * 4;
                    (rgba[d], rgba[d + 1], rgba[d + 2], rgba[d + 3]) = (file[s + 2], file[s + 1], file[s], file[s + 3]);
                }
            }
            images.Add((size, rgba));
        }
        return images;
    }
}
