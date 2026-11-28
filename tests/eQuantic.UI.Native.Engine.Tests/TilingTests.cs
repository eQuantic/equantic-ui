using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// <see cref="Tiling"/>, the normative math of a tile layer (#1937): where a point falls in the one tile
/// the texture holds, on every side of the origin, and the exact geometry design.md decision 11 asks for,
/// a period and a phase never moved to a device pixel.
/// </summary>
public class TilingTests
{
    /// <summary>A 10 × 10 tile repeating from (100, 50) on both axes, in a 20 × 20 texture.</summary>
    private static readonly Tiling Repeating = new(new Point(100, 50), new Size(10, 10), new Size(10, 10), true, true);

    [Fact]
    public void LeftOfAndAboveTheOrigin_TheCellsAreNegative_AndTheTilesContinue()
    {
        // (97.5, 46) is cell (−1, −1), 7.5 and 6 into it.
        Repeating.TryTexel(new Point(97.5f, 46), 20, 20, out var x, out var y).Should().BeTrue();
        x.Should().BeApproximately(15, 1e-3f, "7.5 of a 10-wide tile is three quarters of 20 texels");
        y.Should().BeApproximately(12, 1e-3f);

        // Thirty tiles to the left and up, still a tile.
        Repeating.TryTexel(new Point(100 - 300 + 2.5f, 50 - 300 + 5), 20, 20, out x, out y).Should().BeTrue();
        x.Should().BeApproximately(5, 1e-3f);
        y.Should().BeApproximately(10, 1e-3f);
    }

    [Fact]
    public void OnceOnOneAxis_KeepsCellZeroAloneThere_AndRepeatsOnTheOther()
    {
        var row = Repeating with { RepeatY = false };

        row.TryTexel(new Point(1000.5f, 55), 20, 20, out _, out _).Should().BeTrue("across, it still repeats");
        row.TryTexel(new Point(105, 65), 20, 20, out _, out _).Should().BeFalse("cell 1 down is left out");
        row.TryTexel(new Point(105, 45), 20, 20, out _, out _).Should().BeFalse("and so is cell −1");
        row.TryTexel(new Point(105, 59.9f), 20, 20, out _, out var y).Should().BeTrue("cell 0 is the tile");
        y.Should().BeApproximately(19.8f, 1e-3f);
    }

    [Fact]
    public void ASpacedTile_LeavesItsGap_EmptyInEveryCell()
    {
        // A 300 dp tile spaced over 1000 dp: three tiles at 0, 350 and 700, a 50 dp gap after each.
        var spaced = new Tiling(new Point(0, 0), new Size(350, 100), new Size(300, 100), true, false);

        spaced.TryTexel(new Point(299.5f, 10), 300, 100, out var x, out _).Should().BeTrue();
        x.Should().BeApproximately(299.5f, 1e-2f);
        spaced.TryTexel(new Point(300.5f, 10), 300, 100, out _, out _).Should().BeFalse("the gap after the first tile");
        spaced.TryTexel(new Point(349.5f, 10), 300, 100, out _, out _).Should().BeFalse();
        spaced.TryTexel(new Point(350.5f, 10), 300, 100, out x, out _).Should().BeTrue("the second tile starts at 350");
        x.Should().BeApproximately(0.5f, 1e-2f);
        spaced.TryTexel(new Point(700.5f, 10), 300, 100, out _, out _).Should().BeTrue("the third at 700");
        spaced.WrapsX.Should().BeFalse("tiles a gap apart never read across a seam");
    }

    /// <summary>A point exactly on a tile's start is in that tile, never in the gap of the cell before:
    /// found on Metal, whose division by the period landed a hair under the whole cell.</summary>
    [Fact]
    public void APointOnATilesStart_IsInThatTile()
    {
        var spaced = new Tiling(new Point(8, 99), new Size(23.5f, 12), new Size(15, 12), true, false);
        foreach (var start in new[] { 31.5f, 78.5f, 125.5f })
        {
            spaced.TryTexel(new Point(start, 104.5f), 15, 12, out var x, out _).Should().BeTrue($"{start} starts a tile");
            x.Should().BeApproximately(0, 1e-3f);
        }
    }

    [Fact]
    public void ATexelAtEachEdge_IsTheFirstOrTheLastOfTheTexture()
    {
        Repeating.TryTexel(new Point(100.25f, 50.25f), 20, 20, out var x, out var y).Should().BeTrue();
        x.Should().BeApproximately(0.5f, 1e-3f, "the first texel's centre, a quarter dp into a 10 dp tile of 20 texels");
        y.Should().BeApproximately(0.5f, 1e-3f);

        Repeating.TryTexel(new Point(109.75f, 59.75f), 20, 20, out x, out y).Should().BeTrue();
        x.Should().BeApproximately(19.5f, 1e-3f, "the last texel's centre");
        y.Should().BeApproximately(19.5f, 1e-3f);
    }

    [Fact]
    public void TheSample_AtATexelCentre_IsThatTexel_AndAcrossTheSeam_TheNextTilesFirst()
    {
        // A 2 × 1 texture, red then blue.
        var texture = TextureData.Rgba(2, 1, [255, 0, 0, 255, 0, 0, 255, 255]);
        var tiling = new Tiling(new Point(0, 0), new Size(2, 1), new Size(2, 1), true, true);

        var red = tiling.Sample(texture, 0.5f, 0.5f);
        red.Should().Be(ColorSpace.ToPremultipliedLinear(new Color(255, 0, 0, 255)));

        // At the very end of the tile, half of its last texel (blue) and half of the next tile's first (red).
        var seam = tiling.Sample(texture, 2f, 0.5f);
        seam.R.Should().BeApproximately(0.5f, 1e-4f);
        seam.B.Should().BeApproximately(0.5f, 1e-4f);

        // Held, not wrapped, where the tiles do not touch.
        var held = (tiling with { RepeatX = false }).Sample(texture, 2f, 0.5f);
        held.B.Should().BeApproximately(1f, 1e-4f);
        held.R.Should().Be(0);
    }

    /// <summary>
    /// The spec's "No drift from the exact geometry": a 330 dp tile from the box's origin at 2.625 device
    /// pixels a dp repeats every 866.25 device pixels, so the tenth tile starts at 7796.25. A period rounded
    /// to 866 would start it at 7794, two and a quarter pixels from where Chromium and Gecko draw it.
    /// </summary>
    [Fact]
    public void TheTenthTileOf330dpAt2Point625_StartsAt7796Point25DevicePixels()
    {
        const float scale = 2.625f;
        var tiling = new Tiling(new Point(0, 0), new Size(330, 330), new Size(330, 330), true, true);
        // The texture holds the tile at its device size rounded up: 866.25 → 867 texels.
        const int texels = 867;

        // Device pixel 7796, centre 7796.5: a quarter of a device pixel into the tenth tile (cell 9).
        tiling.TryTexel(new Point(7796.5f / scale, 0.5f / scale), texels, texels, out var first, out _).Should().BeTrue();
        first.Should().BeApproximately(0.25f / 866.25f * texels, 1e-2f, "cell 9 starts at 7796.25, not 7794");

        // Device pixel 7795, centre 7795.5: still the ninth tile, three quarters of a pixel from its end.
        tiling.TryTexel(new Point(7795.5f / scale, 0.5f / scale), texels, texels, out var last, out _).Should().BeTrue();
        last.Should().BeApproximately(865.5f / 866.25f * texels, 1e-2f);

        // And where the cells start, in device pixels: exactly 9 × 330 × 2.625.
        (9 * tiling.Period.Width * scale).Should().Be(7796.25f);
    }
}
