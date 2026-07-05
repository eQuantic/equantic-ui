using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The wave-1 write-once components rendered as Photon pixels — the SAME classes the web suite
/// lowers to DOM. One gallery golden per mode.
/// </summary>
public class Wave1GoldenTests
{
    private static VisualNode Gallery()
    {
        var chips = new Row(gap: Space.S2);
        chips.Add(new Chip("All", ChipKind.Filter, selected: true, onPressed: () => { }));
        chips.Add(new Chip("Income", ChipKind.Filter, onPressed: () => { }));
        chips.Add(new Chip("Beta", ChipKind.Tag) { Variant = Variant.Info });

        var badges = new Row(gap: Space.S3) { Cross = CrossAlign.Center };
        badges.Add(new Avatar("AB", SizeVariant.Large, name: "Ana Beatriz"));
        badges.Add(new Avatar("TK", SizeVariant.Medium, name: "Tomás K"));
        badges.Add(new Badge(3));
        badges.Add(new Badge(120));
        badges.Add(new Badge(7) { Neutral = true });
        badges.Add(Badge.AsDot());

        var column = new Column(gap: Space.S3)
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S4),
        };
        column.Add(new Banner(Variant.Warning, "Your card expires this month.", "Renew it to keep automatic payments running."));
        column.Add(badges);
        column.Add(new Divider());
        column.Add(chips);
        column.Add(new ProgressBar(0.64f));
        column.Add(new ProgressBar(0.4f, Variant.Success) { Prominent = true });

        return column;
    }

    [Theory]
    [InlineData(ThemeMode.Light, "wave1-gallery-light")]
    [InlineData(ThemeMode.Dark, "wave1-gallery-dark")]
    public void Wave1Gallery_RendersThePhotonPixels(ThemeMode mode, string golden)
    {
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(360, 300);

        var host = new PhotonHost(Gallery(), PhotonTheme.Instance, mode, 360, 300);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        backend.Render(builder.Build(), surface);

        GoldenImage.Match(surface, golden);
    }
}
