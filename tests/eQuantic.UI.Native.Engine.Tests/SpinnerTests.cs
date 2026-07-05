using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The Spinner (spec B15) inside the v1 fence — NO arcs: 8 rrect bars rotated i·45° about the
/// center, opacity phase-staggered on the 800ms/rev linear clock, a pure function of the frame
/// time. Reduce Motion keeps the fade but drops the rotation phase (all bars pulse in place) and
/// the spinner still reports active motion — it is a functional indicator, not decoration.
/// </summary>
public class SpinnerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static (RealizeResult Frame, DrawCommand[] Commands) Render(VisualNode root,
        float timeMs = 0, bool reducedMotion = false)
    {
        var builder = new DisplayListBuilder();
        var frame = PhotonRealizer.Realize(root, 64, 64, Theme, ThemeMode.Light, builder,
            timeMs: timeMs, reducedMotion: reducedMotion);
        return (frame, builder.Build().Commands.ToArray());
    }

    [Fact]
    public void Spinner_DrawsEightPhaseStaggeredBars()
    {
        var (frame, commands) = Render(new Spinner(IconSize.Md), timeMs: 0);

        frame.HasActiveMotion.Should().BeTrue("the spinner always animates");
        var bars = commands.Where(c => c.Kind == DrawCommandKind.FillRRect).ToList();
        bars.Should().HaveCount(8);

        // t=0: bar 0 is fully lit; the tail fades linearly behind it (alpha = 1 − 0.7·i/8).
        for (var i = 0; i < 8; i++)
        {
            var expected = (byte)MathF.Round(255 * (1f - 0.7f * (i / 8f)));
            bars[i].Paint.Color.A.Should().BeCloseTo(expected, 1,
                $"bar {i} rides the sawtooth at k=i/8 when t=0");
        }

        // Bars rotate about the center — every command carries a distinct baked transform.
        bars.Select(b => (b.Transform.M11, b.Transform.M12)).Distinct().Should()
            .HaveCountGreaterThan(4, "i·45° rotations produce distinct rotation matrices");
    }

    [Fact]
    public void Spinner_PhaseAdvancesWithTheClock()
    {
        // t = 100ms (one bar step): the lit bar moves from 0 to 1 — the rotation illusion.
        var (_, commands) = Render(new Spinner(IconSize.Md), timeMs: 100);
        var bars = commands.Where(c => c.Kind == DrawCommandKind.FillRRect).ToList();

        bars[1].Paint.Color.A.Should().Be(255, "bar 1 is fully lit at phase 1/8");
        bars[0].Paint.Color.A.Should().BeLessThan(255, "bar 0 has begun fading");
    }

    [Fact]
    public void ReduceMotion_PulsesInPlace_NoRotationPhase()
    {
        var (frame, commands) = Render(new Spinner(IconSize.Md), timeMs: 200, reducedMotion: true);

        frame.HasActiveMotion.Should().BeTrue("the pulse is the spec'd Reduce Motion behavior");
        var alphas = commands.Where(c => c.Kind == DrawCommandKind.FillRRect)
            .Select(c => c.Paint.Color.A).Distinct().ToList();
        alphas.Should().HaveCount(1, "every bar shares the pulse — no rotation stagger (spec B15)");
    }

    [Fact]
    public void Spinner_ScalesGeometryFromTheEmBox_AndRejectsOffWhitelistSizes()
    {
        var (_, commands) = Render(new Spinner(32));
        var bar = commands.First(c => c.Kind == DrawCommandKind.FillRRect);
        bar.Shape.Rect.Width.Should().Be(4, "2×scale at size 32 (scale 2)");
        bar.Shape.Rect.Height.Should().Be(10, "5×scale");
        bar.Shape.Radii.TopLeft.Should().Be(2, "1×scale — full-round bar ends");

        var act = () => new Spinner(18);
        act.Should().Throw<ArgumentOutOfRangeException>("sizes come from the §07 whitelist");
    }

    [Theory]
    [InlineData(ThemeMode.Light, "spinner-light")]
    [InlineData(ThemeMode.Dark, "spinner-dark")]
    public void Spinner_GoldenAtQuarterTurn(ThemeMode mode, string golden)
    {
        var row = new Row(gap: Space.S3) { Cross = CrossAlign.Center, Padding = EdgeInsets.All(Space.S2) };
        row.Add(new Spinner(IconSize.Sm));
        row.Add(new Spinner(IconSize.Dense));
        row.Add(new Spinner(IconSize.Md, PhotonTheme.Instance.Colors(Variant.Primary).Base));
        row.Add(new Spinner(IconSize.Lg));

        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(160, 48);
        var host = new PhotonHost(row, PhotonTheme.Instance, mode, 160, 48);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, timeMs: 200); // phase 0.25 — bar 2 lit
        backend.Render(builder.Build(), surface);

        GoldenImage.Match(surface, golden);
    }
}
