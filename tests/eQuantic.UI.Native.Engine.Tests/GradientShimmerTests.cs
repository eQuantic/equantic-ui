using eQuantic.UI.Components.Shared;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// BoxStyle's 2-stop gradient (the engine fence's exact primitive, animation slice 2) and its first
/// consumer, the Skeleton shimmer: a split-gradient glint sweeping -100%→100% on the 1.4s clock
/// inside the placeholder's clipping rrect. Reduce Motion HIDES the decorative layer entirely
/// (LoopMotion.HideAtRest) — the spec's plain-SurfaceSubtle behavior — and stops the frame loop.
/// </summary>
public class GradientShimmerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static DrawCommand[] Render(VisualNode root, float timeMs = 0, bool reducedMotion = false)
    {
        var builder = new DisplayListBuilder();
        PhotonRealizer.Realize(root, 200, 40, Theme, ThemeMode.Light, builder,
            timeMs: timeMs, reducedMotion: reducedMotion);
        return builder.Build().Commands.ToArray();
    }

    [Fact]
    public void GradientBox_EmitsPaintLinearAcrossTheBounds_ToRight()
    {
        var box = new Box(new BoxStyle
        {
            Width = 120,
            Height = 40,
            Gradient = new LinearGradient(Theme.SurfaceHighlight, new ColorToken(Color.Transparent)),
        });

        var fill = Render(box).Single(c => c.Kind == DrawCommandKind.FillRRect);

        fill.Paint.Kind.Should().Be(PaintKind.LinearGradient);
        fill.Paint.GradientStart.Should().Be(new Point(0, 0));
        fill.Paint.GradientEnd.Should().Be(new Point(120, 0), "ToRight spans the box width");
        fill.Paint.Color.Should().Be(Theme.SurfaceHighlight.Resolve(ThemeMode.Light));
        fill.Paint.EndColor.Should().Be(Color.Transparent);
    }

    [Fact]
    public void GradientBox_ToBottom_SpansTheHeight()
    {
        var box = new Box(new BoxStyle
        {
            Width = 120,
            Height = 40,
            Gradient = new LinearGradient(Theme.Scrim, new ColorToken(Color.Transparent), GradientDirection.ToBottom),
        });

        var fill = Render(box).Single(c => c.Kind == DrawCommandKind.FillRRect);
        fill.Paint.GradientEnd.Should().Be(new Point(0, 40));
    }

    [Fact]
    public void GradientOverSolid_DrawsBothFills_SolidFirst()
    {
        var box = new Box(new BoxStyle
        {
            Width = 120,
            Height = 40,
            Background = Theme.SurfaceSubtle,
            Gradient = new LinearGradient(new ColorToken(Color.Transparent), Theme.SurfaceHighlight),
        });

        var fills = Render(box).Where(c => c.Kind == DrawCommandKind.FillRRect).ToList();

        fills.Should().HaveCount(2, "the gradient composes OVER the solid (CSS background parity)");
        fills[0].Paint.Kind.Should().Be(PaintKind.Solid);
        fills[1].Paint.Kind.Should().Be(PaintKind.LinearGradient);
    }

    [Fact]
    public void SkeletonShimmer_SweepsTheSplitGlint_InsideTheClippingRRect()
    {
        // t=700 (half of 1400): the glint layer sits at translateX(0) — centered over the base.
        var commands = Render(new Skeleton(SkeletonShape.Line, 160), timeMs: 700);

        var fills = commands.Where(c => c.Kind == DrawCommandKind.FillRRect).ToList();
        fills.Should().HaveCount(3, "base + the two mirrored gradient halves");
        fills[0].Paint.Kind.Should().Be(PaintKind.Solid);
        fills[0].Clip.Should().BeNull("the base IS the clip shape");

        var halves = fills.Skip(1).ToList();
        halves.Should().AllSatisfy(half =>
        {
            half.Paint.Kind.Should().Be(PaintKind.LinearGradient);
            half.Clip.Should().NotBeNull("the glint confines to the placeholder rrect");
            half.Transform.M31.Should().Be(0, "half period = layer at rest position");
        });
        // Mirrored: first half fades IN (transparent → highlight), second fades OUT.
        halves[0].Paint.Color.A.Should().Be(0);
        halves[0].Paint.EndColor.A.Should().BeGreaterThan(0);
        halves[1].Paint.Color.A.Should().BeGreaterThan(0);
        halves[1].Paint.EndColor.A.Should().Be(0);
    }

    [Fact]
    public void SkeletonUnderReduceMotion_IsThePlainPlaceholder_AndStopsTheLoop()
    {
        var builder = new DisplayListBuilder();
        var frame = PhotonRealizer.Realize(new Skeleton(SkeletonShape.Line, 160), 200, 40, Theme,
            ThemeMode.Light, builder, timeMs: 700, reducedMotion: true);

        frame.HasActiveMotion.Should().BeFalse("a hidden decorative loop must not keep frames hot");
        var fills = builder.Build().Commands.ToArray().Where(c => c.Kind == DrawCommandKind.FillRRect).ToList();
        fills.Should().HaveCount(1, "HideAtRest removes the glint entirely — spec B16's static SurfaceSubtle");
        fills[0].Paint.Kind.Should().Be(PaintKind.Solid);
    }

    [Theory]
    [InlineData(ThemeMode.Light, "skeleton-shimmer-light")]
    [InlineData(ThemeMode.Dark, "skeleton-shimmer-dark")]
    public void SkeletonShimmer_GoldenAtHalfPeriod(ThemeMode mode, string golden)
    {
        var column = new Column(gap: Space.S2) { Padding = EdgeInsets.All(Space.S2) };
        column.Add(new Skeleton(SkeletonShape.Line, 208));
        var row = new Row(gap: Space.S2);
        row.Add(new Skeleton(SkeletonShape.Circle, 32));
        row.Add(new Skeleton(SkeletonShape.Block, 168, 32));
        column.Add(row);

        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(240, 76);
        var host = new PhotonHost(column, PhotonTheme.Instance, mode, 240, 76);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, timeMs: 700);
        backend.Render(builder.Build(), surface);

        GoldenImage.Match(surface, golden);
    }
}
