using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Waves 3/3b on Photon pixels — the visual debt closes: anchored panels (Select/Menu open via the
/// press pipeline, Tooltip open via the hover pipeline) and the static trio (Drawer, Accordion,
/// Table), light and dark. Interactions run through the HOST between frames, so every golden pins
/// the same machinery the window ships.
/// </summary>
public class Wave3GoldenTests
{
    [Theory]
    [InlineData(ThemeMode.Light, "select-open-light")]
    [InlineData(ThemeMode.Dark, "select-open-dark")]
    public void SelectOpen_RendersThePhotonPixels(ThemeMode mode, string golden)
    {
        var root = Page(new Select(new[] { "Small", "Medium", "Large" }, selectedIndex: 1));
        Render(root, mode, golden, (host, frame) => Tap(host, frame.HitRegions[0].Bounds));
    }

    [Theory]
    [InlineData(ThemeMode.Light, "menu-open-light")]
    [InlineData(ThemeMode.Dark, "menu-open-dark")]
    public void MenuOpen_RendersThePhotonPixels(ThemeMode mode, string golden)
    {
        var menu = new Menu(new Text("Actions", TypeRole.Label), new[]
        {
            new MenuItem("Rename") { Icon = Icons.Person },
            new MenuItem("Share"),
            new MenuItem("Delete") { Icon = Icons.Close, Destructive = true },
        });
        Render(Page(menu), mode, golden, (host, frame) => Tap(host, frame.HitRegions[0].Bounds));
    }

    [Theory]
    [InlineData(ThemeMode.Light, "tooltip-open-light")]
    [InlineData(ThemeMode.Dark, "tooltip-open-dark")]
    public void TooltipOpen_RendersThePhotonPixels(ThemeMode mode, string golden)
    {
        var tooltip = new Tooltip(new Button("Save draft"), "Stores a private copy");
        Render(Page(new Box(new BoxStyle { Padding = EdgeInsets.All(Space.S8) }, tooltip)), mode, golden,
            (host, frame) =>
            {
                var region = frame.HoverRegions[0].Bounds;
                host.PointerMove(region.Center.X, region.Center.Y);
            });
    }

    [Theory]
    [InlineData(ThemeMode.Light, "drawer-light")]
    [InlineData(ThemeMode.Dark, "drawer-dark")]
    public void Drawer_RendersThePhotonPixels(ThemeMode mode, string golden)
    {
        var nav = new Column(gap: Space.S2);
        nav.Add(new Text("Navigation", TypeRole.Title));
        nav.Add(new ListItem("Home", "Start page"));
        nav.Add(new ListItem("Reports", "Weekly numbers"));
        Render(Page(new Drawer(nav, open: true, onDismiss: () => { }) { Width = 240 }), mode, golden);
    }

    [Theory]
    [InlineData(ThemeMode.Light, "accordion-light")]
    [InlineData(ThemeMode.Dark, "accordion-dark")]
    public void Accordion_RendersThePhotonPixels(ThemeMode mode, string golden)
    {
        var accordion = new Accordion(new[]
        {
            new AccordionItem("General") { Content = new Text("Workspace defaults live here.", TypeRole.BodyM) },
            new AccordionItem("Advanced") { Content = new Text("Danger zone.", TypeRole.BodyM) },
        }, openIndex: 0);
        Render(Page(accordion), mode, golden);
    }

    [Theory]
    [InlineData(ThemeMode.Light, "table-light")]
    [InlineData(ThemeMode.Dark, "table-dark")]
    public void Table_RendersThePhotonPixels(ThemeMode mode, string golden)
    {
        var table = new Table(
            new[] { "Name", "Role", "Status" },
            new[]
            {
                new[] { "Ana", "Admin", "Active" },
                new[] { "João", "Editor", "Away" },
            });
        Render(Page(table), mode, golden);
    }

    private static VisualNode Page(VisualNode content)
    {
        var column = new Column(gap: Space.S3) { Padding = EdgeInsets.All(Space.S3), Width = SizeValue.Fill };
        column.Add(new Text("Content behind", TypeRole.Caption));
        column.Add(content);
        return column;
    }

    private static void Tap(PhotonHost host, Rect bounds)
    {
        host.PressDown(bounds.Center.X, bounds.Center.Y);
        host.PressUp(bounds.Center.X, bounds.Center.Y);
    }

    private static void Render(VisualNode root, ThemeMode mode, string golden,
        Action<PhotonHost, RealizeResult>? interact = null)
    {
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(360, 400);
        var host = new PhotonHost(root, PhotonTheme.Instance, mode, 360, 400);
        var first = host.RenderFrame(new DisplayListBuilder());
        interact?.Invoke(host, first);
        // Settle past Motion.Base so entrances leave no residue in the pinned pixels.
        host.RenderFrame(new DisplayListBuilder(), timeMs: 500);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, timeMs: 1000);
        backend.Render(builder.Build(), surface);
        GoldenImage.Match(surface, golden);
    }
}
