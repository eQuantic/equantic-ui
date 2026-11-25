using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;

using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A <see cref="TreeView"/> whose nodes move on Photon (#1533), driven through the host the shells drive: a mouse
/// carries a node inside a folder, never inside its own child, a closed folder held under it opens, and the keyboard
/// moves the tree's active node from the tree's one stop, passing over a folder the app refuses. The web twin is
/// <c>tree-view-move.spec.ts</c>.
/// </summary>
[Collection(ProcessDispatcherCollection.Name)]
public class TreeMoveTests : IDisposable
{
    private readonly IUiDispatcher? _outer = UiDispatcher.Current;

    public TreeMoveTests() => UiDispatcher.Current = PhotonDispatcher.Shared;

    public void Dispose() => UiDispatcher.Current = _outer;

    private static TreeNode Leaf(string key, string label) => new(key, label) { IsLeaf = true };

    private static readonly Dictionary<string, string[]> Listing = new()
    {
        ["src"] = ["App.cs", "Program.cs"],
        ["docs"] = ["intro.md"],
        ["locked"] = ["secret.md"],
    };

    /// <summary>The app around the tree: what it heard.</summary>
    private sealed class Page : StatefulComponent
    {
        public readonly List<string> Moved = [];
        public readonly List<string> Loads = [];
        public readonly List<string> Pressed = [];

        public override VisualNode Build(ComponentContext context) => new TreeView(
            [new TreeNode("src", "src"), new TreeNode("docs", "docs"), new TreeNode("locked", "locked"), Leaf("README.md", "README.md")],
            onPressed: node => Pressed.Add(node.Key))
        {
            Label = "Files",
            ExpandedKeys = ["src"],
            LoadChildren = node =>
            {
                Loads.Add(node.Key);
                var names = Listing.TryGetValue(node.Key, out var listed) ? listed : [];
                return Task.FromResult<IReadOnlyList<TreeNode>>(names.Select(name => Leaf($"{node.Key}/{name}", name)).ToList());
            },
            OnMoved = move =>
            {
                Moved.Add($"{move.Node.Key}>{move.Parent?.Key}|{move.After?.Key}");
                return Task.CompletedTask;
            },
            CanMove = move => move.Parent?.Key != "locked",
        };
    }

    private sealed class Harness(PhotonHost host, Page page)
    {
        private float _now;

        public PhotonHost Host { get; } = host;
        public Page Page { get; } = page;

        public void Frame() => Host.RenderFrame(new DisplayListBuilder(), _now += 16);

        public void Key(string key)
        {
            Host.KeyDown(key);
            Frame();
        }

        /// <summary>The centre of the row of the node with <paramref name="key"/>, where the last frame drew it.</summary>
        public Point Row(string key) =>
            Walk(Host.LastFrame!.Root).First(n => n.Source is SortableItem { } item && item.Id == key).Bounds.Center;

        public Rect RowBox(string key) =>
            Walk(Host.LastFrame!.Root).First(n => n.Source is SortableItem { } item && item.Id == key).Bounds;

        public bool HasRow(string key) =>
            Host.LastFrame is { } frame && Walk(frame.Root).Any(n => n.Source is SortableItem { } item && item.Id == key);

        /// <summary>Frames until <paramref name="done"/> holds: a load and a pause end off the frame that began them.</summary>
        public void FramesUntil(Func<bool> done)
        {
            for (var i = 0; i < 200 && !done(); i++)
            {
                Thread.Sleep(10);
                Frame();
            }
            done().Should().BeTrue();
        }

        private static IEnumerable<LayoutNode> Walk(LayoutNode node)
        {
            yield return node;
            foreach (var child in node.Children)
                foreach (var below in Walk(child))
                    yield return below;
        }
    }

    private static Harness Open()
    {
        var page = new Page();
        var harness = new Harness(new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 360, 480), page);
        harness.Frame();
        harness.FramesUntil(() => page.Loads.Contains("src") && harness.HasRow("src/Program.cs"));
        return harness;
    }

    /// <summary>A mouse presses the row of <paramref name="key"/> and carries it to <paramref name="to"/>.</summary>
    private static void Carry(Harness harness, string key, Point to, bool release = true)
    {
        var from = harness.Row(key);
        harness.Host.PressDown(from.X, from.Y);
        harness.Host.PointerMove(from.X, from.Y + (to.Y > from.Y ? 10 : -10));
        harness.Frame();
        harness.Host.PointerMove(to.X, to.Y);
        harness.Frame();
        if (release) harness.Host.PressUp(to.X, to.Y);
    }

    [Fact]
    public void AMouse_MovesAFileInsideAClosedFolder_AsItsFirstChild()
    {
        var harness = Open();
        Carry(harness, "README.md", harness.Row("docs"));

        harness.Page.Moved.Should().Equal("README.md>docs|");
        harness.Page.Pressed.Should().BeEmpty("a press that carried a node does not press it");
    }

    [Fact]
    public void ANode_NeverLandsInsideItself()
    {
        var harness = Open();
        // src over Program.cs's top quarter: before its own child.
        var program = harness.RowBox("src/Program.cs");
        Carry(harness, "src", new Point(program.X + 20, program.Y + program.Height / 8));

        harness.Page.Moved.Should().BeEmpty();
    }

    [Fact]
    public void ANode_NeverLandsInsideAFile()
    {
        var harness = Open();
        Carry(harness, "README.md", harness.Row("src/App.cs"));

        harness.Page.Moved.Should().BeEmpty();
    }

    [Fact]
    public void AClosedFolder_HeldUnderACarriedNode_Opens()
    {
        var harness = Open();
        Carry(harness, "README.md", harness.Row("docs"), release: false);

        harness.FramesUntil(() => harness.Page.Loads.Contains("docs"));
        harness.Page.Moved.Should().BeEmpty("the node is still carried");
        harness.Host.PointerCancel();
    }

    [Fact]
    public void TheKeyboard_MovesTheActiveNode_FromTheTreesStop_PassingOverARefusedFolder()
    {
        var harness = Open();
        harness.Key("Tab");
        // The first key lands the keyboard on the tree; End walks to the last node.
        harness.Key("ArrowDown");
        harness.Key("End");

        harness.Key(" ");
        // Inside locked is refused and passed over: before locked, then inside docs.
        harness.Key("ArrowUp");
        harness.Key("ArrowUp");
        harness.Key("Enter");

        harness.Page.Moved.Should().Equal("README.md>docs|");
        harness.Page.Pressed.Should().BeEmpty("the Enter that dropped the node was the carry's alone");
    }

    // ---- the depth a node lands at (#1594) ------------------------------------------------------------------------

    /// <summary>A point over the top eighth of the row of <paramref name="key"/>, <paramref name="across"/> points from
    /// where <paramref name="carried"/>'s row is pressed: the gap before that row, at the depth the travel asks for.</summary>
    private static Point Before(Harness harness, string key, string carried, float across)
    {
        var row = harness.RowBox(key);
        return new Point(harness.Row(carried).X + across, row.Y + row.Height / 8);
    }

    [Fact]
    public void AMouse_LandsAFileAsTheLastChildOfAnOpenFolder_WhenItTravelsAnIndentAcross()
    {
        var harness = Open();
        Carry(harness, "README.md", Before(harness, "docs", "README.md", 16));

        harness.Page.Moved.Should().Equal("README.md>src|src/Program.cs");
    }

    [Fact]
    public void AMouse_LandsItAtTheTopLevelThere_WhenItDoesNotTravelAcross()
    {
        var harness = Open();
        Carry(harness, "README.md", Before(harness, "docs", "README.md", 0));

        harness.Page.Moved.Should().Equal("README.md>|src");
    }

    [Fact]
    public void AMouse_LandsAFoldersLastChildAtTheTopLevelAfterIt_WhenItTravelsBack()
    {
        var harness = Open();
        Carry(harness, "src/Program.cs", Before(harness, "docs", "src/Program.cs", -16));

        harness.Page.Moved.Should().Equal("src/Program.cs>|src");
    }

    [Fact]
    public void TheKeyboard_PutsTheActiveNodeAtTheEndOfAnOpenFolder()
    {
        var harness = Open();
        harness.Key("Tab");
        harness.Key("ArrowDown");
        harness.Key("End");

        harness.Key(" ");
        // Inside locked is refused and passed over: before locked, inside docs, before docs.
        harness.Key("ArrowUp");
        harness.Key("ArrowUp");
        harness.Key("ArrowUp");
        harness.Key("ArrowRight");
        harness.Key("Enter");

        harness.Page.Moved.Should().Equal("README.md>src|src/Program.cs");
    }
}
