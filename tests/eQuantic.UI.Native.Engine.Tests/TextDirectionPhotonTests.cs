using System.Globalization;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Photon reads the direction a component's text runs in as the web does (#1568): the nearest
/// <see cref="Directionality"/> above its position, or its UI culture's, and a component that read it
/// builds again in the frame after it changes. Photon still lays out left to right; this is what lets a
/// component that asks the question answer the same on both targets.
/// <para>
/// And it is the ONLY thing Photon does with it (#1569): a glyph that matches the direction, an arrow key and
/// a drag are bound physically, which is logical left to right, until Photon mirrors its layout (outside 1.0).
/// Each of them is measured here under the culture <c>ar</c>, under a <see cref="Directionality"/> that reads
/// right to left, and under both, and must be what it is under <c>en</c>.
/// </para>
/// </summary>
public class TextDirectionPhotonTests
{
    private sealed class Reader(List<string> log, string name) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            log.Add($"{name} {context.TextDirection}");
            return new Text(name, TypeRole.BodyM);
        }
    }

    private sealed class Holder(VisualNode child) : StatefulComponent
    {
        private TextDirection _direction = TextDirection.LeftToRight;

        public void Flip() => SetState(() => _direction = TextDirection.RightToLeft);

        public override VisualNode Build(ComponentContext context) => new Directionality(_direction, child);
    }

    private static void Frame(PhotonHost host) => host.RenderFrame(new DisplayListBuilder());

    [Fact]
    public void AComponent_ReadsTheNearestDirectionality_OrItsCulturesInAFrame()
    {
        var before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ar");
        try
        {
            var log = new List<string>();
            var holder = new Holder(new Reader(log, "inside"));
            var host = new PhotonHost(new Column { new Reader(log, "page"), holder }, PhotonTheme.Instance,
                ThemeMode.Light, 300, 200);
            Frame(host);
            log.Should().Equal("page RightToLeft", "inside LeftToRight");

            log.Clear();
            holder.Flip();
            Frame(host);
            // Photon builds every frame, so this proves the next frame reads the new direction. That only
            // what read it rebuilds is the web's to prove, and text-direction.spec.ts proves it.
            log.Should().Contain("inside RightToLeft", "the frame after the change reads the new direction")
                .And.NotContain("inside LeftToRight");
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    // ---- what only the SDK draws or hears does not follow it on Photon -------------------------------

    /// <summary>The ways a page can read right to left, and the one that does not: the culture, a
    /// <see cref="Directionality"/> over the content, both, and neither under <c>en</c>.</summary>
    public static IEnumerable<object?[]> Readings() =>
    [
        ["en", null],
        ["ar", null],
        ["en", TextDirection.RightToLeft],
        ["ar", TextDirection.RightToLeft],
    ];

    private static T UnderReading<T>(string culture, TextDirection? fence, Func<VisualNode, T> run, Func<VisualNode> content)
    {
        var before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            var node = content();
            return run(fence is { } direction ? new Directionality(direction, node) : node);
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    private static PhotonHost Open(VisualNode root, float width = 360, float height = 200)
    {
        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, width, height);
        host.RenderFrame(new DisplayListBuilder());
        return host;
    }

    private static void Press(PhotonHost host, string key)
    {
        host.KeyDown(key);
        host.RenderFrame(new DisplayListBuilder());
    }

    /// <summary>Asked for every glyph it is handed, which it records; answers a mask that covers only the
    /// left half of the box, so a mirrored raster would differ from an unmirrored one.</summary>
    private sealed class RecordingRasterizer : IIconRasterizer
    {
        public List<IconGlyph> Asked { get; } = [];

        public TextRaster? Rasterize(IconGlyph glyph, float widthDp, float heightDp, float scale)
        {
            Asked.Add(glyph);
            var width = Math.Max((int)MathF.Round(widthDp * scale), 1);
            var height = Math.Max((int)MathF.Round(heightDp * scale), 1);
            var coverage = new byte[width * height];
            for (var row = 0; row < height; row++)
                Array.Fill(coverage, (byte)255, row * width, width / 2);
            return new TextRaster(width, height, coverage);
        }
    }

    [Fact]
    public void AGlyphThatMatchesTheDirection_IsDrawnExactlyAsUnderEnglish_InEveryReading()
    {
        // A glyph of its own, which the recording rasterizer is asked for in every reading: the same glyph
        // value each time (a mirrored one would be another), and the commands that draw it are equal.
        var glyph = new IconGlyph("probe-" + Guid.NewGuid().ToString("N"), "M2 2h8v20H2z", MatchTextDirection: true);
        var rasterizer = new RecordingRasterizer();
        var drawn = new List<DrawCommand>();

        foreach (var reading in Readings())
        {
            var commands = UnderReading((string)reading[0]!, (TextDirection?)reading[1], root =>
            {
                var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, 120, 80) { IconRasterizer = rasterizer };
                var builder = new DisplayListBuilder();
                host.RenderFrame(builder);
                return builder.Build().Commands.ToArray().Where(command => command.Kind == DrawCommandKind.Texture).ToArray();
            }, () => new Icon(glyph, 24));
            commands.Should().ContainSingle($"{reading[0]} {reading[1]}: one mask, the glyph");
            drawn.Add(commands[0]);
        }

        rasterizer.Asked.Should().HaveCount(4).And.OnlyContain(asked => asked == glyph,
            "every reading hands the rasterizer the glyph as it is, MatchTextDirection set and nothing turned");
        drawn.Skip(1).Should().OnlyContain(command => command.Shape == drawn[0].Shape && command.Transform == drawn[0].Transform
            && command.Paint == drawn[0].Paint && command.TextureId == drawn[0].TextureId,
            "the same command at the same place, with the same tint");
    }

    [Theory]
    [MemberData(nameof(Readings))]
    public void ArrowRight_StillAdjustsByPlusOne_AndArrowLeftByMinusOne_InEveryReading(string culture, TextDirection? fence)
    {
        var log = new List<int>();
        var steps = UnderReading(culture, fence, root =>
        {
            var host = Open(root);
            host.FocusNext().Should().BeTrue("the adjustable is one Tab stop");
            Press(host, "ArrowRight");
            Press(host, "ArrowLeft");
            return log.ToArray();
        }, () => new Adjustable(new Box(new BoxStyle { Width = 120, Height = 24 }), log.Add)
        {
            Label = "Budget",
            Value = new RangeValue(0.4f, 0, 1),
        });

        steps.Should().Equal([1, -1], "Photon binds the keys physically, which is logical left to right");
    }

    [Theory]
    [MemberData(nameof(Readings))]
    public void ArrowRight_StillMovesToTheNextItem_AndArrowLeftToThePreviousOne_InEveryReading(string culture, TextDirection? fence)
    {
        var moves = new List<NavigableMove>();
        var walked = UnderReading(culture, fence, root =>
        {
            var host = Open(root, 360, 200);
            host.FocusNext().Should().BeTrue("the grid is one Tab stop");
            Press(host, "ArrowRight");
            Press(host, "ArrowLeft");
            Press(host, "ArrowDown");
            return moves.ToArray();
        }, () => new Navigable(moves.Add,
            [new Row { new Text("a", TypeRole.BodyM), new Text("b", TypeRole.BodyM) }, new Row { new Text("c", TypeRole.BodyM) }])
        {
            Label = "days",
        });

        walked.Should().Equal([NavigableMove.NextItem, NavigableMove.PreviousItem, NavigableMove.NextRow]);
    }

    [Theory]
    [MemberData(nameof(Readings))]
    public void AHorizontalDrag_ReportsTheSameOffset_InEveryReading(string culture, TextDirection? fence)
    {
        var released = float.NaN;
        var offset = UnderReading(culture, fence, root =>
        {
            var host = Open(root);
            var frame = host.RenderFrame(new DisplayListBuilder(), 1000);
            var centre = frame.DragRegions.Single().Bounds.Center;
            host.PressDown(centre.X, centre.Y);
            host.PointerMove(centre.X - 50, centre.Y);
            host.PressUp(centre.X - 50, centre.Y);
            return released;
        }, () => new Draggable(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 60, Background = PhotonTheme.Instance.Surface }),
            reported => released = reported)
        {
            Axis = DragAxis.Horizontal,
            Min = -96,
            Max = 96,
        });

        offset.Should().Be(-50f, "toward the left is negative on Photon, which lays out left to right for 1.0");
    }
}
