using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The list-detail pane on PHOTON — the half that proves the component is write-once rather than a
/// web layout with a native excuse. The web emits both shapes and lets media queries pick; here the
/// engine lays out exactly one, so the same source has to answer the window's real width.
/// </summary>
public class ListDetailTests
{
    private static VisualNode Inbox() => new ScrollView(new Column(gap: 0)
    {
        new ListItem("First message", "Someone") { OnPressed = () => { } },
        new ListItem("Second message", "Someone else") { OnPressed = () => { } },
    });

    private static IReadOnlyList<string> TextsAt(VisualNode root, float width, float height = 700)
    {
        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, width, height);
        var frame = host.RenderFrame(new DisplayListBuilder());
        var texts = new List<string>();
        Gather(frame.Root, texts);
        return texts;

        static void Gather(LayoutNode node, List<string> texts)
        {
            if (node.Source is Text { Content.Length: > 0 } text) texts.Add(text.Content);
            foreach (var child in node.Children) Gather(child, texts);
        }
    }

    /// <summary>A phone shows ONE pane. Nothing chosen means the list is the screen.</summary>
    [Fact]
    public void OnAPhone_TheListIsTheScreenUntilSomethingIsChosen()
    {
        var texts = TextsAt(new ListDetail(Inbox()) { ListTitle = "Inbox" }, width: 390);

        texts.Should().Contain("Inbox").And.Contain("First message");
        texts.Should().NotContain(SdkStrings.NothingSelected,
            "the empty state belongs to the WIDE right pane, which a phone does not have");
    }

    /// <summary>And once something is chosen it REPLACES the list — the pane swap a phone needs and
    /// a tablet must not do.</summary>
    [Fact]
    public void OnAPhone_TheDetailReplacesTheList()
    {
        var texts = TextsAt(new ListDetail(Inbox(), new Text("The message body"), onBack: () => { })
        {
            Title = "Message detail",
            ListTitle = "Inbox",
        }, width: 390);

        texts.Should().Contain("The message body").And.Contain("Message detail");
        texts.Should().NotContain("First message", "the list is not on screen beside it");
    }

    /// <summary>A tablet shows BOTH, from the same component and the same call.</summary>
    [Fact]
    public void OnATablet_BothPanesAreOnScreen()
    {
        var texts = TextsAt(new ListDetail(Inbox(), new Text("The message body"))
        {
            Title = "Message detail",
            ListTitle = "Inbox",
        }, width: 1100);

        texts.Should().Contain("First message", "the list keeps its pane")
            .And.Contain("The message body", "and the detail has its own");
    }

    /// <summary>Wide with nothing chosen still shows a designed screen rather than a blank half.</summary>
    [Fact]
    public void OnATablet_NothingChosenShowsTheEmptyState()
    {
        var texts = TextsAt(new ListDetail(Inbox()) { ListTitle = "Inbox" }, width: 1100);

        texts.Should().Contain("First message").And.Contain(SdkStrings.NothingSelected);
    }

    /// <summary>
    /// The pane the engine lays out follows the WINDOW, with no listener and no state: the same node
    /// answers differently either side of the threshold, which is the whole claim.
    /// </summary>
    [Theory]
    [InlineData(600, false)]
    [InlineData(839, false)]
    [InlineData(840, true)]
    public void TheThresholdIsTheSpecsExpandedClass(float width, bool twoPanes)
    {
        var texts = TextsAt(new ListDetail(Inbox(), new Text("The message body"))
        {
            Title = "Message detail",
        }, width);

        texts.Contains("First message").Should().Be(twoPanes);
        texts.Should().Contain("The message body", "the detail is on screen at every width here");
    }

    /// <summary>The list pane's rows still say WHICH one is current — on a wide window the highlight
    /// is the only thing that says it, so the semantics tree has to carry it.</summary>
    [Fact]
    public void TheChosenRowIsCurrentInTheSemanticsTree()
    {
        var list = new ScrollView(new Column(gap: 0)
        {
            new ListItem("First message") { OnPressed = () => { }, Selected = true },
            new ListItem("Second message") { OnPressed = () => { } },
        });
        var host = new PhotonHost(new ListDetail(list, new Text("Body")), PhotonTheme.Instance,
            ThemeMode.Light, 1100, 700);
        host.RenderFrame(new DisplayListBuilder());

        host.Semantics().Where(node => node.Current).Should().ContainSingle()
            .Which.Label.Should().Be("First message");
    }
}
