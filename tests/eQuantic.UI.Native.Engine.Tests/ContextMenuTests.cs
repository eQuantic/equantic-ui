using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The ready-made context menu on Photon (#1531): opened where the pointer asked, at the child's edge when the
/// keyboard did, and then the Menu's own panel, keys and choice.
/// </summary>
public class ContextMenuTests
{
    private sealed class Page : StatefulComponent
    {
        public List<int> Chosen { get; } = [];

        public override VisualNode Build(ComponentContext context) =>
            new Column(gap: 0)
            {
                new ContextMenu(
                    new Pressable(new Box(new BoxStyle { Width = 300, Height = 40, Background = context.Theme.Surface }), () => { }),
                    [new MenuItem("Rename"), new MenuItem("Duplicate") { Disabled = true }, new MenuItem("Delete") { Destructive = true }],
                    index => Chosen.Add(index)),
            };
    }

    private static (PhotonHost Host, Page Page) Open()
    {
        var page = new Page();
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 500, 500);
        host.RenderFrame(new DisplayListBuilder(), 0);
        host.RenderFrame(new DisplayListBuilder(), 16);
        return (host, page);
    }

    private static IEnumerable<LayoutNode> Walk(LayoutNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var below in Walk(child))
                yield return below;
    }

    private static Rect Target(PhotonHost host) =>
        Walk(host.LastFrame!.Root).First(n => n.Source is ContextTarget).Bounds;

    /// <summary>The open menu's panel: the box an overlay layer holds, the one whose rows are menu items.</summary>
    private static Rect? Panel(PhotonHost host) =>
        host.LastFrame!.OverlayRoots.SelectMany(Walk)
            .FirstOrDefault(n => n.Source is Box && Walk(n).Any(c => c.Source is Pressable { Role: PressableRole.MenuItem }))
            ?.Bounds;

    [Fact]
    public void ASecondaryClick_OpensTheMenu_WhereThePointerAsked()
    {
        var (host, _) = Open();
        var target = Target(host);
        Panel(host).Should().BeNull("the menu is closed");

        host.ContextRequest(target.X + 120, target.Y + 30);
        host.RenderFrame(new DisplayListBuilder(), 32);

        var panel = Panel(host);
        panel.Should().NotBeNull();
        panel!.Value.X.Should().BeApproximately(target.X + 120 + Anchored.DefaultGap, 0.5f);
        panel.Value.Y.Should().BeApproximately(target.Y + 30 + Anchored.DefaultGap, 0.5f);
    }

    [Fact]
    public void TheMenuKey_OpensItUnderTheChild_AndTheKeysChooseLikeAMenusDo()
    {
        var (host, page) = Open();
        host.KeyDown("Tab");
        host.RenderFrame(new DisplayListBuilder(), 32);
        var target = Target(host);

        host.KeyDown("ContextMenu").Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder(), 48);
        var panel = Panel(host);
        panel.Should().NotBeNull();
        panel!.Value.Y.Should().BeApproximately(target.Bottom + Anchored.DefaultGap, 0.5f, "the keyboard asked: the child's bottom edge");

        host.KeyDown("ArrowDown").Should().BeTrue("the disabled item is stepped over");
        host.RenderFrame(new DisplayListBuilder(), 64);
        host.KeyDown("Enter").Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder(), 80);

        page.Chosen.Should().Equal(2);
        Panel(host).Should().BeNull("choosing closes it");
    }
}
