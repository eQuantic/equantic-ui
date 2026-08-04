using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Where a departing layer is drawn while it fades.
/// <para>
/// Every test in this file renders at RenderScale 2, and that is the point rather than detail. A
/// baked command already carries the root scale; replaying it under the current transform applied
/// that scale again, which put the closing dialog at twice its own coordinates — down and to the
/// right — for the single frame before it disappeared. At scale 1 the bug is arithmetically
/// invisible, so every headless test in the suite agreed the exit was perfect.
/// </para>
/// </summary>
public class ExitReplayScaleTests
{
    private const float Scale = 2f;

    private sealed class Screen : Primitives.StatefulComponent
    {
        public bool Showing = true;

        public override VisualNode Build(ComponentContext context)
        {
            var stack = new Stack { Width = SizeValue.Fill, Height = SizeValue.Fill };
            stack.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill }));
            if (Showing)
            {
                var card = new Box(new BoxStyle
                {
                    Width = 100,
                    Height = 60,
                    Background = PhotonTheme.Instance.Colors(Variant.Primary).Base,
                }, new Text("Bye", TypeRole.BodyM));
                stack.Add(new Overlay(new Presence(card)));
            }
            return stack;
        }
    }

    private static DrawCommand[] Paint(PhotonHost host, float timeMs)
    {
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, timeMs);
        return builder.Build().Commands.ToArray();
    }

    /// <summary>The card's own fill, wherever it ended up.</summary>
    private static DrawCommand? Card(DrawCommand[] commands)
    {
        var primary = PhotonTheme.Instance.Colors(Variant.Primary).Base.Resolve(ThemeMode.Light);
        foreach (var command in commands)
            if (command.Paint.Color == primary) return command;
        return null;
    }

    /// <summary>Everything the layer was made of leaves with it — glyph textures included.</summary>
    [Fact]
    public void ADepartingLayerKeepsItsText()
    {
        var screen = new Screen();
        var host = new PhotonHost(screen, PhotonTheme.Instance, ThemeMode.Light, 400, 300)
        {
            RenderScale = Scale,
            TextRasterizer = new StubRasterizer(),
        };

        var present = Paint(host, 0);
        present.Count(c => c.Kind == DrawCommandKind.Texture).Should().BeGreaterThan(0,
            "the card carries a label");

        screen.Showing = false;
        var leaving = Paint(host, 16);

        leaving.Count(c => c.Kind == DrawCommandKind.Texture).Should().BeGreaterThan(0,
            "a layer that loses its words on the way out reads as two different things closing");
    }

    /// <summary>Enough of a text service to make the realizer emit real texture commands.</summary>
    private sealed class StubRasterizer : Framework.ITextRasterizer
    {
        public Framework.TextRaster? Rasterize(string content, TypeStyle style, float typeScale,
            float maxWidth, int maxLines, float scale)
        {
            if (string.IsNullOrEmpty(content)) return null;
            var width = Math.Max(1, (int)(content.Length * 8 * scale));
            var height = Math.Max(1, (int)(style.LineHeight * scale));
            return new Framework.TextRaster(width, height, new byte[width * height]);
        }
    }

    [Fact]
    public void ADepartingLayerStaysWhereItWas()
    {
        var screen = new Screen();
        var host = new PhotonHost(screen, PhotonTheme.Instance, ThemeMode.Light, 400, 300)
        {
            RenderScale = Scale,
        };

        var present = Card(Paint(host, 0));
        present.Should().NotBeNull("the card is on screen to begin with");

        screen.Showing = false;
        var leaving = Card(Paint(host, 16));
        leaving.Should().NotBeNull("a departed layer replays while it fades");

        // Same place, give or take the few dp of the exit's own drop. Doubling would land it at
        // twice the coordinate — hundreds of dp away, off the bottom-right of a 400×300 window.
        var before = present!.Value.Transform;
        var after = leaving!.Value.Transform;
        after.M11.Should().Be(before.M11, "the scale must not be applied twice");
        after.M22.Should().Be(before.M22);
        after.M31.Should().Be(before.M31, "an exit never moves sideways");
        (after.M32 - before.M32).Should().BeInRange(0, 40 * Scale,
            "the only vertical movement is the exit's own drop");
    }
}
