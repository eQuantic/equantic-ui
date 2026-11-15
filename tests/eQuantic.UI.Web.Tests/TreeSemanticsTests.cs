using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Testing;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A TREE on the web (#1529, #1530), the server's half: what the realizer writes, and what a reader
/// hears through <see cref="ComponentTester"/>. Three facts are asserted because each is a way to ship a
/// broken tree: an item that is a button (ARIA in HTML refuses <c>treeitem</c> on one); a tree whose
/// items sit in rows (a tree must not hold rows); and items that do not say how deep they stand, which a
/// flat list of visible rows cannot let a reader infer. The browser's twin is <c>tree-view.spec.ts</c>,
/// Photon's <c>TreeViewTests</c>.
/// </summary>
public class TreeSemanticsTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static TreeNode Leaf(string key, string label) => new(key, label) { IsLeaf = true };

    private static TreeView Files(string? selected = null, Action<TreeNode>? onPressed = null) =>
        new([
            new TreeNode("src", "src") { Children = [Leaf("src/App.cs", "App.cs"), Leaf("src/Ui.cs", "Ui.cs")] },
            Leaf("README.md", "README.md"),
        ], selected, onPressed)
        {
            Label = "Files",
            ExpandedKeys = ["src"],
        };

    [Fact]
    public void ATreeIsReadAsATree_ItsItemsWithTheirLevelPlaceAndState()
    {
        using var tester = new ComponentTester(Theme);
        tester.Pump(Files(selected: "src/Ui.cs"));

        var tree = tester.Semantics.Should().ContainSingle(stop => stop.Role == SemanticRole.Tree).Which;
        tree.Name.Should().Be("Files");
        tester.Semantics.Where(stop => stop.Role == SemanticRole.TreeItem)
            .Select(item => (item.Name, item.TreePlace, item.Expanded, item.Selected))
            .Should().Equal(
                ("src", new TreePlace(1, 1, 2), true, false),
                ("App.cs", new TreePlace(2, 1, 2), (bool?)null, false),
                ("Ui.cs", new TreePlace(2, 2, 2), (bool?)null, true),
                ("README.md", new TreePlace(1, 2, 2), (bool?)null, (bool?)false));
    }

    [Fact]
    public void APressReportsTheNode_AndOpensOrClosesAParent()
    {
        var pressed = new List<string>();
        using var tester = new ComponentTester(Theme);
        tester.Pump(Files(onPressed: node => pressed.Add(node.Key)));

        tester.Tap(SemanticRole.TreeItem, "src");
        tester.Pump();
        pressed.Should().Equal("src");
        tester.Find(SemanticRole.TreeItem, "src").Expanded.Should().BeFalse("src was open, and the press closed it");
        tester.Semantics.Should().NotContain(stop => stop.Name == "App.cs");

        tester.Tap(SemanticRole.TreeItem, "README.md");
        tester.Pump();
        pressed.Should().Equal("src", "README.md");
    }

    [Fact]
    public void ATreeIsOneStopOfDivItems_NotWrappedInRows_AndItsActiveItemResolves()
    {
        var items = new List<VisualNode>
        {
            new Pressable(new Text("src"), () => { })
            {
                Role = PressableRole.TreeItem, Expanded = true, Selected = false, TreePlace = new TreePlace(1, 1, 1),
            },
            new Pressable(new Text("App.cs"), () => { })
            {
                Role = PressableRole.TreeItem, Selected = true, TreePlace = new TreePlace(2, 1, 1),
            },
        };
        var host = WebRealizer.Lower(new Navigable(_ => { }, items)
        {
            Role = NavigableRole.Tree,
            Label = "Files",
            ActiveCell = (1, 0),
        }, Theme).Render();

        host.Attributes["role"].Should().Be("tree");
        host.Attributes["tabindex"].Should().Be("0");
        Walk(host).Should().NotContain(node => node.Attributes.GetValueOrDefault("role") == "row",
            "a tree must not hold rows");

        var treeItems = Walk(host).Where(node => node.Attributes.GetValueOrDefault("role") == "treeitem").ToList();
        treeItems.Should().HaveCount(2);
        treeItems.Should().OnlyContain(node => node.Tag == "div", "ARIA in HTML refuses role=treeitem on a button");
        treeItems.Should().OnlyContain(node => node.Attributes["tabindex"] == "-1", "the tree is the one stop");
        treeItems[1].Attributes["aria-level"].Should().Be("2");
        treeItems[1].Attributes["aria-posinset"].Should().Be("1");
        treeItems[1].Attributes["aria-setsize"].Should().Be("1");
        treeItems[1].Attributes["aria-selected"].Should().Be("true");
        treeItems[0].Attributes["aria-expanded"].Should().Be("true");

        var active = host.Attributes["aria-activedescendant"];
        treeItems[1].Attributes["id"].Should().Be(active, "the active item is the one the host names");
    }

    /// <summary>
    /// The server draws a page once and keeps none of it, so a folder the page opens is not loaded there:
    /// its loading item is drawn, and the browser's tree, which is mounted, loads it.
    /// </summary>
    [Fact]
    public async Task TheServersRenderLoadsNothing()
    {
        var loads = 0;
        var tree = new TreeView([new TreeNode("docs", "docs")])
        {
            ExpandedKeys = ["docs"],
            LoadChildren = _ =>
            {
                loads++;
                return Task.FromResult<IReadOnlyList<TreeNode>>([Leaf("docs/intro.md", "intro.md")]);
            },
        };
        var host = WebRealizer.Lower(tree, Theme).Render();
        await Task.Delay(100);

        loads.Should().Be(0);
        Walk(host).Should().Contain(node => node.Attributes.GetValueOrDefault("aria-label") == SdkStrings.TreeLoading);
    }

    [Fact]
    public void ADisabledTreeItemSaysSo_ForADivCannotBeDisabled()
    {
        var host = WebRealizer.Lower(new Navigable(_ => { }, [
            new Pressable(new Text("locked"), () => { })
            {
                Role = PressableRole.TreeItem, Disabled = true, TreePlace = new TreePlace(1, 1, 1),
            },
        ]) { Role = NavigableRole.Tree }, Theme).Render();

        var item = Walk(host).Single(node => node.Attributes.GetValueOrDefault("role") == "treeitem");
        item.Attributes.Should().NotContainKey("disabled");
        item.Attributes["aria-disabled"].Should().Be("true");
    }
}
