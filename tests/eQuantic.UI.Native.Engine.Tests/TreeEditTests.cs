using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A <see cref="TreeView"/> edited in place on Photon (#1532), driven through the host the shells drive, a key and a
/// frame at a time: F2 opens a field over the keyboard's node with its label selected, Enter commits and gives the
/// keyboard back to the tree, Escape and an empty name cancel, the app's request creates under a node, a busy node
/// takes nothing, and the menu key opens the menu of the tree's active item. The web twin is
/// <c>tree-view-edit.spec.ts</c>.
/// <para>
/// Where the keyboard is, a person reads by what Enter presses: the tree has the keyboard back when Enter presses
/// its active item rather than reaching a field.
/// </para>
/// </summary>
[Collection(ProcessDispatcherCollection.Name)]
public class TreeEditTests : IDisposable
{
    private readonly IUiDispatcher? _outer = UiDispatcher.Current;

    public TreeEditTests() => UiDispatcher.Current = PhotonDispatcher.Shared;

    public void Dispose() => UiDispatcher.Current = _outer;

    private static TreeNode Leaf(string key, string label) => new(key, label) { IsLeaf = true };

    /// <summary>The app around the tree: its requests, its busy nodes, and what it heard.</summary>
    private sealed class Page : StatefulComponent
    {
        public readonly List<string> Pressed = [];
        public readonly List<string> Renamed = [];
        public readonly List<string> Created = [];
        public readonly List<string> Chosen = [];
        public int Loads;
        public TaskCompletionSource Work = new();
        public TreeEditRequest? Edit;
        public IReadOnlyList<string> Busy = [];

        public Page() => Work.SetResult();

        public void Change(Action change) => SetState(change);

        public override VisualNode Build(ComponentContext context) => new TreeView(
            [
                new TreeNode("src", "src") { Children = [Leaf("src/App.cs", "App.cs"), Leaf("src/Ui.cs", "Ui.cs")] },
                new TreeNode("docs", "docs"),
                Leaf("README.md", "README.md"),
            ],
            onPressed: node => Pressed.Add(node.Key))
        {
            Label = "Files",
            LoadChildren = node =>
            {
                Loads++;
                return Task.FromResult<IReadOnlyList<TreeNode>>([Leaf($"{node.Key}/intro-{Loads}.md", $"intro-{Loads}.md")]);
            },
            OnRenamed = async (node, name) =>
            {
                Renamed.Add($"{node.Key}>{name}");
                await Work.Task;
            },
            OnCreated = async (node, name) =>
            {
                Created.Add($"{node.Key}+{name}");
                await Work.Task;
            },
            Edit = Edit,
            BusyKeys = Busy,
            MenuBuilder = _ => [new MenuItem("Rename"), new MenuItem("Delete")],
            OnMenuSelect = (node, index) => Chosen.Add($"{node.Key}#{index}"),
        };
    }

    private sealed class Harness(PhotonHost host, Page page)
    {
        public PhotonHost Host { get; } = host;
        public Page Page { get; } = page;

        public void Frame() => Host.RenderFrame(new DisplayListBuilder());

        public void Key(string key, KeyModifiers modifiers = KeyModifiers.None)
        {
            Host.KeyDown(key, modifiers);
            Frame();
        }

        public void Type(string text)
        {
            Host.TextInput(text).Should().BeTrue("the field has the keyboard");
            Frame();
        }

        public SemanticNode Item(string label) =>
            Host.Semantics().Single(node => node.Role == SemanticRole.TreeItem && node.Label == label);

        public IReadOnlyList<string> MenuItems() =>
            Host.Semantics().Where(node => node.Role == SemanticRole.MenuItem).Select(node => node.Label).ToList();

        /// <summary>Frames until <paramref name="done"/> holds: the app's task ends off the frame that ran it.</summary>
        public void FramesUntil(Func<bool> done)
        {
            for (var i = 0; i < 200 && !done(); i++)
            {
                Thread.Sleep(10);
                Frame();
            }
            done().Should().BeTrue();
        }
    }

    private static Harness Open(Page? page = null)
    {
        page ??= new Page();
        var harness = new Harness(new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 360, 480), page);
        harness.Frame();
        harness.Key("Tab");
        return harness;
    }

    /// <summary>The keyboard on App.cs, inside open src.</summary>
    private static Harness OnAppCs()
    {
        var harness = Open();
        harness.Key("ArrowDown");
        harness.Key("ArrowRight");
        harness.Key("ArrowRight");
        return harness;
    }

    [Fact]
    public void F2_OpensAFieldWithTheLabelSelected_AndEnterRenames_AndGivesTheKeyboardBack()
    {
        var harness = OnAppCs();

        harness.Key("F2");
        harness.Host.HasTextFocus.Should().BeTrue("the field over the label takes the keyboard");
        // The label is selected: what is typed replaces it.
        harness.Type("Main.cs");
        harness.Key("Enter");

        harness.Page.Renamed.Should().Equal("src/App.cs>Main.cs");
        harness.Host.HasTextFocus.Should().BeFalse("the field closed");
        harness.Page.Pressed.Should().BeEmpty("Enter in the field renamed, it pressed nothing");

        // The keyboard is back on the tree, on the node: Down then Enter presses the next one.
        harness.Key("ArrowDown");
        harness.Key("Enter");
        harness.Page.Pressed.Should().Equal("src/Ui.cs");
    }

    [Fact]
    public void Escape_AnEmptyName_AndTheSameName_Cancel()
    {
        var harness = OnAppCs();

        harness.Key("F2");
        harness.Type("Other.cs");
        harness.Key("Escape");
        harness.Host.HasTextFocus.Should().BeFalse();

        harness.Key("F2");
        harness.Key("Backspace");
        harness.Key("Enter");

        harness.Key("F2");
        harness.Key("Enter");

        harness.Page.Renamed.Should().BeEmpty();
        // Still on App.cs, and the tree has the keyboard.
        harness.Key("Enter");
        harness.Page.Pressed.Should().Equal("src/App.cs");
    }

    [Fact]
    public void TheArrowsTypedInTheField_AreTheFields_AndMoveNothingInTheTree()
    {
        var harness = OnAppCs();
        harness.Key("F2");
        harness.Key("ArrowUp");
        harness.Key("Home");
        harness.Host.HasTextFocus.Should().BeTrue();
        harness.Key("Escape");
        harness.Key("Enter");
        harness.Page.Pressed.Should().Equal("src/App.cs");
    }

    [Fact]
    public void ARenamedNode_IsBusyWhileTheAppWorks_AndTakesNoPressNoEditAndNoMenu()
    {
        var page = new Page { Work = new TaskCompletionSource() };
        var harness = Open(page);
        harness.Key("ArrowDown");
        harness.Key("ArrowRight");
        harness.Key("ArrowRight");
        harness.Key("F2");
        harness.Type("Main.cs");
        harness.Key("Enter");
        harness.FramesUntil(() => harness.Item("App.cs").Disabled);

        harness.Key("Enter");
        harness.Key("F2");
        harness.Key("ContextMenu");
        harness.Page.Pressed.Should().BeEmpty();
        harness.Host.HasTextFocus.Should().BeFalse();
        harness.MenuItems().Should().BeEmpty();

        page.Work.SetResult();
        harness.FramesUntil(() => !harness.Item("App.cs").Disabled);
    }

    [Fact]
    public void ANodeTheAppSaysIsBusy_TakesNothing()
    {
        var page = new Page { Busy = ["README.md"] };
        var harness = Open(page);
        // The first key lands the keyboard on the first item; End then walks to the last.
        harness.Key("ArrowDown");
        harness.Key("End");
        harness.Item("README.md").Disabled.Should().BeTrue();
        harness.Key("Enter");
        harness.Key("F2");
        harness.Key("ContextMenu");
        page.Pressed.Should().BeEmpty();
        harness.Host.HasTextFocus.Should().BeFalse();
        harness.MenuItems().Should().BeEmpty();
    }

    [Fact]
    public void ACreatingRequest_OpensTheNode_PutsAFieldFirstUnderIt_AndReadsItsChildrenAgain()
    {
        var harness = Open();
        harness.Page.Change(() => harness.Page.Edit = new TreeEditRequest(1, "docs") { Creates = true });
        harness.Frame();
        harness.FramesUntil(() => harness.Page.Loads == 1 && harness.Host.Semantics().Any(n => n.Label == "intro-1.md"));
        harness.Item("docs").Expanded.Should().BeTrue();
        harness.Host.HasTextFocus.Should().BeTrue("the new item's field takes the keyboard");
        harness.Item("intro-1.md").TreePlace.Should().Be(new TreePlace(2, 2, 2), "the field stands first, and counts");

        harness.Type("notes.md");
        harness.Key("Enter");
        harness.FramesUntil(() => harness.Host.Semantics().Any(n => n.Label == "intro-2.md"));
        harness.Page.Created.Should().Equal("docs+notes.md");
        harness.Page.Loads.Should().Be(2, "the children the tree loaded were read again");
        harness.Host.HasTextFocus.Should().BeFalse();
    }

    [Fact]
    public void TheMenuKey_OpensTheMenuOfTheTreesActiveItem_AndTheChoiceIsReported()
    {
        var harness = Open();
        harness.Key("ArrowDown");
        harness.Key("ArrowDown");

        harness.Key("ContextMenu");
        harness.MenuItems().Should().Equal("Rename", "Delete");
        harness.Key("ArrowDown");
        harness.Key("Enter");

        harness.Page.Chosen.Should().Equal("docs#1");
        harness.MenuItems().Should().BeEmpty();
        harness.Page.Pressed.Should().BeEmpty();
    }

    [Fact]
    public void ShiftF10_AsksForTheMenuOfTheActiveItem_NotOfTheTree()
    {
        var harness = Open();
        harness.Key("ArrowDown");
        harness.Key("F10", KeyModifiers.Shift);
        harness.MenuItems().Should().Equal("Rename", "Delete");
        harness.Key("Escape");
        harness.MenuItems().Should().BeEmpty();
        harness.Page.Chosen.Should().BeEmpty();
    }
}
