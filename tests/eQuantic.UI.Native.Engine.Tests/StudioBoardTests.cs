using eQuantic.Studio;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The desktop sample's board (#1537), moved through its host: the gallery's Board section is the web
/// dashboard's <c>/board</c> on Photon, and a card a mouse carries to another column is built there next.
/// </summary>
public class StudioBoardTests
{
    private const float Width = 1180;
    private const float Height = 820;

    private static PhotonHost OpenBoard()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["section"] = GallerySection.Board.ToString() })
            .Build();
        var host = new PhotonHost(new StudioShell(configuration), PhotonTheme.Instance, ThemeMode.Light, Width, Height);
        host.RenderFrame(new DisplayListBuilder(), 0);
        host.RenderFrame(new DisplayListBuilder(), 16);
        return host;
    }

    private static IEnumerable<LayoutNode> Walk(LayoutNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var below in Walk(child))
                yield return below;
    }

    private static LayoutNode ListOf(PhotonHost host, string column) =>
        Walk(host.LastFrame!.Root).First(n => n.Source is Sortable { } list && list.Id == column);

    private static string[] CardsIn(PhotonHost host, string column) =>
        Walk(ListOf(host, column)).Select(n => n.Source).OfType<SortableItem>().Select(item => item.Id).ToArray();

    [Fact]
    public void ACardCarriedToTheNextColumn_IsBuiltThere()
    {
        var host = OpenBoard();
        CardsIn(host, "Doing").Should().Equal("c4");
        var card = Walk(ListOf(host, "To do")).First(n => n.Source is SortableItem { Id: "c1" }).Bounds.Center;
        var doing = ListOf(host, "Doing").Bounds;

        host.PressDown(card.X, card.Y);
        host.PointerMove(card.X + 10, card.Y);
        host.RenderFrame(new DisplayListBuilder(), 32);
        // Below the column's one card: it lands after it.
        host.PointerMove(doing.Center.X, doing.Bottom - 10);
        host.RenderFrame(new DisplayListBuilder(), 48);
        host.PressUp(doing.Center.X, doing.Bottom - 10);
        host.RenderFrame(new DisplayListBuilder(), 64);
        host.RenderFrame(new DisplayListBuilder(), 80);

        CardsIn(host, "Doing").Should().Equal("c4", "c1");
        CardsIn(host, "To do").Should().Equal("c2", "c3");
    }

    [Fact]
    public void APressThatDoesNotTravel_OpensTheCard()
    {
        var host = OpenBoard();
        var card = Walk(ListOf(host, "To do")).First(n => n.Source is SortableItem { Id: "c2" }).Bounds.Center;

        host.PressDown(card.X, card.Y);
        host.PressUp(card.X, card.Y);
        host.RenderFrame(new DisplayListBuilder(), 32);

        Walk(host.LastFrame!.Root).Select(n => n.Source).OfType<Text>()
            .Should().Contain(text => text.Content == "Opened: Sort the lists");
        CardsIn(host, "To do").Should().Equal("c1", "c2", "c3");
    }
}
