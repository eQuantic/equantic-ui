using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The controls the Studio gallery added: Stepper, SegmentedControl, PageIndicator and Slider.
/// Each is authored once against the abstract vocabulary, so these assert the BUILT TREE — the same
/// tree the web realizer and Photon both consume.
/// </summary>
public class GalleryComponentTests
{
    private static readonly ComponentContext Ctx = new(PhotonTheme.Instance);

    private static Box Frame(VisualNode tree) => tree.Should().BeOfType<Box>().Subject;

    /// <summary>The slider's track, under the layers an enabled one wears: the Adjustable that
    /// answers the arrows, the frame Box, the Draggable scrub.</summary>
    private static Row Track(VisualNode tree)
    {
        if (tree is Adjustable keyboard) tree = keyboard.Child;
        var inner = Frame(tree).Child;
        return (inner is Draggable scrub ? scrub.Child : inner!).Should().BeOfType<Row>().Subject;
    }

    // ---- Stepper ---------------------------------------------------------------------------

    [Theory]
    [InlineData(SizeVariant.Small)]
    [InlineData(SizeVariant.Medium)]
    [InlineData(SizeVariant.Large)]
    [InlineData(SizeVariant.XLarge)]
    public void Stepper_FrameReadsTheControlLadder(SizeVariant size)
    {
        var frame = Frame(new Stepper(3) { Size = size }.Build(Ctx));
        frame.Style.Height.Value.Should().Be(Sizing.Height(size));
        frame.Style.CornerRadius.TopLeft.Should().Be(Sizing.Radius(size));
    }

    [Fact]
    public void Stepper_ValueIsTabular_SoTheRowDoesNotJiggle()
    {
        var row = Frame(new Stepper(9) { Suffix = " GB" }.Build(Ctx)).Child.Should().BeOfType<Row>().Subject;
        var value = ((Row)row.Children[1].Should().BeOfType<Box>().Subject.Child!)
            .Children[0].Should().BeOfType<Text>().Subject;
        value.Content.Should().Be("9 GB");
        value.Tabular.Should().BeTrue();
        value.MaxLines.Should().Be(1);
    }

    [Fact]
    public void Stepper_AtTheEndsOfTheRange_DisablesTheArmRatherThanClampingSilently()
    {
        var atMax = Frame(new Stepper(5) { Min = 0, Max = 5 }.Build(Ctx)).Child.Should().BeOfType<Row>().Subject;
        Arm(atMax, first: true).Disabled.Should().BeFalse("5 can still go down");
        Arm(atMax, first: false).Disabled.Should().BeTrue("5 is the ceiling");

        var atMin = Frame(new Stepper(0) { Min = 0, Max = 5 }.Build(Ctx)).Child.Should().BeOfType<Row>().Subject;
        Arm(atMin, first: true).Disabled.Should().BeTrue("0 is the floor");
        Arm(atMin, first: false).Disabled.Should().BeFalse();

        static Pressable Arm(Row row, bool first) =>
            row.Children[first ? 0 : 2].Should().BeOfType<Pressable>().Subject;
    }

    [Fact]
    public void Stepper_StepAppliesToBothDirections()
    {
        float? got = null;
        var row = Frame(new Stepper(10, v => got = v) { Step = 5 }.Build(Ctx)).Child.Should().BeOfType<Row>().Subject;

        ((Pressable)row.Children[0]).OnPressed!();
        got.Should().Be(5);
        ((Pressable)row.Children[2]).OnPressed!();
        got.Should().Be(15);
    }

    // ---- SegmentedControl ------------------------------------------------------------------

    [Fact]
    public void SegmentedControl_StatesSelection_NotJustPaintsIt()
    {
        var row = Frame(new SegmentedControl(["All", "Income", "Expenses"], 1).Build(Ctx))
            .Child.Should().BeOfType<Row>().Subject;

        var selected = Segment(row, 1);
        selected.Selected.Should().BeTrue();
        selected.OnPressed.Should().BeNull("the selected segment is not a re-selection target");

        Segment(row, 0).Selected.Should().BeFalse("selectable, currently not selected — never null here");
        Segment(row, 2).Selected.Should().BeFalse();

        static Pressable Segment(Row row, int index) =>
            row.Children[index].Should().BeOfType<Flexible>().Subject
                .Child.Should().BeOfType<Pressable>().Subject;
    }

    [Fact]
    public void SegmentedControl_SelectionReadsAsElevation_OverARecessedTrack()
    {
        var theme = PhotonTheme.Instance;
        var track = Frame(new SegmentedControl(["A", "B"], 0).Build(Ctx));
        track.Style.Background.Should().Be(theme.SurfaceSubtle);

        var row = track.Child.Should().BeOfType<Row>().Subject;
        Segment(row, 0).Style.Elevation.Should().Be(1);
        Segment(row, 0).Style.Background.Should().Be(theme.Surface);
        Segment(row, 1).Style.Elevation.Should().Be(0);
        Segment(row, 1).Style.Background.Should().BeNull("an unselected segment is the track showing through");

        static Box Segment(Row row, int index) =>
            ((Pressable)((Flexible)row.Children[index]).Child).Child.Should().BeOfType<Box>().Subject;
    }

