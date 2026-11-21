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
        frame.LinkRegions[0].Destination.Should().Be("/users/2");
        frame.LinkRegions[1].Destination.Should().Be("/detail");
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

    private sealed class Launcher : IUrlLauncher
    {
        public List<Uri> Opened { get; } = [];
        public ValueTask<bool> OpenUrlAsync(Uri url) { Opened.Add(url); return ValueTask.FromResult(true); }
        public bool CanOpen(Uri url) => true;
    }

    /// <summary>A link that opens beside the page (#941): an absolute address goes to the system's browser,
    /// and the app's own screen stays where it is; an app route, with no second page to open, navigates.
    /// Both ways a person follows a link say the same: the pointer's tap and the activation a keyboard or a
    /// screen reader sends by id.</summary>
    [Theory]
    [InlineData("https://example.com/terms", true, false)]
    [InlineData("/terms", false, false)]
    [InlineData("https://example.com/terms", true, true)]
    [InlineData("/terms", false, true)]
    public void ALinkThatOpensBeside_HandsAnAddressToTheSystem_AndNavigatesARoute(
        string destination, bool goesToTheSystem, bool byActivation)
    {
        var column = new Column(gap: 0) { Width = SizeValue.Fill };
        column.Add(new Link(destination, new Text("Terms", TypeRole.Label)) { OpensBeside = true });
        var host = new PhotonHost(column, PhotonTheme.Instance, ThemeMode.Light, 360, 300);
        var frame = host.RenderFrame(new DisplayListBuilder());
        string? navigated = null;
        host.NavigationRequested = href => navigated = href;
        var launcher = new Launcher();

        using (CapabilityScope.With<IUrlLauncher>(launcher))
        {
            var region = frame.LinkRegions.Single();
            region.OpensBeside.Should().BeTrue();
            if (byActivation)
            {
                host.Activate(region.Id).Should().BeTrue();
            }
            else
            {
                host.PressDown(region.Bounds.Center.X, region.Bounds.Center.Y);
                host.PressUp(region.Bounds.Center.X, region.Bounds.Center.Y);
            }
        }

        launcher.Opened.Select(url => url.AbsoluteUri).Should().Equal(goesToTheSystem ? [destination] : []);
        navigated.Should().Be(goesToTheSystem ? null : destination);
    }
}
