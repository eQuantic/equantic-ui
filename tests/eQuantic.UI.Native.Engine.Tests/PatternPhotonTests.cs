using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A box's patterns on Photon (#1937, #1939): every kind placed in one of the box's areas and clipped to
/// another with its rounded corners, a tile composited once at its device size and drawn as ONE
/// <see cref="DrawCommandKind.TiledTexture"/>, and the pixels the Reference backend draws from it.
/// </summary>
public class PatternPhotonTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static Color Page => Theme.Background.Resolve(ThemeMode.Light, ContrastLevel.Standard);

    private static readonly Color Brick = Color.FromRgb(200, 40, 40);

    /// <summary>The desk's shape: a 675 × 518 artwork, one opaque rectangle over the whole viewBox.</summary>
    private static readonly VectorDrawing Desk = new(0, 0, 675, 518,
        [new VectorShape("M0 0 H675 V518 H0 Z", VectorPaint.Solid(Brick))]);

    /// <summary>A 40 × 40 artwork with one 20 × 20 square in its top-left corner, so where a tile stands shows.</summary>
    private static readonly VectorDrawing Corner = new(0, 0, 40, 40,
        [new VectorShape("M0 0 H20 V20 H0 Z", VectorPaint.Solid(Brick))]);

    /// <summary>Two overlapping translucent shapes, one in a run and one inherited, for the comparison with a Drawing.</summary>
    private static readonly VectorDrawing Mark = new(0, 0, 100, 80,
    [
        new VectorShape("M0 0 H70 V60 H0 Z", VectorPaint.Gradients(VectorPaintKind.LinearGradient,
            new VectorGradient(0, 0, 1, 1, 0, false,
                [new VectorStop(0, Color.FromRgb(30, 27, 75)), new VectorStop(1, Color.FromRgb(67, 56, 202))]))),
        new VectorShape("M30 20 H100 V80 H30 Z", new VectorPaint(VectorPaintKind.Inherit, Color.Black.WithOpacity(0.55f))),
    ]);

    private sealed class Images : IImageLoader
    {
        /// <summary>60 × 40 pixels, opaque, a colour per quadrant.</summary>
        public RgbaImage? Load(string source)
        {
            if (source != "img/linen.png" && source != "img/linen-dark.png") return null;
            var pixels = new byte[60 * 40 * 4];
            for (var y = 0; y < 40; y++)
                for (var x = 0; x < 60; x++)
                {
                    var i = (y * 60 + x) * 4;
                    pixels[i] = (byte)(x < 30 ? 255 : 0);
                    pixels[i + 1] = (byte)(y < 20 ? 255 : 0);
                    pixels[i + 2] = (byte)(source.Contains("dark") ? 255 : 0);
                    pixels[i + 3] = 255;
                }
            return new RgbaImage(60, 40, pixels);
        }
    }

    private static PhotonHost Host(VisualNode root, int width, int height, float scale = 1, bool images = false) =>
        new(root, Theme, ThemeMode.Light, width, height)
        {
            IconRasterizer = new PortableIconRasterizer(),
            ImageLoader = images ? new Images() : null,
            RenderScale = scale,
            SmoothScroll = false,
        };

    private static DisplayList Frame(PhotonHost host)
    {
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        return builder.Build();
    }

    private static DrawCommand[] Tiled(DisplayList list) =>
        list.Commands.ToArray().Where(c => c.Kind == DrawCommandKind.TiledTexture).ToArray();

    private static byte[] Pixels(DisplayList list, int width, int height)
    {
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(width, height);
        backend.Render(list, surface);
        var pixels = new byte[width * height * 4];
        surface.ReadPixelsSrgb(pixels);
        return pixels;
    }

    private static Color PixelAt(byte[] pixels, int width, int x, int y)
    {
        var i = (y * width + x) * 4;
        return new Color(pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]);
    }

    // ---- emission ------------------------------------------------------------------------------

    [Fact]
    public void AFullWindowDesk_IsOneTiledTextureCommand_InsideTheBox()
    {
        var host = Host(new Box(new BoxStyle
        {
            Width = 2560,
            Height = 1440,
            Patterns = [new DrawingPattern(Desk, 330)],
        }), 2560, 1440);

        var list = Frame(host);
        var tiles = Tiled(list);

        tiles.Should().ContainSingle("one draw covers the 48 to 63 tiles a full-window desk shows");
        var command = tiles[0];
        command.Shape.Rect.Should().Be(new Rect(0, 0, 2560, 1440), "the tiles cover the clip area, the whole box");
        command.Clip.Should().BeNull("a square box needs no clip: the area's rectangle bounds the tiles");
        command.Tiling.Origin.Should().Be(new Point(0, 0));
        command.Tiling.Period.Width.Should().Be(330);
        command.Tiling.Period.Height.Should().BeApproximately(253.2444f, 1e-3f, "the height from the artwork's aspect");
        command.Tiling.RepeatX.Should().BeTrue();
        command.Tiling.RepeatY.Should().BeTrue();
        command.StrokeWidth.Should().Be(1, "fully opaque by default");
        var texture = list.Textures[command.TextureId];
        texture.Format.Should().Be(TextureFormat.Rgba8);
        texture.Width.Should().Be(330);
        texture.Height.Should().Be(254, "253.24 device pixels, rounded up");
    }

    [Fact]
    public void ThePatterns_PaintInListOrder_AboveTheBackground_AndBelowTheGlowAndTheGradient()
    {
        var host = Host(new Box(new BoxStyle
        {
            Width = 120,
            Height = 100,
            Background = Theme.Surface,
            Patterns = [new GridPattern(50, Theme.Border), new DrawingPattern(Desk, 30)],
            Glow = new RadialGradient(Theme.SurfaceHighlight, new ColorToken(Color.Transparent), 0.5f, 0.5f, 60, 50),
            Gradient = new LinearGradient(new ColorToken(Color.Transparent), Theme.SurfaceHighlight),
        }), 120, 100);

        var commands = Frame(host).Commands.ToArray()
            .Where(c => c.Kind is DrawCommandKind.FillRRect or DrawCommandKind.TiledTexture).ToList();

        commands[0].Paint.Color.Should().Be(Theme.Surface.Resolve(ThemeMode.Light, ContrastLevel.Standard), "the background first");
        var tile = commands.FindIndex(c => c.Kind == DrawCommandKind.TiledTexture);
        var glow = commands.FindIndex(c => c.Paint.Kind == PaintKind.RadialGradient);
        var gradient = commands.FindIndex(c => c.Paint.Kind == PaintKind.LinearGradient);
        tile.Should().Be(1 + 3 + 2, "after the background and the grid's three vertical and two horizontal rules");
        glow.Should().Be(tile + 1, "the glow over the last pattern");
        gradient.Should().Be(glow + 1, "the gradient on top");
    }

    [Fact]
    public void ASecondFrame_DrawsTheSameTile_AndANewRenderScale_CompositesAgain()
    {
        var host = Host(new Box(new BoxStyle
        {
            Width = 400,
            Height = 300,
            Patterns = [new DrawingPattern(Desk, 120)],
        }), 400, 300, scale: 2);

        var first = Frame(host);
        var firstTile = first.Textures[Tiled(first)[0].TextureId];
        var second = Frame(host);
        var secondTile = second.Textures[Tiled(second)[0].TextureId];
        secondTile.Should().BeSameAs(firstTile, "the second frame hits the host's cache: composited once");
        firstTile.Width.Should().Be(240);

        host.RenderScale = 3;
        var third = Frame(host);
        var thirdTile = third.Textures[Tiled(third)[0].TextureId];
        thirdTile.Should().NotBeSameAs(firstTile, "a new scale composites the tile again, crisp at it");
        thirdTile.Width.Should().Be(360);
    }

    /// <summary>The spec's "A tile placed in the content area and clipped to the padding area".</summary>
    [Fact]
    public void ATileInTheContentArea_ClippedToThePaddingArea_StartsInsideTheBorderAndThePadding()
    {
        var host = Host(new Box(new BoxStyle
        {
            Width = 200,
            Height = 150,
            BorderWidth = 2,
            BorderColor = Theme.Border,
            Padding = new EdgeInsets(10, 10, 10, 10),
            Patterns = [new DrawingPattern(Desk, 50) { Area = BoxArea.Content, ClipTo = BoxArea.Padding }],
        }), 200, 150);

        var command = Tiled(Frame(host)).Single();
        command.Tiling.Origin.Should().Be(new Point(12, 12), "the first tile starts at the content area");
        command.Shape.Rect.Should().Be(new Rect(2, 2, 196, 146), "no tile pixel lands on the border");
    }

    /// <summary>The spec's "A grid in a bordered, rounded box (#1939)": the rules start in the padding area
    /// and run across the whole box, inside its rounded corners.</summary>
    [Fact]
    public void AGridInABorderedRoundedBox_StartsInThePaddingArea_InsideTheRoundedClip()
    {
        var host = Host(new Box(new BoxStyle
        {
            Width = 200,
            Height = 120,
            BorderWidth = 4,
            BorderColor = new ColorToken(Color.Transparent),
            CornerRadius = new CornerRadii(16),
            Patterns = [new GridPattern(50, Theme.Border)],
        }), 200, 120);

        var rules = Frame(host).Commands.ToArray().Where(c => c.Kind == DrawCommandKind.FillRRect).ToList();
        var vertical = rules.Where(r => r.Shape.Rect.Width == 1).Select(r => r.Shape.Rect.X).ToList();
        var horizontal = rules.Where(r => r.Shape.Rect.Height == 1).Select(r => r.Shape.Rect.Y).ToList();
        vertical.Should().Equal(4f, 54f, 104f, 154f);
        horizontal.Should().Equal(4f, 54f, 104f);
        rules.Should().OnlyContain(r => r.Clip != null && r.Clip.Value.Radii == new CornerRadii(16),
            "every rule is drawn inside the box's rounded corners");
    }

    private static VisualNode Scroller(BoxStyle desk, Transform2D? around = null)
    {
        var column = new Column(gap: 0) { Width = SizeValue.Fill };
        column.Add(new Box(desk));
        var scroller = new ScrollView(column) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        return around is { } transform
            ? new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill, Transform = transform }, scroller)
            : scroller;
    }

    private static BoxStyle WindowDesk(TileAttachment attachment) => new()
    {
        Width = SizeValue.Fill,
        Height = 1200,
        Background = new ColorToken(Color.FromRgb(240, 236, 220)),
        Patterns = [new DrawingPattern(Corner, 40) { Attachment = attachment }],
    };

    [Fact]
    public void AWindowAttachedTile_StaysAtTheWindowsOrigin_WhileAScrollMovesTheBox()
    {
        var host = Host(Scroller(WindowDesk(TileAttachment.Window)), 300, 200);
        var before = Tiled(Frame(host)).Single();
        before.Tiling.Origin.Should().Be(new Point(0, 0));

        host.ScrollBy(150, 100, 0, 300);
        var after = Tiled(Frame(host)).Single();
        after.Tiling.Origin.Should().Be(new Point(0, 0), "the window did not move");
        after.Shape.Rect.Y.Should().Be(-300, "the box did");

        // A box attachment, against which the rule shows: its tiles move with the box.
        var boxed = Host(Scroller(WindowDesk(TileAttachment.Box)), 300, 200);
        Frame(boxed);
        boxed.ScrollBy(150, 100, 0, 300);
        Tiled(Frame(boxed)).Single().Tiling.Origin.Should().Be(new Point(0, -300));
    }

    /// <summary>The spec's "Under a transform": a window attachment under a rotated box paints as a box one.</summary>
    [Fact]
    public void UnderATransform_AWindowAttachedTile_MovesWithTheBox()
    {
        var host = Host(Scroller(WindowDesk(TileAttachment.Window), Transform2D.Rotate(2)), 300, 200);
        Frame(host);
        host.ScrollBy(150, 100, 0, 300);
        var command = Tiled(Frame(host)).Single();
        command.Tiling.Origin.Should().Be(new Point(0, -300), "placed in its own box, as CSS Transforms 1 §6 paints it");
    }

    /// <summary>The spec's "A raster tile on both targets": 120 dp over a 60 × 40 image at 2 device pixels a dp.</summary>
    [Fact]
    public void AnImageTile_IsOneTiledTexture_ResampledToItsDeviceSize()
    {
        var host = Host(new Box(new BoxStyle
        {
            Width = 400,
            Height = 300,
            Patterns = [new ImagePattern("img/linen.png", 120)],
        }), 400, 300, scale: 2, images: true);

        var list = Frame(host);
        var command = Tiled(list).Single();
        var texture = list.Textures[command.TextureId];
        texture.Width.Should().Be(240);
        texture.Height.Should().Be(160);
        command.Tiling.Period.Should().Be(new Size(120, 80), "the height from the image's own aspect");
        // The image's top-left quadrant is yellow, magnified bilinearly: well inside it, still yellow.
        PixelAt(texture.Alpha, 240, 20, 20).Should().Be(new Color(255, 255, 0, 255));
    }

    [Fact]
    public void ADarkSource_IsTheImageTiledInDarkMode()
    {
        var root = new Box(new BoxStyle
        {
            Width = 400,
            Height = 300,
            Patterns = [new ImagePattern("img/linen.png", 120) { DarkSource = "img/linen-dark.png" }],
        });
        var host = new PhotonHost(root, Theme, ThemeMode.Dark, 400, 300) { ImageLoader = new Images() };
        var list = Frame(host);
        var texture = list.Textures[Tiled(list).Single().TextureId];
        texture.Alpha[2].Should().Be(255, "the dark image's blue channel");
    }

    [Fact]
    public void WithNoRasterizer_NoVectorTile_AndWithNoLoader_NoImageTile()
    {
        var host = new PhotonHost(new Box(new BoxStyle
        {
            Width = 200,
            Height = 100,
            Patterns = [new DrawingPattern(Desk, 50), new ImagePattern("img/linen.png", 50)],
        }), Theme, ThemeMode.Light, 200, 100);

        Tiled(Frame(host)).Should().BeEmpty();
    }

    // ---- pixels on the Reference backend -------------------------------------------------------

    /// <summary>The spec's "No seam at a fractional scale": a 33 dp tile is 86.625 device pixels at 2.625.</summary>
    [Fact]
    public void AnOpaqueTile_AtAFractionalScale_LeavesNoSeam()
    {
        var host = Host(new Box(new BoxStyle
        {
            Width = 100,
            Height = 60,
            Patterns = [new DrawingPattern(new VectorDrawing(0, 0, 10, 10,
                [new VectorShape("M0 0 H10 V10 H0 Z", VectorPaint.Solid(Brick))]), 33)],
        }), 120, 80, scale: 2.625f);

        var list = Frame(host);
        list.Textures[Tiled(list).Single().TextureId].Width.Should().Be(87, "86.625 rounded up");
        // 100 × 60 dp is 262.5 × 157.5 device pixels; every pixel whose centre is inside is the tile's.
        var pixels = Pixels(list, 315, 210);
        var off = new List<(int X, int Y, Color Pixel)>();
        for (var y = 0; y < 157; y++)
            for (var x = 0; x < 262; x++)
                if (PixelAt(pixels, 315, x, y) is var pixel && pixel != Brick)
                    off.Add((x, y, pixel));
        off.Should().BeEmpty("no pixel falls between two tiles");
    }

    /// <summary>The spec's "Half opacity": an opaque white tile at 0.5 over black is 0.5 in linear light.</summary>
    [Fact]
    public void AWhiteTileAtHalfOpacity_OverBlack_IsHalfInLinearLight()
    {
        var host = Host(new Box(new BoxStyle
        {
            Width = 100,
            Height = 60,
            Background = new ColorToken(Color.FromRgb(0, 0, 0)),
            Patterns = [new DrawingPattern(new VectorDrawing(0, 0, 10, 10,
                [new VectorShape("M0 0 H10 V10 H0 Z", VectorPaint.Solid(Color.White))]), 20) { Opacity = 0.5f }],
        }), 100, 60);

        var pixels = Pixels(Frame(host), 100, 60);
        foreach (var (x, y) in new[] { (5, 5), (19, 30), (20, 30), (50, 41) })
        {
            var pixel = PixelAt(pixels, 100, x, y);
            ColorSpace.SrgbToLinear(pixel.R).Should().BeApproximately(0.5f, 0.005f, $"at ({x}, {y})");
            pixel.R.Should().Be(pixel.G).And.Be(pixel.B);
        }
    }

    /// <summary>The spec's "the corner outside the radius is the page", for a tile and for a grid (#1939).</summary>
    [Fact]
    public void TheCornerOutsideTheRadius_IsThePage_ForATileAndForAGrid()
    {
        BoxStyle Rounded(Pattern pattern) => new()
        {
            Width = 200,
            Height = 120,
            BorderWidth = 4,
            BorderColor = new ColorToken(Color.Transparent),
            CornerRadius = new CornerRadii(16),
            Patterns = [pattern],
        };

        var tile = Pixels(Frame(Host(new Box(Rounded(new DrawingPattern(Desk, 50))), 200, 120)), 200, 120);
        PixelAt(tile, 200, 1, 1).Should().Be(Page, "outside the rounded corner the page shows");
        PixelAt(tile, 200, 100, 60).Should().Be(Brick, "inside, the tile");

        var grid = Pixels(Frame(Host(new Box(Rounded(new GridPattern(50, new ColorToken(Brick)))), 200, 120)), 200, 120);
        // The horizontal rule at y = 4 runs across the whole box, and stops at the corner's curve.
        PixelAt(grid, 200, 1, 4).Should().Be(Page, "the rule is clipped to the rounded corner");
        PixelAt(grid, 200, 100, 4).Should().Be(Brick, "and drawn where the box is");
    }

    /// <summary>A tile drawn once against the same artwork as a <see cref="Drawing"/>: the compositor
    /// paints as the node paints, within one level per channel (the tile's 8-bit storage).</summary>
    [Fact]
    public void OneTile_MatchesTheSameArtworkAsADrawing_WithinOneLevel()
    {
        var tile = Frame(Host(new Box(new BoxStyle
        {
            Width = 100,
            Height = 80,
            Patterns = [new DrawingPattern(Mark, 100, 80)
            {
                HorizontalRepeat = TileRepeat.Once,
                VerticalRepeat = TileRepeat.Once,
            }],
        }), 100, 80, scale: 2));
        var drawing = Frame(Host(new Drawing(Mark, 100, 80), 100, 80, scale: 2));

        var a = Pixels(tile, 200, 160);
        var b = Pixels(drawing, 200, 160);
        var worst = 0;
        for (var i = 0; i < a.Length; i++) worst = Math.Max(worst, Math.Abs(a[i] - b[i]));
        worst.Should().BeLessThanOrEqualTo(1);
    }

    /// <summary>The spec's "A desk inside a scroller": the tile's pixels at a window point do not move.</summary>
    [Fact]
    public void AWindowAttachedTile_KeepsItsPixelsAtAWindowPoint_ThroughAScroll()
    {
        byte[] Window(TileAttachment attachment, bool scrolled)
        {
            var host = Host(Scroller(WindowDesk(attachment)), 300, 200);
            Frame(host);
            if (scrolled) host.ScrollBy(150, 100, 0, 310);
            return Pixels(Frame(host), 300, 200);
        }

        Window(TileAttachment.Window, scrolled: true).Should().Equal(Window(TileAttachment.Window, scrolled: false),
            "the window's tiles stay still while the box scrolls under them");
        Window(TileAttachment.Box, scrolled: true).Should().NotEqual(Window(TileAttachment.Box, scrolled: false),
            "a box's tiles move with it, which is what makes the line above say something");
    }
}
