using eQuantic.UI.Components;
using eQuantic.UI.Material;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Material Design 3 as a write-once theme, rendered to PHOTON PIXELS: the SAME shared components the
/// Photon goldens use, realized with <see cref="MaterialTheme"/> — M3 purple primary, M3 role
/// containers, and the M3 shape scale (12/16/28 vs Photon's 10/14/20). Swapping the theme is the
/// whole change; no component knows Material exists.
/// </summary>
public class MaterialThemeGoldenTests
{
    private static VisualNode Gallery()
    {
        var column = new Column(gap: Space.S3) { Padding = EdgeInsets.All(Space.S3), Width = SizeValue.Fill };

        var buttons = new Row(gap: Space.S2) { Cross = CrossAlign.Center };
        buttons.Add(new Button("Filled"));
        buttons.Add(new Button("Tertiary", Variant.Tertiary));
        buttons.Add(new Button("Text", Variant.Link));
        column.Add(buttons);

        var chips = new Row(gap: Space.S2);
        chips.Add(new Chip("All", ChipKind.Filter, selected: true));
        chips.Add(new Chip("Beta", ChipKind.Tag) { Variant = Variant.Tertiary });
        column.Add(chips);

        column.Add(new ProgressBar(0.6f));
        column.Add(new Card(new Text("Material card", TypeRole.Title)) { Width = SizeValue.Fill });
        return column;
    }

    [Fact]
    public void Material_IsADistinctThemeThroughTheSameContract()
    {
        MaterialTheme.Instance.Colors(Variant.Primary).Base.Resolve(ThemeMode.Light)
            .Should().Be(Color.FromRgb(0x67, 0x50, 0xA4), "M3 baseline primary");
        MaterialTheme.Instance.Shape(ShapeScale.Large).Should().Be(16, "M3 large shape");
        MaterialTheme.Instance.Shape(ShapeScale.Large).Should()
            .NotBe(PhotonTheme.Instance.Shape(ShapeScale.Large), "the ladders differ (M3 16 vs Photon 14)");
    }

    [Theory]
    [InlineData(ThemeMode.Light, "material-gallery-light")]
    [InlineData(ThemeMode.Dark, "material-gallery-dark")]
    public void MaterialGallery_RendersThePhotonPixels(ThemeMode mode, string golden)
    {
        Render(MaterialTheme.Instance, mode, golden);
    }

    [Theory]
    [InlineData(ThemeMode.Light, "material-seed-blue-light")]
    [InlineData(ThemeMode.Dark, "material-seed-blue-dark")]
    public void MaterialDynamicColor_FromASeed_RendersThePhotonPixels(ThemeMode mode, string golden)
    {
        // DYNAMIC COLOR through the same realizer: a whole M3 theme derived from the eQuantic-blue seed
        // #0050A0 via HCT tonal palettes, rendered to pixels. The purple baseline gallery becomes blue
        // with nothing but a different IAppTheme — the exact surface a developer brands the framework by.
        Render(MaterialTheme.FromSeed(Color.FromRgb(0x00, 0x50, 0xA0)), mode, golden);
    }

    private static void Render(IAppTheme theme, ThemeMode mode, string golden)
    {
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(360, 320);
        var host = new PhotonHost(Gallery(), theme, mode, 360, 320);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        backend.Render(builder.Build(), surface);

        GoldenImage.Match(surface, golden);
    }
}
