using eQuantic.UI.Primitives;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The vocabulary of a box's patterns (#1937): the closed set, the refusals of each kind, each
/// naming its member, the one C# answer for a tile's size, and the value equality of each kind.
/// </summary>
public class PatternVocabularyTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    /// <summary>The demand's tile: 675 × 518 on its own grid.</summary>
    private static readonly VectorDrawing Art = new(0, 0, 675, 518,
        [new VectorShape("M0 0H675V518H0Z", VectorPaint.Solid(Color.FromRgb(0xC8, 0x79, 0x6B)))]);

    private static readonly Size Desk = new(1600, 1000);

    // -- The refusals, each naming its member ------------------------------------------------------

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void ANegativeOrUnboundedDpLength_IsRefused(float dp)
    {
        var refuse = () => { TileLength _ = dp; };
        refuse.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("dp");
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.5f)]
    [InlineData(1.01f)]
    [InlineData(float.NaN)]
    public void AFractionOutsideZeroToOne_IsRefused(float fraction)
    {
        var refuse = () => TileLength.Fraction(fraction);
        refuse.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("fraction");
    }

    [Fact]
    public void AFractionOfOne_IsTheWholeArea() =>
        new DrawingPattern(Art, TileLength.Fraction(1)).SizeIn(Desk, Art.Aspect).Width.Should().Be(1600);

    [Fact]
    public void AFitBesideALength_IsRefused_NamingFit()
    {
        var beside = () => new DrawingPattern(Art, 330) { Fit = ImageFit.Cover };
        beside.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("Fit");
        var besideHeight = () => new ImagePattern("img/linen.png", height: 40) { Fit = ImageFit.Contain };
        besideHeight.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("Fit");
    }

    /// <summary>A tile's fit is set after its constructor, so the tile with neither a length nor a fit is
    /// refused where it enters a box, which is the first place both are known.</summary>
    [Fact]
    public void ATileWithNeitherALengthNorAFit_IsRefused_WhereItEntersTheBox()
    {
        var unsized = () => new BoxStyle { Patterns = [new DrawingPattern(Art)] };
        unsized.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("Fit");

        var fitted = () => new BoxStyle { Patterns = [new DrawingPattern(Art) { Fit = ImageFit.Contain }] };
        fitted.Should().NotThrow("a fit sizes the tile");
        var grid = () => new BoxStyle { Patterns = [new GridPattern(24, Theme.Border)] };
        grid.Should().NotThrow("a grid is sized by its cell");
    }

    [Theory]
    [InlineData(1.5f, 0f)]
    [InlineData(0f, -0.1f)]
    [InlineData(float.NaN, 0f)]
    public void AnAnchorOutsideZeroToOne_IsRefused_NamingAnchor(float x, float y)
    {
        var refuse = () => new DrawingPattern(Art, 330) { Anchor = new Point(x, y) };
        refuse.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("Anchor");
    }

    [Theory]
    [InlineData(-0.01f)]
    [InlineData(1.01f)]
    [InlineData(float.NaN)]
    public void AnOpacityOutsideZeroToOne_IsRefused_NamingOpacity(float opacity)
    {
        var refuse = () => new DrawingPattern(Art, 330) { Opacity = opacity };
        refuse.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("Opacity");
    }

    /// <summary>
    /// A grid's cell and rule are finite numbers above 0: Photon paints a grid rule by rule, and a cell
    /// or a rule that is not finite gave that loop no end (security review P1, #1937). A <c>with</c>
    /// passes the same refusal.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void AGridCellOrRuleThatIsNotAFiniteNumberAboveZero_IsRefused_NamingIt(float value)
    {
        var cell = () => new GridPattern(value, Theme.Border);
        cell.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("Cell");
        var rule = () => new GridPattern(24, Theme.Border, value);
        rule.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("LineWidth");
        var with = () => new GridPattern(24, Theme.Border) with { LineWidth = value };
        with.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("LineWidth");
    }

    [Fact]
    public void AGridOfAnyFiniteCellAndRuleAboveZero_IsTaken()
    {
        var grid = new GridPattern(0.01f, Theme.Border, 1e9f);
        grid.Cell.Should().Be(0.01f);
        grid.LineWidth.Should().Be(1e9f, "a rule wider than its cell fills it, as the web's gradient does");
    }

    [Fact]
    public void AnEmptySource_IsRefused_NamingIt()
    {
        var empty = () => new ImagePattern("", 120);
        empty.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("source");
        var emptyDark = () => new ImagePattern("img/linen.png", 120) { DarkSource = "" };
        emptyDark.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("DarkSource");
    }

    // -- The size ------------------------------------------------------------------------------------

    [Fact]
    public void ADpWidth_TakesItsHeightFromTheAspect() =>
        new DrawingPattern(Art, 330).SizeIn(Desk, Art.Aspect).Should().Be(new Size(330, 330 / (675f / 518f)));

    [Fact]
    public void ADpHeight_TakesItsWidthFromTheAspect() =>
        new DrawingPattern(Art, height: 100).SizeIn(Desk, Art.Aspect).Should().Be(new Size(100 * (675f / 518f), 100));

    [Fact]
    public void AFraction_IsOfThePlacingArea()
    {
        var size = new DrawingPattern(Art, TileLength.Fraction(0.25f)).SizeIn(Desk, Art.Aspect);
        size.Width.Should().Be(400);
        size.Height.Should().BeApproximately(306.963f, 0.001f);
    }

    [Fact]
    public void TwoLengths_AreUsedAsTheyAre() =>
        new DrawingPattern(Art, 100, TileLength.Fraction(0.5f)).SizeIn(Desk, Art.Aspect).Should().Be(new Size(100, 500));

    [Theory]
    [InlineData(ImageFit.Contain, 1000 * 675f / 518f, 1000)]
    [InlineData(ImageFit.Cover, 1600, 1600 / (675f / 518f))]
    [InlineData(ImageFit.Stretch, 1600, 1000)]
    public void AFit_IsAgainstTheArea(ImageFit fit, float width, float height)
    {
        var size = new DrawingPattern(Art) { Fit = fit }.SizeIn(Desk, Art.Aspect);
        size.Width.Should().BeApproximately(width, 0.001f);
        size.Height.Should().BeApproximately(height, 0.001f);
    }

    [Fact]
    public void AFit_OfAnAreaTallerThanTheArtwork_MeetsItsWidth()
    {
        var tall = new Size(400, 1000);
        new DrawingPattern(Art) { Fit = ImageFit.Contain }.SizeIn(tall, Art.Aspect).Width.Should().Be(400);
        new DrawingPattern(Art) { Fit = ImageFit.Cover }.SizeIn(tall, Art.Aspect).Height.Should().Be(1000);
    }

    // -- Equality and the conversion ----------------------------------------------------------------

    [Fact]
    public void EachKind_IsEqualByValue()
    {
        new GridPattern(56, Theme.Border).Should().Be(new GridPattern(56, Theme.Border));
        new GridPattern(56, Theme.Border).Should().NotBe(new GridPattern(56, Theme.Border) { ClipTo = BoxArea.Padding });

        new DrawingPattern(Art, 330) { Opacity = 0.09f }.Should().Be(new DrawingPattern(Art, 330) { Opacity = 0.09f });
        new DrawingPattern(Art, 330).Should().NotBe(new DrawingPattern(Art, 331));
        new DrawingPattern(Art, 330).Should().NotBe(new DrawingPattern(Art, 330) { Anchor = new Point(0.5f, 0.5f) });

        new ImagePattern("a.png", 120) { DarkSource = "b.png" }.Should().Be(new ImagePattern("a.png", 120) { DarkSource = "b.png" });
        new ImagePattern("a.png", 120).Should().NotBe(new ImagePattern("a.png", 120) { DarkSource = "b.png" });
        ((Pattern)new ImagePattern("a.png", 120)).Should().NotBe(new DrawingPattern(Art, 120));
    }

    [Fact]
    public void ANumber_IsALengthInDp_AndTheDefaultIsAuto()
    {
        TileLength dp = 330;
        dp.IsAuto.Should().BeFalse();
        dp.IsFraction.Should().BeFalse();
        dp.Value.Should().Be(330);
        dp.Should().Be((TileLength)330f);

        default(TileLength).IsAuto.Should().BeTrue();
        TileLength.Fraction(0.5f).IsFraction.Should().BeTrue();
        TileLength.Fraction(0.5f).Should().NotBe((TileLength)0.5f, "half the area is not half a dp");
    }

    [Fact]
    public void TheDefaults_AreCssBackgroundsOwn()
    {
        var tile = new DrawingPattern(Art, 330);
        tile.Area.Should().Be(BoxArea.Padding);
        tile.ClipTo.Should().Be(BoxArea.Border);
        tile.Attachment.Should().Be(TileAttachment.Box);
        tile.HorizontalRepeat.Should().Be(TileRepeat.Repeat);
        tile.VerticalRepeat.Should().Be(TileRepeat.Repeat);
        tile.Anchor.Should().Be(new Point(0, 0));
        tile.Opacity.Should().Be(1);
    }

    // -- The closed set ------------------------------------------------------------------------------

    /// <summary>
    /// A type outside the vocabulary cannot be a pattern: a record's copy constructor must stay
    /// protected, so a derived record could chain to it, and only the private protected dispatch
    /// keeps it out. Compiled against the real assembly, as an app's code would be.
    /// </summary>
    [Theory]
    [InlineData("Pattern")]
    [InlineData("ArtworkPattern")]
    public void ATypeOutsideTheVocabulary_CannotDeriveAKind(string root)
    {
        var source = $$"""
            using eQuantic.UI.Primitives;
            public sealed record Outside : {{root}}
            {
                public Outside(Outside original) : base(original) { }
                public Outside() : base(new DrawingPattern(VectorDrawing.Empty, 1)) { }
            }
            """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Append(typeof(Pattern).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => TestReferences.Of(path));
        var compilation = CSharpCompilation.Create("Outside", [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Should().Contain(d => d.Id == "CS0534",
            "a kind must implement the dispatch it cannot reach from outside the assembly");
    }

    /// <summary>The visitor reaches each kind's own method.</summary>
    [Fact]
    public void Accept_HandsEachKindToItsOwnVisit()
    {
        var names = new KindNames();
        ((Pattern)new GridPattern(8, Theme.Border)).Accept(names, 0).Should().Be("grid");
        ((Pattern)new DrawingPattern(Art, 8)).Accept(names, 0).Should().Be("drawing");
        ((Pattern)new ImagePattern("a.png", 8)).Accept(names, 0).Should().Be("image");
    }

    private sealed class KindNames : IPatternVisitor<int, string>
    {
        public string Visit(GridPattern pattern, int state) => "grid";
        public string Visit(DrawingPattern pattern, int state) => "drawing";
        public string Visit(ImagePattern pattern, int state) => "image";
    }
}
