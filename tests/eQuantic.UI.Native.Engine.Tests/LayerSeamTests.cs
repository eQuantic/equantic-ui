using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Two layers that share an edge on a fractional device pixel (#1344). A swipeable row keeps its action behind its
/// surface, the same height as the row; on an Android emulator at 2.625 device pixels a dp, a 78dp row ends at
/// 204.75px. Each layer covered three quarters of that last pixel row, so the action's red blended in first and the
/// surface's coverage blended over it, and about a fifth of the red survived: a red hairline under a closed row. A
/// browser snaps a box's background to device pixels and draws no line for the same tree.
/// </summary>
public class LayerSeamTests
{
    private const float Scale = 2.625f;
    private const int WidthDp = 120;
    private const int HeightDp = 100;

    private static readonly Color Page = Color.FromRgb(0xF0, 0xF0, 0xF0);
    private static readonly Color Action = Color.FromRgb(0xE0, 0x20, 0x20);
    private static readonly Color Surface = Color.FromRgb(0xFF, 0xFF, 0xFF);

    /// <summary>The page, the red action behind, and the white surface over it, both 78dp tall from the top.</summary>
    private static VisualNode Row()
    {
        var stack = new Stack();
        stack.Add(new Box(new BoxStyle { Width = WidthDp, Height = HeightDp, Background = new ColorToken(Page) }));
        stack.Add(new Positioned(new Box(new BoxStyle { Width = WidthDp, Height = 78, Background = new ColorToken(Action) }), top: 0, start: 0));
        stack.Add(new Positioned(new Box(new BoxStyle { Width = WidthDp, Height = 78, Background = new ColorToken(Surface) }), top: 0, start: 0));
        return stack;
    }

    private static (byte[] Pixels, int Width) Render()
    {
        var width = (int)MathF.Ceiling(WidthDp * Scale);
        var height = (int)MathF.Ceiling(HeightDp * Scale);
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(width, height);
        var host = new PhotonHost(Row(), PhotonTheme.Instance, ThemeMode.Light, WidthDp, HeightDp) { RenderScale = Scale };
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        backend.Render(builder.Build(), surface);
        var pixels = new byte[width * height * 4];
        surface.ReadPixelsSrgb(pixels);
        return (pixels, width);
    }

    [Fact]
    public void TheLayerBehind_DoesNotShowThroughTheEdgeTheyShare_OnAFractionalPixel()
    {
        var (pixels, width) = Render();

        // 78dp × 2.625 = 204.75px: row 204 is the one both edges cut.
        foreach (var x in new[] { 10, width / 2, width - 10 })
        {
            var at = (204 * width + x) * 4;
            var (r, g, b) = (pixels[at], pixels[at + 1], pixels[at + 2]);
            (r - Math.Min(g, b)).Should().BeLessThanOrEqualTo(4,
                $"row 204 at x {x} holds none of the action's red, where the surface covers it: read ({r}, {g}, {b})");
        }
    }
}
