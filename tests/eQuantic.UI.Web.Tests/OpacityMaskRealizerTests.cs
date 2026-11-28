using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A subtree faded along a gradient, lowered for the web (#1938): ONE wrapper <c>div</c> carrying
/// <c>mask-image</c>, the mask's RESOLVED stops written as CSS writes a gradient, and the width contract of the
/// child it stands in for. CROSS-PINNED by <c>masked.spec.ts</c>, which asserts the same literals on the twin, and by
/// the <c>masked-*</c> cases of the component parity fixture, which pin the two writers' atomic class names to each
/// other.
/// </summary>
public class OpacityMaskRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    private static string Style(HtmlNode node) => node.Attributes.GetValueOrDefault("style") ?? "";

    private static Primitives.Box Hugging() => new(new BoxStyle { Width = 120, Height = 80 });

    private static readonly OpacityStop[] Ramp = [new(0, 1), new(1, 0)];

    /// <summary>The `mask-image` of the wrapper a mask lowers to.</summary>
    private static string MaskImage(OpacityGradient gradient)
    {
        var style = Style(Render(new OpacityMask(gradient, Hugging())));
        var start = style.IndexOf("mask-image: ", StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, "the wrapper carries the gradient: " + style);
        var value = style[(start + "mask-image: ".Length)..];
        // The value holds no `;`, so the declaration ends at the next one or at the end of the style.
        var end = value.IndexOf(';');
        return end < 0 ? value : value[..end];
    }

    // -- Each row of design.md decision 5's table ------------------------------------------------------------------

    [Fact]
    public void ALinearOpacityGradient_WritesItsAngleAndItsStopsAsOpacitiesOfBlack() =>
        MaskImage(new LinearOpacityGradient(135, Ramp)).Should().Be(
            "linear-gradient(135deg, rgb(0 0 0 / 1) 0%, rgb(0 0 0 / 0) 100%)");

    [Fact]
    public void TheResolvedStops_AreWritten_NeverTheStopsAsGiven() =>
        MaskImage(new LinearOpacityGradient(90, [new(0.6f, 1), new(0.3f, 0)])).Should().Be(
            "linear-gradient(90deg, rgb(0 0 0 / 1) 60%, rgb(0 0 0 / 0) 60%)",
            "the second stop moved up to the first's offset, so the browser's own fix-up has nothing left to do");

    [Fact]
    public void ASingleStop_IsWrittenTwice_At0And100() =>
        MaskImage(new LinearOpacityGradient(90, [new(0.3f, 0.5f)])).Should().Be(
            "linear-gradient(90deg, rgb(0 0 0 / 0.5) 0%, rgb(0 0 0 / 0.5) 100%)");

    [Fact]
    public void TheHerosField_IsAnEllipseOfSeventyPercentAtSeventyAndThirtyFive() =>
        MaskImage(new RadialOpacityGradient(new Point(0.7f, 0.35f), RadialExtent.Fraction(0.7f, 0.7f), [new(0.35f, 1), new(0.76f, 0)]))
            .Should().Be("radial-gradient(70% 70% at 70% 35%, rgb(0 0 0 / 1) 35%, rgb(0 0 0 / 0) 76%)");

    [Fact]
    public void ARadialOpacityGradientSizedByASide_WritesTheKeyword() =>
        MaskImage(new RadialOpacityGradient(new Point(0.5f, 0.5f), RadialExtent.ClosestSide, Ramp)).Should().Be(
            "radial-gradient(closest-side at 50% 50%, rgb(0 0 0 / 1) 0%, rgb(0 0 0 / 0) 100%)");

    [Theory]
    [InlineData(RadialExtentKind.FarthestSide, "farthest-side")]
    [InlineData(RadialExtentKind.ClosestCorner, "closest-corner")]
    [InlineData(RadialExtentKind.FarthestCorner, "farthest-corner")]
    public void EachNamedSize_WritesItsKeyword(RadialExtentKind kind, string keyword)
    {
        var extent = kind switch
        {
            RadialExtentKind.FarthestSide => RadialExtent.FarthestSide,
            RadialExtentKind.ClosestCorner => RadialExtent.ClosestCorner,
            _ => default,
        };
        MaskImage(new RadialOpacityGradient(new Point(0.2f, 0.9f), extent, Ramp)).Should().Be(
            $"radial-gradient({keyword} at 20% 90%, rgb(0 0 0 / 1) 0%, rgb(0 0 0 / 0) 100%)");
    }

    [Fact]
    public void ARadialOpacityGradientInDp_WritesTwoLengths() =>
        MaskImage(new RadialOpacityGradient(new Point(0.5f, 0.5f), RadialExtent.Dp(120, 80.5f), Ramp)).Should().Be(
            "radial-gradient(120px 80.5px at 50% 50%, rgb(0 0 0 / 1) 0%, rgb(0 0 0 / 0) 100%)");

    /// <summary>Measured with numbers.fsx: a float times 100 writes 5.5 and 32.5 where the double it widens to says
    /// 5.4999999… and 32.4999988…, which the twin must write alike.</summary>
    [Fact]
    public void AFractionOfAFloat_WritesTheDecimalsOfTheFloat() =>
        MaskImage(new LinearOpacityGradient(200.5f, [new(0.055f, 1), new(0.325f, 0.2f)])).Should().Be(
            "linear-gradient(200.5deg, rgb(0 0 0 / 1) 5.5%, rgb(0 0 0 / 0.2) 32.5%)");

    [Fact]
    public void AnOffsetOutsideZeroToOne_IsKept() =>
        MaskImage(new LinearOpacityGradient(90, [new(-0.5f, 1), new(1.5f, 0)])).Should().Be(
            "linear-gradient(90deg, rgb(0 0 0 / 1) -50%, rgb(0 0 0 / 0) 150%)");

    // -- The wrapper -----------------------------------------------------------------------------------------------

    [Fact]
    public void AOpacityMaskNode_IsOneDivHoldingTheChild()
    {
        var node = Render(new OpacityMask(new LinearOpacityGradient(90, Ramp), Hugging()));

        node.Tag.Should().Be("div");
        node.Children.Should().ContainSingle().Which.Tag.Should().Be("div");
        Style(node).Should().Contain("mask-image: linear-gradient(90deg");
    }

    [Fact]
    public void AOpacityGradientChangesNoHitTest_SoTheWrapperWritesNoPointerEvents() =>
        Style(Render(new OpacityMask(new LinearOpacityGradient(90, Ramp), Hugging()))).Should().NotContain("pointer-events");

    [Fact]
    public void AChildThatHugs_LeavesTheWrapperWithoutASize()
    {
        var style = Style(Render(new OpacityMask(new LinearOpacityGradient(90, Ramp), Hugging())));

        style.Should().NotContain("width").And.NotContain("height");
    }

    [Fact]
    public void AFillChild_MakesTheWrapperFillAndCarriesItsCap()
    {
        var capped = new Primitives.Box(new BoxStyle { Width = SizeValue.Fill, MaxWidth = 980, Height = SizeValue.Fill });

        var style = Style(Render(new OpacityMask(new LinearOpacityGradient(90, Ramp), capped)));

        style.Should().Contain("width: 100%").And.Contain("height: 100%").And.Contain("max-width: 980px");
    }

    [Fact]
    public void AFillChildInARow_MakesTheWrapperTheRowsItem()
    {
        var row = new Row(gap: 0) { Width = SizeValue.Fill, Main = MainAlign.Center };
        row.Add(new OpacityMask(new LinearOpacityGradient(90, Ramp),
            new Primitives.Box(new BoxStyle { Width = SizeValue.Fill, MaxWidth = 980 })));

        var wrapper = Render(row).Children.Should().ContainSingle().Which;

        Style(wrapper).Should().Contain("width: 100%").And.Contain("max-width: 980px");
    }

    /// <summary>A stack anchors a positioned child on both edges of an axis when the child fills it, and the
    /// wrapper is the child the stack sees (`Fills` looks through every layout-transparent wrapper).</summary>
    [Fact]
    public void APositionedOpacityMaskBox_StillSpansBetweenItsAnchors()
    {
        var stack = new Stack { Width = SizeValue.Fixed(200), Height = SizeValue.Fixed(90) };
        stack.Add(new Positioned(
            new OpacityMask(new LinearOpacityGradient(90, Ramp), new Primitives.Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill })),
            top: 30, end: 0));

        var anchor = Render(stack).Children.Should().ContainSingle().Which;

        Style(anchor).Should().Contain("inset-inline-start: 0").And.Contain("bottom: 0",
            "a child that fills spans between the anchors, as the same box does alone");
        Style(anchor.Children.Should().ContainSingle().Which).Should().Contain("width: 100%").And.Contain("height: 100%");
    }

    [Fact]
    public void AOpacityMaskNodeInsideAHoverable_PassesTheFillAndTheCapThrough()
    {
        var capped = new Primitives.Box(new BoxStyle { Width = SizeValue.Fill, MaxWidth = 320 });

        var host = Style(Render(new Hoverable(new OpacityMask(new LinearOpacityGradient(90, Ramp), capped), _ => { })));

        host.Should().Contain("width: 100%").And.Contain("max-width: 320px",
            "a wrapper around a OpacityMask stands in for it, so the whole width contract of the box inside crosses both");
    }
}
