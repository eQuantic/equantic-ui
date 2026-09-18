using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// C7 Slider: the figures the handoff measures.
/// <para>
/// Three of them were wrong, and the audit had caught one. The thumb was 20dp where the block says
/// 24, its ring was 2dp where the block says 1 — two deviations compounding, so the white face came
/// out 16dp across instead of 22 — and the REST half of the track was painted
/// <c>BorderStrong</c>, the token for a line that DIVIDES, where the block says
/// <c>SurfaceSubtle</c>. A slider at 10% read as mostly full.
/// </para>
/// <para>
/// Asserted on the BUILT tree rather than on lowered markup, because these are the component's
/// measurements and not a target's: the same numbers have to reach Photon's paint and the web's
/// CSS, and a test that read one target's output would pin only that one.
/// </para>
/// </summary>
public class SliderHandoffFidelityTests
{
    private static readonly ComponentContext Context = new(PhotonTheme.Instance);

    private static IEnumerable<VisualNode> Walk(VisualNode node)
    {
        yield return node;
        foreach (var child in Children(node))
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static IEnumerable<VisualNode> Children(VisualNode node) => node switch
    {
        SingleChildNode single => single.Child is null ? [] : [single.Child],
        Box box => box.Child is null ? [] : [box.Child],
        FlexNode flex => flex.Children,
        Stack stack => stack.Children,
        _ => [],
    };

    private static IEnumerable<BoxStyle> Boxes(Slider slider) =>
        Walk(slider.Build(Context)).OfType<Box>().Select(box => box.Style);

    /// <summary>"Thumb 24dp white + E2 + 1dp Border" — all four figures, because the size and the
    /// ring were wrong together and fixing one alone still misses the face.</summary>
    [Fact]
    public void TheThumbIsTheHandoffsKnob()
    {
        var theme = Context.Theme;
        var thumb = Boxes(new Slider(0.5f, _ => { }))
            .Single(style => style.Elevation == 2 && style.BorderWidth > 0);

        thumb.Width.Value.Should().Be(24, "spec C7: the thumb is 24dp, and it is the finger's target");
        thumb.Height.Value.Should().Be(24);
        thumb.BorderWidth.Should().Be(1, "spec C7: a hairline ring, not a 2dp one");
        thumb.Background.Should().Be(theme.Surface, "the knob is the surface colour, whichever mode is on");
        thumb.BorderColor.Should().Be(theme.Border,
            "the hairline is the ELEVATION contract's, not a tint — IAppTheme.Elevation says dark E1-E2 "
            + "require a 1dp border, and the variant already reads as the filled half of the track");
        thumb.Elevation.Should().Be(2, "spec C7: E2");
    }

    /// <summary>
    /// "Track 4dp Radius.Full: active Primary, rest SurfaceSubtle (two rrects)." The two halves are
    /// told apart by their corner rounding — the outer end of each is round and the inner end butts
    /// against the thumb — which is also the pair the handoff calls two rrects.
    /// </summary>
    [Fact]
    public void TheTrackIsFilledInPrimaryOverAGrooveNotABorder()
    {
        var theme = Context.Theme;
        var bars = Boxes(new Slider(0.5f, _ => { }))
            .Where(style => style.Height.Value == 4 && style.Background is not null)
            .ToArray();

        bars.Should().HaveCount(2, "spec C7: two rrects, one per side of the thumb");
        bars[0].Background.Should().Be(theme.Colors(Variant.Primary).Base, "the active half is Primary");
        bars[1].Background.Should().Be(theme.SurfaceSubtle,
            "spec C7: the rest half is SurfaceSubtle — BorderStrong is the token for a line that divides, "
            + "and it painted the unfilled rail as dark as a text field's outline");
    }

    /// <summary>
    /// The rest half must not follow the accent: a Destructive slider's groove is still the surface
    /// tone, or the unfilled part of the track would read as a second value.
    /// </summary>
    [Fact]
    public void TheGrooveDoesNotTakeTheVariantsColour()
    {
        var theme = Context.Theme;
        var bars = Boxes(new Slider(0.5f, _ => { }) { Variant = Variant.Destructive })
            .Where(style => style.Height.Value == 4 && style.Background is not null)
            .ToArray();

        bars[0].Background.Should().Be(theme.Colors(Variant.Destructive).Base);
        bars[1].Background.Should().Be(theme.SurfaceSubtle);

        Boxes(new Slider(0.5f, _ => { }) { Variant = Variant.Destructive })
            .Single(style => style.Elevation == 2 && style.BorderWidth > 0)
            .BorderColor.Should().Be(theme.Border, "nor does the knob's hairline");
    }

    /// <summary>
    /// A DISABLED slider dims the whole control and drops the accent, and the groove stays the
    /// groove — the two halves must still be told apart, or a disabled slider loses its readout.
    /// </summary>
    [Fact]
    public void ADisabledSliderStillReadsAsAValue()
    {
        var theme = Context.Theme;
        var bars = Boxes(new Slider(0.5f, _ => { }) { Disabled = true })
            .Where(style => style.Height.Value == 4 && style.Background is not null)
            .ToArray();

        bars[0].Background.Should().Be(theme.BorderStrong, "the filled half drops the accent when disabled");
        bars[1].Background.Should().Be(theme.SurfaceSubtle);
        bars[0].Background.Should().NotBe(bars[1].Background, "the value must still be readable");
    }
}
