using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Spec S1 rendered to Photon pixels: group opacity over OVERLAPPING children (the case per-command
/// alpha gets wrong), a center-anchored rotated card, aspect-ratio driving the underdetermined axis,
/// and align-self overriding the row's cross alignment.
/// </summary>
public class S1StyleGoldenTests
{
    private static VisualNode Gallery()
    {
        var column = new Column(gap: Space.S3) { Padding = EdgeInsets.All(Space.S3), Width = SizeValue.Fill };

        // Group opacity: two overlapping filled boxes inside ONE 50% layer — a single composite.
        var overlap = new Stack();
        overlap.Add(new Box(new BoxStyle
        {
            Width = 120, Height = 48, Background = new ColorToken(Color.FromRgb(0x0B, 0x5F, 0xB0)),
            CornerRadius = new CornerRadii(Radius.Md),
        }));
        overlap.Add(new Positioned(new Box(new BoxStyle
        {
            Width = 120, Height = 48, Background = new ColorToken(Color.FromRgb(0xB0, 0x2B, 0x0B)),
            CornerRadius = new CornerRadii(Radius.Md),
        }), top: 16, start: 60));
        column.Add(new Box(new BoxStyle { Opacity = 0.5f }, overlap));

        // Static transform: a rotated, slightly scaled badge (paint-only — layout box unchanged).
        column.Add(new Box(new BoxStyle
        {
            Width = 140, Height = 40,
            Background = new ColorToken(Color.FromRgb(0x2E, 0x7D, 0x32)),
            CornerRadius = new CornerRadii(Radius.Sm),
            Transform = Transform2D.Rotate(8).WithScale(1.05f),
        }));

        // Aspect ratio: width fills, height derives (16:9).
        column.Add(new Box(new BoxStyle
        {
            Width = SizeValue.Fill, AspectRatio = 16f / 9f,
            Background = new ColorToken(Color.FromRgb(0x54, 0x3A, 0x8C)),
            CornerRadius = new CornerRadii(Radius.Lg),
        }));

        // Align-self: middle chip overrides the row's End alignment with Start.
        var row = new Row(gap: Space.S2) { Cross = CrossAlign.End, Height = 56 };
        row.Add(new Box(new BoxStyle { Width = 40, Height = 20, Background = new ColorToken(Color.FromRgb(0x88, 0x88, 0x88)) }));
        row.Add(new Box(new BoxStyle { Width = 40, Height = 20, Background = new ColorToken(Color.FromRgb(0xC5, 0x8A, 0x12)) }) { AlignSelf = CrossAlign.Start });
        row.Add(new Box(new BoxStyle { Width = 40, Height = 20, Background = new ColorToken(Color.FromRgb(0x88, 0x88, 0x88)) }));
        column.Add(row);

        return column;
    }

    [Theory]
    [InlineData(ThemeMode.Light, "s1-style-light")]
    [InlineData(ThemeMode.Dark, "s1-style-dark")]
    public void S1Gallery_RendersThePhotonPixels(ThemeMode mode, string golden)
    {
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(320, 340);
        var host = new PhotonHost(Gallery(), PhotonTheme.Instance, mode, 320, 340);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        backend.Render(builder.Build(), surface);

        GoldenImage.Match(surface, golden);
    }
}
