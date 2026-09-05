using eQuantic.UI.Material;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The data palette and its audit, held to the numbers the data-visualization method publishes for
/// its reference instance. Those numbers were computed by the method's own validator (OKLab, Machado
/// 2009, WCAG) — so reproducing them to a tenth is what proves the C# port IS that method, and the
/// palette IS the validated instance. Every threshold has a case that must fail beside the case that
/// must pass: an audit that only ever said yes would not be an instrument.
/// </summary>
public class DataPaletteTests
{
    private const double Tol = 0.06; // the published numbers are rounded to one decimal

    private static Color Rgb(uint hex) => Color.FromRgb((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex);

    private static IReadOnlyList<Color> Series(ThemeMode mode) =>
        DataPalette.Default.Series.Select(t => t.Resolve(mode)).ToList();

    [Theory]
    [InlineData(ThemeMode.Light, 9.1, 19.6)]
    [InlineData(ThemeMode.Dark, 8.4, 19.3)]
    public void The_default_series_clear_every_hard_gate_on_the_adjacent_pairlist(ThemeMode mode, double cvd, double normal)
    {
        var report = PaletteAudit.Categorical(Series(mode), mode);

        report.Ok.Should().BeTrue(report.ToString());
        report.Lines.Should().HaveCount(5);
        report.Lines.Where(l => l.Check is "Lightness band" or "Chroma floor" or "CVD separation" or "Normal-vision floor")
            .Should().OnlyContain(l => l.Verdict == AuditVerdict.Pass, report.ToString());
        report.WorstCvdDeltaE.Should().BeApproximately(cvd, Tol, "the method publishes the worst adjacent CVD pair");
        report.WorstNormalDeltaE.Should().BeApproximately(normal, Tol, "and the worst adjacent normal-vision pair");
    }

    [Theory]
    [InlineData(ThemeMode.Light, 9.2, 24.0)]
    [InlineData(ThemeMode.Dark, 9.4, 20.9)]
    public void The_first_three_slots_clear_all_pairs_which_is_the_cap_for_scatter_and_small_multiples(
        ThemeMode mode, double cvd, double normal)
    {
        var report = PaletteAudit.Categorical(Series(mode).Take(3).ToList(), mode, allPairs: true);

        report.Ok.Should().BeTrue(report.ToString());
        report.Lines.Single(l => l.Check == "CVD separation").Verdict.Should().Be(AuditVerdict.Pass);
        report.WorstCvdDeltaE.Should().BeApproximately(cvd, Tol);
        report.WorstNormalDeltaE.Should().BeApproximately(normal, Tol);
    }

    [Theory]
    [InlineData(ThemeMode.Light)]
    [InlineData(ThemeMode.Dark)]
    public void The_full_eight_cannot_clear_all_pairs_so_the_audit_says_so(ThemeMode mode)
    {
        // Yellow sits beside orange once four slots are on screen together; on all pairs the
        // normal-vision floor (light) and the CVD floor (dark) both give way. The method documents
        // this as the series cap, and a palette change cannot fix it — only fewer series or facets.
        var report = PaletteAudit.Categorical(Series(mode), mode, allPairs: true);

        report.Ok.Should().BeFalse(report.ToString());
        report.Lines.Should().Contain(l => l.Verdict == AuditVerdict.Fail);
    }

    [Fact]
    public void Contrast_relief_on_the_light_surface_is_exactly_the_three_the_method_names()
    {
        var light = PaletteAudit.Categorical(Series(ThemeMode.Light), ThemeMode.Light);
        var dark = PaletteAudit.Categorical(Series(ThemeMode.Dark), ThemeMode.Dark);

        light.BelowContrast.Should().BeEquivalentTo([Rgb(0x1baf7a), Rgb(0xeda100), Rgb(0xe87ba4)],
            "aqua, yellow and magenta sit below 3:1 on the light surface by design — the relief rule applies");
        light.Lines.Single(l => l.Check == "Contrast vs surface").Verdict.Should().Be(AuditVerdict.Relief);
        dark.BelowContrast.Should().BeEmpty("the dark steps were chosen to clear 3:1 on the dark surface");
        dark.Lines.Single(l => l.Check == "Contrast vs surface").Verdict.Should().Be(AuditVerdict.Pass);
    }

    [Theory]
    [InlineData(0x0ca30c, 3.27, 5.19)] // good
    [InlineData(0xfab219, 1.79, 9.49)] // warning
    [InlineData(0xec835a, 2.57, 6.60)] // serious
    [InlineData(0xd03b3b, 4.68, 3.62)] // critical
    public void Status_contrast_reproduces_the_published_table(uint hex, double onLight, double onDark)
    {
        PaletteAudit.Contrast(Rgb(hex), PaletteAudit.DefaultSurface(ThemeMode.Light)).Should().BeApproximately(onLight, 0.006);
        PaletteAudit.Contrast(Rgb(hex), PaletteAudit.DefaultSurface(ThemeMode.Dark)).Should().BeApproximately(onDark, 0.006);
    }

    [Fact]
    public void Status_steps_are_the_documented_ones_in_both_modes()
    {
        var status = DataPalette.Default.Status;
        foreach (var mode in new[] { ThemeMode.Light, ThemeMode.Dark })
        {
            status.Good.Resolve(mode).Should().Be(Rgb(0x0ca30c));
            status.Warning.Resolve(mode).Should().Be(Rgb(0xfab219));
            status.Serious.Resolve(mode).Should().Be(Rgb(0xec835a));
            status.Critical.Resolve(mode).Should().Be(Rgb(0xd03b3b));
        }
    }

    [Fact]
    public void An_ordinal_ramp_reads_as_a_ramp_and_its_light_end_clears_the_surface()
    {
        // The method's own example (steps 250, 350, 500, 650 of the blue ramp): one hue, monotone,
        // and the lightest step at 2.06:1 on the light surface — documented as the floor case.
        Color[] ramp = [Rgb(0x86b6ef), Rgb(0x5598e7), Rgb(0x256abf), Rgb(0x104281)];

        var report = PaletteAudit.Ordinal(ramp, ThemeMode.Light);

        report.Ok.Should().BeTrue(report.ToString());
        report.Lines.Should().OnlyContain(l => l.Verdict == AuditVerdict.Pass);
        PaletteAudit.Contrast(Rgb(0x86b6ef), PaletteAudit.DefaultSurface(ThemeMode.Light)).Should().BeApproximately(2.06, 0.006);
    }

    [Fact]
    public void On_a_dark_surface_the_ramp_may_go_no_darker_than_step_600()
    {
        // Documented: step 600 (#184f95) is the darkest that still clears 2:1 on the dark surface at
        // 2.15:1; step 650 (#104281) does not. The same four steps, one swapped, must flip the verdict.
        Color[] clears = [Rgb(0x86b6ef), Rgb(0x5598e7), Rgb(0x256abf), Rgb(0x184f95)];
        Color[] tooDark = [Rgb(0x86b6ef), Rgb(0x5598e7), Rgb(0x256abf), Rgb(0x104281)];

        PaletteAudit.Ordinal(clears, ThemeMode.Dark).Ok.Should().BeTrue();
        PaletteAudit.Contrast(Rgb(0x184f95), PaletteAudit.DefaultSurface(ThemeMode.Dark)).Should().BeApproximately(2.15, 0.006);

        var failing = PaletteAudit.Ordinal(tooDark, ThemeMode.Dark);
        failing.Ok.Should().BeFalse(failing.ToString());
        failing.Lines.Single(l => l.Check == "Light-end contrast").Verdict.Should().Be(AuditVerdict.Fail);
    }

    [Fact]
    public void A_rainbow_is_not_a_ramp()
    {
        Color[] rainbow = [Rgb(0xe34948), Rgb(0xeda100), Rgb(0x008300), Rgb(0x2a78d6)];

        var report = PaletteAudit.Ordinal(rainbow, ThemeMode.Light);

        report.Lines.Single(l => l.Check == "Single hue").Verdict.Should().Be(AuditVerdict.Fail);
    }

    [Fact]
    public void The_default_sequential_ramp_is_monotone_one_hue_and_flips_its_anchor_in_dark_mode()
    {
        var light = DataPalette.Default.Sequential.Select(t => t.Resolve(ThemeMode.Light)).ToList();
        var dark = DataPalette.Default.Sequential.Select(t => t.Resolve(ThemeMode.Dark)).ToList();

        var report = PaletteAudit.Ordinal(light, ThemeMode.Light);
        report.Lines.Single(l => l.Check == "Lightness monotone").Verdict.Should().Be(AuditVerdict.Pass);
        report.Lines.Single(l => l.Check == "Single hue").Verdict.Should().Be(AuditVerdict.Pass);
        // The lightest sequential step is allowed to recede toward the surface (it means "near zero");
        // that is the ordinal floor case the method documents, so it is expected here, not a defect.
        report.Lines.Single(l => l.Check == "Light-end contrast").Verdict.Should().Be(AuditVerdict.Fail);

        dark.Should().Equal(light.AsEnumerable().Reverse(), "the step that means near-zero recedes toward the dark surface");
    }

    [Fact]
    public void The_diverging_poles_read_as_opposite_and_the_midpoint_as_nothing()
    {
        var scale = DataPalette.Default.Diverging;
        foreach (var mode in new[] { ThemeMode.Light, ThemeMode.Dark })
        {
            var hueGap = Math.Abs(PaletteAudit.Hue(scale.Negative.Resolve(mode)) - PaletteAudit.Hue(scale.Positive.Resolve(mode)));
            if (hueGap > 180) hueGap = 360 - hueGap;
            hueGap.Should().BeGreaterThan(90, "blue against red: warm against cool");
            PaletteAudit.Oklch(scale.Midpoint.Resolve(mode)).C.Should().BeLessThan(0.10, "the midpoint is a gray, never a hue");
        }
    }

    [Fact]
    public void A_ninth_series_is_refused_by_name()
    {
        var palette = DataPalette.Default;

        palette.SeriesColor(7).Should().Be(palette.Series[7]);
        var act = () => palette.SeriesColor(DataPalette.SeriesCeiling);

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*fold the tail into Other*");
    }

    [Fact]
    public void A_palette_with_the_wrong_number_of_slots_is_refused_at_construction()
    {
        var seven = DataPalette.Default.Series.Take(7).ToList();
        var act = () => new DataPalette(seven, DataPalette.Default.Sequential, DataPalette.Default.Diverging,
            DataPalette.Default.Other, DataPalette.Default.Status);

        act.Should().Throw<ArgumentException>().WithMessage("*exactly 8 series slots*");
    }

    [Fact]
    public void Every_theme_answers_the_reference_palette_until_it_brings_its_own()
    {
        IAppTheme material = MaterialTheme.FromSeed(Rgb(0x6750a4));

        material.Data.Should().BeSameAs(DataPalette.Default);
        material.Data.Series.Should().HaveCount(DataPalette.SeriesCeiling);
    }

    [Fact]
    public void Simulation_leaves_a_gray_alone_and_moves_a_red()
    {
        // A neutral has nothing for a deficiency to remove; a saturated red is exactly what protanopia
        // cannot see. The sanity check on the direction of the matrices, not their precision.
        var gray = Rgb(0x898781);
        var red = Rgb(0xe34948);

        PaletteAudit.DeltaE(gray, PaletteAudit.Simulate(gray, Cvd.Protan)).Should().BeLessThan(2);
        PaletteAudit.DeltaE(red, PaletteAudit.Simulate(red, Cvd.Protan)).Should().BeGreaterThan(20);
    }
}
