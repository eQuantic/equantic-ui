using eQuantic.UI.Components;
using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The gradient + shimmer web half (animation slice 2): BoxStyle.Gradient lowers to
/// <c>background-image: linear-gradient(…)</c> with light-dark() stops (mode-free DOM, same as solid
/// fills), and the Skeleton's decorative glint carries <c>eq-loop-rest-hidden</c> so
/// prefers-reduced-motion yields the spec's plain placeholder. Cross-pinned byte-for-byte with the
/// TypeScript lowering (gradient-shimmer.spec.ts).
/// </summary>
public class GradientShimmerRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    [Fact]
    public void GradientBox_LowersToBackgroundImage_AfterBackgroundColor()
    {
        var node = Render(new Box(new BoxStyle
        {
            Width = 120,
            Height = 40,
            Background = Theme.SurfaceSubtle,
            Gradient = new LinearGradient(new ColorToken(Color.Transparent), Theme.SurfaceHighlight),
        }));

        node.Attributes["style"].Should().Contain(
            $"background-color: {TokenCss.Value(Theme.SurfaceSubtle)}; " +
            $"background-image: linear-gradient(to right, #00000000, {TokenCss.Value(Theme.SurfaceHighlight)})",
            "the gradient composes OVER the solid, in the HtmlStyle property order");
    }

    [Fact]
    public void GradientDirection_ToBottom_EmitsTheKeyword()
    {
        var node = Render(new Box(new BoxStyle
        {
            Width = 120,
            Height = 40,
            Gradient = new LinearGradient(Theme.Scrim, new ColorToken(Color.Transparent), GradientDirection.ToBottom),
        }));

        node.Attributes["style"].Should().Contain("background-image: linear-gradient(to bottom, ");
    }

    /// <summary>
    /// The DIAGONAL axes (the "tinted tile" gradient every icon chip in the eQuantic site uses).
    /// Native runs the same <c>Paint.Linear</c> primitive with corner endpoints — the axis was
    /// always a pair of points, so the diagonals needed no engine or shader change.
    /// </summary>
    [Theory]
    [InlineData(GradientDirection.ToBottomRight, "to bottom right")]
    [InlineData(GradientDirection.ToBottomLeft, "to bottom left")]
    public void GradientDirection_Diagonals_EmitTheKeyword(GradientDirection direction, string keyword)
    {
        var node = Render(new Box(new BoxStyle
        {
            Width = 32,
            Height = 32,
            Gradient = new LinearGradient(Theme.Colors(Variant.Primary).Base, Theme.Colors(Variant.Tertiary).Base, direction),
        }));

        node.Attributes["style"].Should().Contain($"background-image: linear-gradient({keyword}, ");
    }

    /// <summary>
    /// GRADIENT TEXT: the background paints through the glyphs. `color` survives as the readable
    /// FALLBACK for browsers without background-clip:text — an invisible headline is a far worse
    /// failure than a solid one — and the prefixed twins ship for Safari. A text with no colour of
    /// its own writes none since #614, so the fallback is the colour it inherits: the foreground of
    /// the box above it, or the document's TextPrimary. One it names is written, and is the fallback.
    /// </summary>
    [Fact]
    public void GradientText_ClipsTheBackgroundToTheGlyphs_KeepingAColorFallback()
    {
        var gradient = new LinearGradient(Theme.TextPrimary, Theme.Colors(Variant.Primary).Base);
        var node = Render(new Text("Headline", TypeRole.Display) { Gradient = gradient });

        var style = node.Attributes["style"];
        style.Should().NotMatchRegex(@"(?<![-\w])color:",
            "the fallback is the colour it inherits, when background-clip is unsupported");
        Render(new Text("Headline", TypeRole.Display, Theme.TextSecondary) { Gradient = gradient })
            .Attributes["style"].Should().Contain($"color: {TokenCss.Value(Theme.TextSecondary)}",
                "a colour of its own is written, and is the fallback");
        style.Should().Contain($"background-image: {TokenCss.Gradient(gradient)}");
        style.Should().Contain("-webkit-background-clip: text; background-clip: text");
        style.Should().Contain("-webkit-text-fill-color: transparent");
    }

    /// <summary>
    /// The GRID pattern lowers to the two repeating hairline layers plus the matching
    /// <c>background-size</c> — the "graph paper" backdrop, realizable on both targets with
    /// primitives that already exist (native emits the same rules as ordinary fills).
    /// </summary>
    [Fact]
    public void GridPattern_EmitsBothHairlineLayers_AndTheCellSize()
    {
        var node = Render(new Box(new BoxStyle
        {
            Width = 200,
            Height = 200,
            Patterns = [new GridPattern(56, Theme.Border)],
        }));

        var line = TokenCss.Value(Theme.Border);
        node.Attributes["style"].Should().Contain(
            $"background-image: linear-gradient(to right, {line} 1px, transparent 1px), " +
            $"linear-gradient(to bottom, {line} 1px, transparent 1px)");
        node.Attributes["style"].Should().Contain("background-size: 56px 56px, 56px 56px");
    }

    /// <summary>
    /// The elliptical spotlight lowers to a CSS <c>radial-gradient()</c> with an explicit ellipse
    /// extent and a PERCENTAGE center — the exact shape the vocabulary models, so it survives a
    /// resize without recomputation.
    /// </summary>
    [Fact]
    public void Glow_LowersToAnEllipticalRadialGradient_WithAPercentageCenter()
    {
        var glow = new RadialGradient(Theme.Colors(Variant.Info).Base,
            new ColorToken(Color.Transparent), CenterX: 0.2f, CenterY: 0f, RadiusX: 800, RadiusY: 500);
        var node = Render(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 400, Glow = glow }));

        node.Attributes["style"].Should().Contain(
            $"background-image: radial-gradient(800px 500px at 20% 0%, "
            + $"{TokenCss.Value(Theme.Colors(Variant.Info).Base)}, #00000000)");
    }

    /// <summary>All three background layers stack in CSS paint order (gradient on top, then the
    /// glow, then the grid) — the same back-to-front order the native realizer draws them.</summary>
    [Fact]
    public void AllThreeBackgroundLayers_StackInPaintOrder_WithAlignedSizes()
    {
        var node = Render(new Box(new BoxStyle
        {
            Width = 300,
            Height = 300,
            Gradient = new LinearGradient(Theme.Scrim, new ColorToken(Color.Transparent)),
            Glow = new RadialGradient(Theme.Colors(Variant.Info).Base, new ColorToken(Color.Transparent),
                0.5f, 0.5f, 200, 120),
            Patterns = [new GridPattern(56, Theme.Border)],
        }));

        var style = node.Attributes["style"] ?? "";
        var linear = style.IndexOf("linear-gradient(to right", StringComparison.Ordinal);
        var radial = style.IndexOf("radial-gradient(", StringComparison.Ordinal);
        var grid = style.IndexOf("transparent 1px", StringComparison.Ordinal);
        linear.Should().BeLessThan(radial, "the linear gradient is the topmost layer");
        radial.Should().BeLessThan(grid, "the glow sits above the grid");
        style.Should().Contain("background-size: auto, auto, 56px 56px, 56px 56px",
            "one size entry per layer — the grid contributes two");
    }

    /// <summary>With BOTH set the gradient must paint ABOVE the grid — in CSS the first layer wins,
    /// and background-size needs the `auto` placeholder so the entries stay aligned.</summary>
    [Fact]
    public void GridPattern_UnderAGradient_KeepsLayerOrderAndSizeAlignment()
    {
        var node = Render(new Box(new BoxStyle
        {
            Width = 200,
            Height = 200,
            Gradient = new LinearGradient(Theme.Scrim, new ColorToken(Color.Transparent)),
            Patterns = [new GridPattern(56, Theme.Border)],
        }));

        var style = node.Attributes["style"];
        style.Should().Contain("background-image: linear-gradient(to right, ");
        style.Should().Contain("background-size: auto, 56px 56px, 56px 56px");
        style.IndexOf(TokenCss.Gradient(new LinearGradient(Theme.Scrim, new ColorToken(Color.Transparent))), StringComparison.Ordinal)
            .Should().BeLessThan(style.IndexOf("transparent 1px", StringComparison.Ordinal),
                "the gradient layer is listed first, so it paints on top of the grid");
    }

    // -- The patterns of #1937: each list, padded, and written only when it is not CSS's default ------

    /// <summary>A small tile painted in its own colour: one document whatever the mode.</summary>
    private static readonly VectorDrawing Square = new(0, 0, 10, 10,
        [new VectorShape("M0 0H10V10H0Z", VectorPaint.Solid(Color.FromRgb(0xC8, 0x79, 0x6B)))]);

    /// <summary>Its data URL, written out by hand from the table: the oracle the lowering is held to.</summary>
    private const string SquareUrl = "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 10 10' "
        + "width='10' height='10' preserveAspectRatio='none' fill='none'%3E%3Cpath d='M0 0H10V10H0Z' fill='%23c8796b'/%3E%3C/svg%3E\")";

    private static string Style(params Pattern[] patterns) =>
        Render(new Box(new BoxStyle { Width = 200, Height = 200, Patterns = patterns })).Attributes["style"]!;

    [Fact]
    public void ADefaultGrid_WritesNoListButTheImagesAndTheSizes()
    {
        var style = Style(new GridPattern(56, Theme.Border));
        style.Should().Contain("background-size: 56px 56px, 56px 56px");
        foreach (var list in new[] { "background-repeat", "background-position", "background-origin", "background-clip", "background-attachment" })
            style.Should().NotContain(list, "every entry is CSS's default, which is what the box wrote before #1937");
    }

    [Fact]
    public void ADrawingTile_IsADataUrlOfItsArtwork_WithAnAutoSide()
    {
        var style = Style(new DrawingPattern(Square, 330));
        style.Should().Contain($"background-image: {SquareUrl}");
        style.Should().Contain("background-size: 330px auto");
    }

    [Fact]
    public void TwoPatterns_UnderAGradientAndAGlow_KeepTheOrderAndPadEveryList()
    {
        var gradient = new LinearGradient(Theme.Scrim, new ColorToken(Color.Transparent));
        var glow = new RadialGradient(Theme.Colors(Variant.Info).Base, new ColorToken(Color.Transparent), 0.5f, 0.5f, 200, 120);
        var style = Render(new Box(new BoxStyle
        {
            Width = 300,
            Height = 300,
            Gradient = gradient,
            Glow = glow,
            Patterns = [new GridPattern(56, Theme.Border), new DrawingPattern(Square, 40) { HorizontalRepeat = TileRepeat.Once }],
        })).Attributes["style"]!;

        var line = TokenCss.Value(Theme.Border);
        style.Should().Contain($"background-image: {TokenCss.Gradient(gradient)}, {TokenCss.Glow(glow)}, {SquareUrl}, "
            + $"linear-gradient(to right, {line} 1px, transparent 1px), linear-gradient(to bottom, {line} 1px, transparent 1px)",
            "the last pattern is on top, so it comes first after the glow, as Photon paints it last");
        style.Should().Contain("background-size: auto, auto, 40px auto, 56px 56px, 56px 56px");
        style.Should().Contain("background-repeat: repeat, repeat, no-repeat repeat, repeat, repeat",
            "a list one entry needs is written for every layer, or CSS would repeat it from the start");
    }

    [Fact]
    public void OnceAcrossAndSpacedDown_WritesBothKeywords() =>
        Style(new DrawingPattern(Square, 40) { HorizontalRepeat = TileRepeat.Once, VerticalRepeat = TileRepeat.Spaced })
            .Should().Contain("background-repeat: no-repeat space");

    [Fact]
    public void FittedBothWays_IsRound() =>
        Style(new DrawingPattern(Square, 40) { HorizontalRepeat = TileRepeat.Fitted, VerticalRepeat = TileRepeat.Fitted })
            .Should().Contain("background-repeat: round;");

    [Fact]
    public void AnAnchorWithAnOffset_IsACalcOnEachAxis() =>
        Style(new DrawingPattern(Square, 40) { Anchor = new Point(0.5f, 1), Offset = new Point(12, -8) })
            .Should().Contain("background-position: calc(50% + 12px) calc(100% - 8px)");

    [Fact]
    public void AFraction_IsAPercentage_AndAFit_ItsKeyword()
    {
        Style(new DrawingPattern(Square, TileLength.Fraction(0.25f))).Should().Contain("background-size: 25% auto");
        Style(new DrawingPattern(Square) { Fit = ImageFit.Cover }).Should().Contain("background-size: cover");
        Style(new DrawingPattern(Square) { Fit = ImageFit.Contain }).Should().Contain("background-size: contain");
        Style(new DrawingPattern(Square) { Fit = ImageFit.Stretch }).Should().Contain("background-size: 100% 100%");
    }

    [Fact]
    public void TheAreas_AreTheOriginAndTheClip()
    {
        var style = Style(new DrawingPattern(Square, 40) { Area = BoxArea.Content, ClipTo = BoxArea.Padding });
        style.Should().Contain("background-origin: content-box");
        style.Should().Contain("background-clip: padding-box");
        Style(new GridPattern(50, Theme.Border) { Area = BoxArea.Border })
            .Should().Contain("background-origin: border-box, border-box", "a grid's two layers are placed alike");
    }

    [Fact]
    public void AWindowAttachment_IsFixed_AndEveryOtherLayerScrolls()
    {
        var style = Render(new Box(new BoxStyle
        {
            Gradient = new LinearGradient(Theme.Scrim, new ColorToken(Color.Transparent)),
            Patterns = [new DrawingPattern(Square, 40) { Attachment = TileAttachment.Window }],
        })).Attributes["style"]!;
        style.Should().Contain("background-attachment: scroll, fixed");
    }

    [Fact]
    public void AnImageTile_IsItsSourceAsACssString()
    {
        var style = Style(new ImagePattern("img/linen.png", 120));
        style.Should().Contain("background-image: url(\"img/linen.png\")");
        style.Should().Contain("background-size: 120px auto");
    }

    [Fact]
    public void AnOpacity_IsOneGroupAroundTheShapes() =>
        Style(new DrawingPattern(Square, 40) { Opacity = 0.5f })
            .Should().Contain("fill='none'%3E%3Cg opacity='0.5'%3E%3Cpath d='M0 0H10V10H0Z' fill='%23c8796b'/%3E%3C/g%3E%3C/svg%3E");

    /// <summary>
    /// A tile whose shapes inherit its tint varies by mode, so the element carries each variant as a
    /// custom property, valid only while the theme's toggle selects it, and the layer reads them
    /// through nested fallbacks (design.md decision 8). Photon's TextPrimary keeps its pair at More,
    /// so only the mode varies.
    /// </summary>
    [Fact]
    public void ATintThatDiffersByMode_IsAVariantPerMode_ReadThroughTheToggles()
    {
        var inked = new VectorDrawing(0, 0, 10, 10, [new VectorShape("M0 0H10V10H0Z", VectorPaint.Inherit)]);
        var style = Style(new DrawingPattern(inked, 40));

        style.Should().Contain("background-image: var(--eq-p0-l, var(--eq-p0-d))");
        var light = WebRealizer.CssColor(Theme.TextPrimary.Light).Replace("#", "%23");
        var dark = WebRealizer.CssColor(Theme.TextPrimary.Dark).Replace("#", "%23");
        style.Should().Contain($"--eq-p0-l: var(--eq-on-light) url(\"data:image/svg+xml,");
        style.Should().Contain($"fill='none' color='{light}'%3E");
        style.Should().Contain($"--eq-p0-d: var(--eq-on-dark) url(\"data:image/svg+xml,");
        style.Should().Contain($"fill='none' color='{dark}'%3E");
    }

    [Fact]
    public void ATintThatDiffersByContrast_IsAVariantPerLeg()
    {
        var inked = new VectorDrawing(0, 0, 10, 10, [new VectorShape("M0 0H10V10H0Z", VectorPaint.Inherit)]);
        var tint = new ColorToken(Color.FromRgb(1, 2, 3), Color.FromRgb(4, 5, 6)).WithMore(Color.FromRgb(7, 8, 9), Color.FromRgb(10, 11, 12));
        var style = Style(new GridPattern(8, Theme.Border), new DrawingPattern(inked, 40, tint: tint));

        style.Should().Contain("background-image: var(--eq-p1-ls, var(--eq-p1-lm, var(--eq-p1-ds, var(--eq-p1-dm)))), ",
            "the index is the pattern's place in the list");
        style.Should().Contain("--eq-p1-lm: var(--eq-on-light) var(--eq-on-more) url(");
        style.Should().Contain("color='%23070809'");
    }

    [Fact]
    public void ADarkSource_IsAVariantPerMode()
    {
        var style = Style(new ImagePattern("img/linen.png", 120) { DarkSource = "img/linen-dark.png" });
        style.Should().Contain("background-image: var(--eq-p0-l, var(--eq-p0-d))");
        style.Should().Contain("--eq-p0-l: var(--eq-on-light) url(\"img/linen.png\")");
        style.Should().Contain("--eq-p0-d: var(--eq-on-dark) url(\"img/linen-dark.png\")");
    }

    [Fact]
    public void AHoverGradient_PadsEveryListTheBaseWrote()
    {
        var gradient = new LinearGradient(Theme.Scrim, new ColorToken(Color.Transparent));
        var sink = new StyleSink();
        WebRealizer.Lower(new Box(new BoxStyle
        {
            Patterns = [new DrawingPattern(Square, 40) { HorizontalRepeat = TileRepeat.Once }],
            Hover = new StyleDiff { Gradient = gradient },
        }), Theme, 1f, sink);
        var css = sink.Css;
        css.Should().Contain(":hover{background-repeat:repeat, no-repeat repeat}",
            "under the hover the gradient is the first layer, so the base's entry moves to the second");
    }

    [Fact]
    public void SkeletonShimmer_ClippedTrack_RestHiddenGlint_MirroredGradients()
    {
        var track = Render(new Skeleton(SkeletonShape.Line, 160));

        track.Attributes["style"].Should().Contain($"background-color: {TokenCss.Value(Theme.SurfaceSubtle)}");
        // The two properties, not the two ADJACENT: a box now declares its hit-target state
        // between them, and an assertion that spanned the boundary was asserting the order as
        // much as the values.
        track.Attributes["style"].Should().Contain("border-radius: 999px");
        track.Attributes["style"].Should().Contain("overflow: hidden");

        var layer = track.Children[0];
        layer.Attributes["class"].Should().Be("eq-loop eq-loop-rest-hidden",
            "the decorative glint hides entirely under prefers-reduced-motion (spec B16)");
        layer.Attributes["style"].Should().Contain("animation: eq-slide-x 1400ms linear infinite");
        layer.Attributes["style"].Should().EndWith("--eq-loop-from: -100%; --eq-loop-to: 100%");

        var glint = layer.Children[0];
        var highlight = TokenCss.Value(Theme.SurfaceHighlight);
        glint.Children[0].Children[0].Attributes["style"].Should().Contain(
            $"background-image: linear-gradient(to right, #00000000, {highlight})");
        glint.Children[1].Children[0].Attributes["style"].Should().Contain(
            $"background-image: linear-gradient(to right, {highlight}, #00000000)");
    }

    [Fact]
    public void GeneratedStylesheet_HidesRestHiddenLoops_AndCarriesTheHighlightToken()
    {
        var css = ThemeStylesheet.Generate(Theme);

        css.Should().Contain(
            "@media (prefers-reduced-motion: reduce) { .eq-loop { animation: none; } .eq-loop-rest-hidden { visibility: hidden; } }");
        css.Should().Contain($"--eq-color-surface-highlight: {TokenCss.Value(Theme.SurfaceHighlight)};");
    }
}
