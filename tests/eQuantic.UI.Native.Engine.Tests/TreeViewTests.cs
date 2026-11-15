using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A <see cref="TreeView"/> on Photon (#1529, #1530), driven through the host the shells drive, a key
/// and a frame at a time: the WAI-ARIA tree pattern's keyboard, text search through
/// <see cref="PhotonHost.TextInput"/>, children loaded when a node first opens (and loaded again after a
/// failure), a path revealed, and what a reader hears. The web twins are <c>tree-view.spec.ts</c> and
/// <c>TreeSemanticsTests</c>.
/// <para>
/// What the keyboard is on has no native counterpart of the web's <c>aria-activedescendant</c>, so these
/// tests read it the way a person does: Enter presses the item the keyboard is on, and the press is
/// reported.
/// </para>
/// </summary>
public class TreeViewTests
{
    private static TreeNode Leaf(string key, string label) => new(key, label) { IsLeaf = true };

    private static IReadOnlyList<TreeNode> Files() =>
    [
        new TreeNode("src", "src") { Children = [Leaf("src/App.cs", "App.cs"), Leaf("src/Assets", "Assets")] },
        new TreeNode("docs", "docs"),
        Leaf("README.md", "README.md"),
    ];

    private sealed class Harness
    {
        public readonly List<string> Pressed = [];
        public readonly List<string> Toggled = [];
        public PhotonHost Host = null!;

        public void Frame() => Host.RenderFrame(new DisplayListBuilder());

        /// <summary>A key, then a frame, as a keyboard and a shell do.</summary>
        public void Key(string key)
        {
            Host.KeyDown(key);
            Frame();
        }

        public void Type(string text)
        {
            Host.TextInput(text).Should().BeTrue($"'{text}' is a search while the tree holds the keyboard");
            Frame();
        }

        public IReadOnlyList<SemanticNode> Items() =>
            Host.Semantics().Where(node => node.Role == SemanticRole.TreeItem).ToList();

        public SemanticNode Item(string label) => Items().Single(node => node.Label == label);

        /// <summary>Frames until <paramref name="done"/> holds: a load finishes off the frame that asked.</summary>
        public void FramesUntil(Func<bool> done)
        {
            for (var i = 0; i < 200 && !done(); i++)
            {
                Thread.Sleep(10);
                Frame();
            }
            done().Should().BeTrue("the load finished and the next frame showed it");
        }
    }

    private static Harness Open(Func<TreeNode, Task<IReadOnlyList<TreeNode>>>? loader = null,
        IReadOnlyList<string>? reveal = null, IReadOnlyList<TreeNode>? roots = null)
    {
        var harness = new Harness();
        var tree = new TreeView(roots ?? Files(), onPressed: node => harness.Pressed.Add(node.Key))
        {
            Label = "Files",
            LoadChildren = loader,
            OnToggled = (node, open) => harness.Toggled.Add($"{node.Key}:{open}"),
            RevealPath = reveal,
        };
        harness.Host = new PhotonHost(tree, PhotonTheme.Instance, ThemeMode.Light, 360, 480);
        harness.Frame();
        // Tab reaches the tree, the one stop it is.
        harness.Key("Tab");
        return harness;
    }

    [Fact]
    public void TheTreeIsOneStop_AndItsItemsAreReadWithTheirLevelAndPlace()
    {
        var harness = Open();
        var frame = harness.Host.RenderFrame(new DisplayListBuilder());
        frame.FocusStops.Should().ContainSingle(stop => stop.Grid != null, "the tree is the composite's one stop");
        frame.FocusStops.Should().HaveCount(1, "no item of a tree is a Tab stop of its own");

        var semantics = harness.Host.Semantics();
        semantics.Should().ContainSingle(node => node.Role == SemanticRole.Tree).Which.Label.Should().Be("Files");
        // Open and closed are stated on a node that has something to open: docs gives no children and
        // this tree has no loader, so it has none, and says nothing, like the leaf after it.
        harness.Items().Select(item => (item.Label, item.TreePlace, item.Expanded)).Should().Equal(
            ("src", new TreePlace(1, 1, 3), false),
            ("docs", new TreePlace(1, 2, 3), (bool?)null),
            ("README.md", new TreePlace(1, 3, 3), (bool?)null));
    }

    [Fact]
    public void TheArrowsWalkOpenEnterAndLeave_AndEnterPressesTheItemTheKeyboardIsOn()
    {
        var harness = Open();

        harness.Key("ArrowDown");   // lands on the first item
        harness.Key("ArrowRight");  // opens src
        harness.Item("src").Expanded.Should().BeTrue();
        harness.Item("App.cs").TreePlace.Should().Be(new TreePlace(2, 1, 2));
        harness.Toggled.Should().Equal("src:True");

        harness.Key("ArrowRight");  // into src
        harness.Key("ArrowDown");   // Assets
        harness.Pressed.Should().BeEmpty("moving never presses");
        harness.Key("Enter");
        harness.Pressed.Should().Equal("src/Assets");

        harness.Key("ArrowLeft");   // up to src
        harness.Key("ArrowLeft");   // closes src
        harness.Item("src").Expanded.Should().BeFalse();
        harness.Items().Select(item => item.Label).Should().Equal("src", "docs", "README.md");

        harness.Key("End");
        harness.Key("Enter");
        harness.Key("Home");
        harness.Key("Enter");
        harness.Pressed.Should().Equal("src/Assets", "README.md", "src");
    }

    [Fact]
    public void TypingALabelsFirstLettersMovesToIt_AndOneLetterAgainCycles()
    {
        var harness = Open(roots:
        [
            Leaf("App.cs", "App.cs"),
            Leaf("Assets", "Assets"),
            Leaf("README.md", "README.md"),
        ]);
        harness.Key("End");        // README.md
        harness.Type("a");         // wraps to App.cs
        harness.Type("s");         // "as" narrows to Assets
        harness.Key("Enter");
        harness.Pressed.Should().Equal("Assets");

        Thread.Sleep(800);         // the search ends with a pause
        harness.Type("a");         // the next one after Assets that starts with a: App.cs again
        harness.Key("Enter");
        harness.Pressed.Should().Equal("Assets", "App.cs");
    }

    [Fact]
    public void SpaceIsNotASearch()
    {
        var harness = Open();
        harness.Host.TextInput(" ").Should().BeFalse("Space is a key of its own, never a search");
    }

    [Fact]
    public void AFolderLoadsOnce_WithALoadingItemUnderItUntilItsChildrenArrive()
    {
        var gate = new TaskCompletionSource<IReadOnlyList<TreeNode>>();
        var loads = 0;
        var harness = Open(loader: _ =>
        {
            loads++;
            return gate.Task;
        });

        harness.Key("ArrowDown");
        harness.Key("ArrowDown");   // docs
        harness.Key("ArrowRight");  // opens it, and its load starts
        harness.Item(SdkStrings.TreeLoading).TreePlace.Should().Be(new TreePlace(2, 1, 1));

        gate.SetResult([Leaf("docs/intro.md", "intro.md")]);
        harness.FramesUntil(() => harness.Items().Any(item => item.Label == "intro.md"));
        harness.Items().Should().NotContain(item => item.Label == SdkStrings.TreeLoading);

        harness.Key("ArrowLeft");   // closes docs
        harness.Key("ArrowRight");  // opens it again: what was loaded is kept
        harness.Items().Should().Contain(item => item.Label == "intro.md");
        loads.Should().Be(1);
    }

    [Fact]
    public void AFailedLoadSaysSo_AndItsItemLoadsAgain()
    {
        var calls = 0;
        var harness = Open(loader: _ =>
        {
            calls++;
            return calls == 1
                ? Task.FromException<IReadOnlyList<TreeNode>>(new InvalidOperationException("offline"))
                : Task.FromResult<IReadOnlyList<TreeNode>>([Leaf("docs/intro.md", "intro.md")]);
        });

        harness.Key("ArrowDown");
        harness.Key("ArrowDown");   // docs
        harness.Key("ArrowRight");  // opens it; the load fails
        harness.FramesUntil(() => harness.Items().Any(item => item.Label == SdkStrings.TreeLoadFailed));

        harness.Key("ArrowDown");   // the failed item
        harness.Key("Enter");       // tries again
        harness.FramesUntil(() => harness.Items().Any(item => item.Label == "intro.md"));
        calls.Should().Be(2);
        harness.Pressed.Should().BeEmpty("a status item is not a node, and pressing it reports nothing");
    }

    [Fact]
    public void APathIsRevealed_LoadingEachFolderOnItsWay_AndTheKeyboardLandsOnItsLastNode()
    {
        var loads = new List<string>();
        var harness = Open(
            loader: node =>
            {
                loads.Add(node.Key);
                IReadOnlyList<TreeNode> children = node.Key == "docs"
                    ? [new TreeNode("docs/guides", "guides")]
                    : [Leaf("docs/guides/intro.md", "intro.md")];
                return Task.FromResult(children);
            },
            reveal: ["docs", "docs/guides", "docs/guides/intro.md"]);

        harness.FramesUntil(() => harness.Items().Any(item => item.Label == "intro.md"));
        harness.Item("docs").Expanded.Should().BeTrue();
        harness.Item("guides").Expanded.Should().BeTrue();
        harness.Item("intro.md").TreePlace.Should().Be(new TreePlace(3, 1, 1));
        loads.Should().Equal("docs", "docs/guides");

        harness.Key("Enter");
        harness.Pressed.Should().Equal("docs/guides/intro.md");
    }
}
