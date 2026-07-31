using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The write-once <see cref="Link"/> on Photon: pure navigation semantics — the child owns visuals,
/// the region registers, and a tap no pressable claims resolves through the HOST's navigation seam
/// (<see cref="PhotonHost.NavigationRequested"/> — where the platform shell maps hrefs to pages).
/// A Pressable INSIDE a link wins the tap, exactly like a button inside an anchor.
/// </summary>
public class LinkTests
{
    private static VisualNode Page()
    {
        var column = new Column(gap: Space.S3) { Width = SizeValue.Fill, Padding = EdgeInsets.All(Space.S4) };
        column.Add(new Link("/users/2", new Text("Open user", TypeRole.Label)));

        var inner = new Row(gap: Space.S2);
        inner.Add(new Text("Card", TypeRole.BodyM));
        inner.Add(new Button("Act"));
        column.Add(new Link("/detail", new Card(inner, CardKind.Outlined) { Width = SizeValue.Fill }));
        return column;
    }

    [Fact]
    public void Links_RegisterRegions_InPaintOrder()
    {
        var host = new PhotonHost(Page(), PhotonTheme.Instance, ThemeMode.Light, 360, 300);
        var frame = host.RenderFrame(new DisplayListBuilder());

        frame.LinkRegions.Should().HaveCount(2);
        frame.LinkRegions[0].Node.Href.Should().Be("/users/2");
        frame.LinkRegions[1].Node.Href.Should().Be("/detail");
    }

    [Fact]
    public void Tap_OnALink_ResolvesThroughTheNavigationSeam()
    {
        var host = new PhotonHost(Page(), PhotonTheme.Instance, ThemeMode.Light, 360, 300);
        var frame = host.RenderFrame(new DisplayListBuilder());
        string? navigated = null;
        host.NavigationRequested = href => navigated = href;

        var region = frame.LinkRegions[0];
        host.PressDown(region.Bounds.Center.X, region.Bounds.Center.Y);
        host.PressUp(region.Bounds.Center.X, region.Bounds.Center.Y);

        navigated.Should().Be("/users/2", "a tap no pressable claims navigates the topmost link");
    }

    [Fact]
    public void PressableInsideALink_WinsTheTap()
    {
        var host = new PhotonHost(Page(), PhotonTheme.Instance, ThemeMode.Light, 360, 300);
        var frame = host.RenderFrame(new DisplayListBuilder());
        string? navigated = null;
        host.NavigationRequested = href => navigated = href;

        var button = frame.HitRegions.Single(r => r.Node.Label == "Act");
        host.PressDown(button.Bounds.Center.X, button.Bounds.Center.Y);
        host.PressUp(button.Bounds.Center.X, button.Bounds.Center.Y);

        navigated.Should().BeNull("the button inside the link claims the tap — anchor semantics");
    }

    [Fact]
    public void WithoutTheSeam_LinksAreInert()
    {
        var host = new PhotonHost(Page(), PhotonTheme.Instance, ThemeMode.Light, 360, 300);
        var frame = host.RenderFrame(new DisplayListBuilder());

        var region = frame.LinkRegions[0];
        host.PressDown(region.Bounds.Center.X, region.Bounds.Center.Y);
        host.PressUp(region.Bounds.Center.X, region.Bounds.Center.Y)
            .Should().BeFalse("no seam registered — the tap falls through");
    }
}
