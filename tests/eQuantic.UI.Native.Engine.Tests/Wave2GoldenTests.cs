using eQuantic.UI.Components.Shared;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Primitives;
using List = eQuantic.UI.Components.Shared.List;
using Switch = eQuantic.UI.Components.Shared.Switch;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>The wave-2 batch rendered as Photon pixels — the SAME classes the web suite lowers.</summary>
public class Wave2GoldenTests
{
    private static VisualNode Gallery()
    {
        var column = new Column(gap: Space.S3)
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S4),
        };

        column.Add(new Tabs(["Overview", "Holdings", "Activity"], selected: 0));

        var iconRow = new Row(gap: Space.S2) { Cross = CrossAlign.Center };
        iconRow.Add(new IconButton(Icons.Search, "Search"));
        iconRow.Add(new IconButton(Icons.Heart, "Favorite", IconButtonKind.Tonal) { Selected = true, SelectedGlyph = Icons.HeartFilled });
        iconRow.Add(new IconButton(Icons.Mail, "Mail", IconButtonKind.Filled));
        iconRow.Add(new IconButton(Icons.Close, "Close", IconButtonKind.Outline));
        column.Add(iconRow);

        var forms = new Row(gap: Space.S5) { Cross = CrossAlign.Center };
        forms.Add(new Checkbox(true, label: "Agree"));
        forms.Add(new Checkbox(false, label: "News"));
        forms.Add(new Switch(true));
        forms.Add(new Switch(false));
        column.Add(forms);

        column.Add(new RadioGroup(["Weekly", "Monthly"], selected: 1, label: "Frequency"));

        column.Add(new List(
        [
            new ListItem("Wi-Fi", "Photon-5G", onPressed: () => { })
            {
                Leading = new Icon(Icons.Notifications, IconSize.Md),
                Trailing = new Icon(Icons.ChevronRight, IconSize.Dense),
            },
            new ListItem("Notifications") { Trailing = new Switch(true) },
        ]));

        return column;
    }

    [Theory]
    [InlineData(ThemeMode.Light, "wave2-gallery-light")]
    [InlineData(ThemeMode.Dark, "wave2-gallery-dark")]
    public void Wave2Gallery_RendersThePhotonPixels(ThemeMode mode, string golden)
    {
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(380, 460);

        var host = new PhotonHost(Gallery(), PhotonTheme.Instance, mode, 380, 460);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        backend.Render(builder.Build(), surface);

        GoldenImage.Match(surface, golden);
    }
}