    [Fact]
    public void SegmentedControl_ThumbRadiusSitsInsideTheTrackLip()
    {
        var track = Frame(new SegmentedControl(["A", "B"], 0).Build(Ctx));
        var inset = track.Style.Padding.Top;
        var thumb = (Box)((Pressable)((Flexible)((Row)track.Child!).Children[0]).Child).Child;
        thumb.Style.CornerRadius.TopLeft.Should().Be(track.Style.CornerRadius.TopLeft - inset,
            "concentric radii — a thumb rounded like its track leaves a crescent gap");
    }

    // ---- PageIndicator ---------------------------------------------------------------------

    [Fact]
    public void PageIndicator_CurrentDotIsWider_SoPositionSurvivesGreyscale()
    {
        var row = new PageIndicator(4, 2).Build(Ctx).Should().BeOfType<Row>().Subject;
        row.Children.Should().HaveCount(4);

        var current = row.Children[2].Should().BeOfType<Box>().Subject;
        var other = row.Children[0].Should().BeOfType<Box>().Subject;
        current.Style.Width.Value.Should().BeGreaterThan(other.Style.Width.Value);
        current.Style.Height.Value.Should().Be(other.Style.Height.Value, "only the width carries the state");
    }

    [Fact]
    public void PageIndicator_PastItsOwnThreshold_BecomesAReadout()
    {
        var text = new PageIndicator(PageIndicator.MaxDots + 1, 3).Build(Ctx)
            .Should().BeOfType<Text>().Subject;
        text.Content.Should().Be($"Page 4 of {PageIndicator.MaxDots + 1}");
        text.Tabular.Should().BeTrue("the readout changes in place");
    }

    [Fact]
    public void PageIndicator_TappableDotsGrowToTheTouchMinimum()
    {
        var row = new PageIndicator(3, 0, _ => { }).Build(Ctx).Should().BeOfType<Row>().Subject;
        var press = row.Children[0].Should().BeOfType<Pressable>().Subject;
        press.Selected.Should().BeTrue();
        press.Child.Should().BeOfType<Box>().Which.Style.Height.Value.Should().Be(Touch.MinTarget);
    }

    [Fact]
    public void PageIndicator_WithoutACallback_StaysDecorative()
    {
        var row = new PageIndicator(3, 0).Build(Ctx).Should().BeOfType<Row>().Subject;
        row.Children.Should().AllBeOfType<Box>("non-interactive dots must not claim to be buttons");
    }

    // ---- Slider ----------------------------------------------------------------------------

    [Theory]
    [InlineData(0f, 1, 1000)]
    [InlineData(0.25f, 250, 750)]
    [InlineData(1f, 1000, 1)]
    public void Slider_FillProportionIsTheValue(float value, int filled, int rest)
    {
        var row = Track(new Slider(value).Build(Ctx));
        row.Children[0].Should().BeOfType<Flexible>().Which.Flex.Should().Be(filled);
        row.Children[2].Should().BeOfType<Flexible>().Which.Flex.Should().Be(rest);
    }

    [Fact]
    public void Slider_ClampsAValueOutsideItsRange()
    {
        var row = Track(new Slider(99) { Min = 0, Max = 10 }.Build(Ctx));
        row.Children[0].Should().BeOfType<Flexible>().Which.Flex.Should().Be(1000);
    }

    [Fact]
    public void Slider_TrackPressMovesOneStepTowardThePress()
    {
        float? got = null;
        var row = Track(new Slider(50, v => got = v) { Min = 0, Max = 100, Step = 10 }.Build(Ctx));

        Half(row, first: true).OnPressed!();
        got.Should().Be(40);
        Half(row, first: false).OnPressed!();
        got.Should().Be(60);

        static Pressable Half(Row row, bool first) =>
            ((Flexible)row.Children[first ? 0 : 2]).Child.Should().BeOfType<Pressable>().Subject;
    }

    [Fact]
    public void Slider_StepPressNeverLeavesTheRange()
    {
        float? got = null;
        var atMin = Track(new Slider(0, v => got = v) { Min = 0, Max = 1, Step = 0.25f }.Build(Ctx));
        ((Pressable)((Flexible)atMin.Children[0]).Child).OnPressed!();
        got.Should().Be(0);

        var atMax = Track(new Slider(1, v => got = v) { Min = 0, Max = 1, Step = 0.25f }.Build(Ctx));
        ((Pressable)((Flexible)atMax.Children[2]).Child).OnPressed!();
        got.Should().Be(1);
    }

    [Fact]
    public void Slider_Disabled_TakesNoPresses()
    {
        var row = Track(new Slider(0.5f, _ => throw new InvalidOperationException()) { Disabled = true }
            .Build(Ctx));
        ((Flexible)row.Children[0]).Child.Should().BeOfType<Box>("a disabled track is not a button");
        ((Flexible)row.Children[2]).Child.Should().BeOfType<Box>();
    }

    [Fact]
    public void Slider_TrackHalvesRoundOnlyTheirOuterEnds()
    {
        var row = Track(new Slider(0.5f).Build(Ctx));
        var filled = Bar(row, first: true);
        var rest = Bar(row, first: false);

        filled.Style.CornerRadius.TopLeft.Should().BeGreaterThan(0);
        filled.Style.CornerRadius.TopRight.Should().Be(0, "the inner end butts against the thumb");
        rest.Style.CornerRadius.TopLeft.Should().Be(0);
        rest.Style.CornerRadius.TopRight.Should().BeGreaterThan(0);

        static Box Bar(Row row, bool first)
        {
            var target = (Box)((Pressable)((Flexible)row.Children[first ? 0 : 2]).Child).Child;
            return (Box)((Column)target.Child!).Children[0];
        }
    }
}
