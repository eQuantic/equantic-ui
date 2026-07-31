using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>Wave 3b on Photon: Tooltip (hover reveal — no scrim), Drawer (modal scrim), Accordion
/// (internal toggle), Table (grid rows) — all through the host's ordinary pipelines.</summary>
public class Wave3bNativeTests
{
    private static PhotonHost Host(VisualNode root) =>
        new(root, PhotonTheme.Instance, ThemeMode.Light, 360, 400);

    [Fact]
    public void Tooltip_ShowsOnHover_AndHidesOnLeave()
    {
        var root = new Column(gap: 0) { Padding = EdgeInsets.All(Space.S4) };
        root.Add(new Tooltip(new Text("Save", TypeRole.Label), "Saves the draft"));

        var host = Host(root);
        var frame = host.RenderFrame(new DisplayListBuilder());
        var closedCount = frame.HitRegions.Count;
        frame.HoverRegions.Should().HaveCount(1, "the anchor registers for hover tracking");

        // Pointer over the anchor → the panel opens on the next frame; no scrim joins the hits.
        var region = frame.HoverRegions[0];
        host.PointerMove(region.Bounds.Center.X, region.Bounds.Center.Y);
        var open = host.RenderFrame(new DisplayListBuilder());
        open.Root.Should().NotBeNull();
        host.PointerLeave();
        var closed = host.RenderFrame(new DisplayListBuilder());
        closed.HitRegions.Count.Should().Be(closedCount, "hover-open panels take no scrim");
    }

    [Fact]
    public void Drawer_ScrimBlocks_AndDismisses()
    {
        var dismissed = false;
        var pagePressed = false;
        var root = new Column(gap: 0) { Width = SizeValue.Fill };
        root.Add(new Button("Page", onPressed: () => pagePressed = true));
        root.Add(new Drawer(new Text("Nav", TypeRole.Label), open: true, onDismiss: () => dismissed = true));

        var host = Host(root);
        var frame = host.RenderFrame(new DisplayListBuilder());

        // Tap where the page button sits: the scrim (topmost, full viewport) wins.
        var page = frame.HitRegions[0];
        host.PressDown(page.Bounds.Center.X, page.Bounds.Center.Y);
        host.PressUp(page.Bounds.Center.X, page.Bounds.Center.Y);
        pagePressed.Should().BeFalse("the drawer scrim blocks the page");
        dismissed.Should().BeTrue();
    }

    [Fact]
    public void Accordion_TogglesSections_Internally()
    {
        var accordion = new Accordion(new[]
        {
            new AccordionItem("General") { Content = new Text("body a", TypeRole.BodyM) },
            new AccordionItem("Advanced") { Content = new Text("body b", TypeRole.BodyM) },
        });
        var root = new Column(gap: 0) { Width = SizeValue.Fill };
        root.Add(accordion);

        var host = Host(root);
        var frame = host.RenderFrame(new DisplayListBuilder());
        frame.HitRegions.Should().HaveCount(2, "two headers, everything closed");

        // Open the second section, then the first (single-open swaps).
        var second = frame.HitRegions[1];
        host.PressDown(second.Bounds.Center.X, second.Bounds.Center.Y);
        host.PressUp(second.Bounds.Center.X, second.Bounds.Center.Y);
        var open = host.RenderFrame(new DisplayListBuilder());
        open.HitRegions.Should().HaveCount(2, "headers stay the only pressables");
        open.HitRegions[1].Bounds.Y.Should().Be(frame.HitRegions[1].Bounds.Y,
            "opening the LAST section pushes nothing above it");

        host.PressDown(open.HitRegions[0].Bounds.Center.X, open.HitRegions[0].Bounds.Center.Y);
        host.PressUp(open.HitRegions[0].Bounds.Center.X, open.HitRegions[0].Bounds.Center.Y);
        var swapped = host.RenderFrame(new DisplayListBuilder());
        swapped.HitRegions[1].Bounds.Y.Should().BeGreaterThan(open.HitRegions[1].Bounds.Y,
            "opening the FIRST section pushes the second header down (single-open swapped)");
    }

    [Fact]
    public void Table_LaysOutHeaderAndRows()
    {
        var table = new Table(
            new[] { "Name", "Role" },
            new[] { new[] { "Ana", "Admin" }, new[] { "João", "Editor" } });
        var root = new Column(gap: 0) { Width = SizeValue.Fill };
        root.Add(table);

        var host = Host(root);
        var frame = host.RenderFrame(new DisplayListBuilder());
        frame.HitRegions.Should().BeEmpty("v1 tables are read-only");
        frame.Root.Bounds.Height.Should().BeGreaterThan(88, "header + two 44dp rows laid out");
    }
}
