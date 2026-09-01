using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;
using static eQuantic.UI.Components.UI;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The DECLARATIVE surface can state semantics — pinned end to end.
/// <para>
/// The OS Cleaner's F1 report (the friction ledger in <c>docs/DESKTOP-PLAN.md</c>): factories
/// mirror constructors, <c>Label</c>/<c>Selected</c> were init-only, so a fully declarative screen
/// could not name an icon-only button or mark a nav item selected — the app stayed imperative
/// because of it. The fix widened the FACTORY pair (C# here, the runtime's <c>UI.ts</c> twin, and
/// the transpiled pin) rather than the constructor, because the constructor's trailing slot is
/// where the compiler lands object initializers — reshaping it would have broken every emitted
/// initializer. These tests author nodes EXACTLY as a declarative screen does (<c>using static</c>
/// factories, no <c>new</c>, no initializers) and assert the semantics reach the rendered HTML.
/// </para>
/// </summary>
public class FactorySemanticSurfaceTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static HtmlNode Rendered(VisualNode node, string attribute) =>
        Walk(WebRealizer.Lower(node, Theme).Render())
            .First(candidate => candidate.Attributes.ContainsKey(attribute));

    [Fact]
    public void PressableFactoryStatesNameAndSelection()
    {
        var node = Pressable(Icon(Icons.Search), onPressed: () => { },
            label: "Search", selected: true);

        var button = Rendered(node, "aria-pressed");
        button.Attributes["aria-pressed"].Should().Be("true");
        button.Attributes["aria-label"].Should().Be("Search");
    }

    [Fact]
    public void PressableFactoryStatesDisclosureAndDisabled()
    {
        var expanded = Rendered(
            Pressable(Icon(Icons.Search), () => { }, expanded: false), "aria-expanded");
        expanded.Attributes["aria-expanded"].Should().Be("false",
            "closed is a statement — only a null Expanded means no disclosure at all");

        var disabled = Rendered(
            Pressable(Icon(Icons.Search), () => { }, label: "Search", disabled: true), "disabled");
        disabled.Attributes.Should().ContainKey("disabled");
    }

    [Fact]
    public void PressableFactoryCarriesThePressedFill()
    {
        var node = Pressable(Icon(Icons.Search), () => { },
            pressedBackground: Theme.SurfaceSubtle);

        var button = Walk(WebRealizer.Lower(node, Theme).Render())
            .First(candidate => candidate.Attributes.TryGetValue("style", out var style) &&
                                style.Contains("--eq-pressed-bg"));
        button.Attributes["class"].Should().Contain("eq-pressable");
    }

    [Fact]
    public void LinkFactoryStatesNameAndWhereYouAre()
    {
        var node = Link("/scan", Icon(Icons.Search), label: "Scan", current: true);

        var anchor = Rendered(node, "aria-current");
        anchor.Attributes["aria-current"].Should().Be("page");
        anchor.Attributes["aria-label"].Should().Be("Scan");
    }

    [Fact]
    public void TextEntryFactoryStatesNameHintAndObscuring()
    {
        var node = TextEntry("", label: "Password", placeholder: "Enter password",
            obscure: true, disabled: true);

        var input = Rendered(node, "placeholder");
        input.Attributes["type"].Should().Be("password");
        input.Attributes["placeholder"].Should().Be("Enter password");
        input.Attributes["aria-label"].Should().Be("Password");
        input.Attributes.Should().ContainKey("disabled");
    }
}

/// <summary>
/// The LAYOUT tail — the OS Cleaner's report after migrating its whole UI to the declarative surface:
/// 49 places could not say a container's width or height (init-only on VisualNode, no factory
/// parameter), so `new Row(...) { Width = SizeValue.Fill }` stayed the only spelling of the most
/// common layout there is. And 3 places needed a Pressable's composite role.
/// </summary>
public class FactoryLayoutTailTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    [Fact]
    public void EveryContainerFactory_ReachesWidthAndHeight()
    {
        Row(width: SizeValue.Fill).Width.Should().Be(SizeValue.Fill);
        Column(height: SizeValue.Fill).Height.Should().Be(SizeValue.Fill);
        Grid([GridTrack.Flex()], width: SizeValue.Fill).Width.Should().Be(SizeValue.Fill);
        Stack(width: SizeValue.Fixed(320), height: SizeValue.Fixed(200)).Height.Should().Be(SizeValue.Fixed(200));
        ScrollView(Text("x"), height: SizeValue.Fill).Height.Should().Be(SizeValue.Fill);
        ListView(3, 44, i => Text($"{i}"), width: SizeValue.Fill).Width.Should().Be(SizeValue.Fill);
    }

    [Fact]
    public void LeavingTheTailOut_MeansHug_ExactlyAsAnInitializerWould()
    {
        // default(SizeValue) is Hug: the factory without a width is the constructor without one.
        Row().Width.Should().Be(new Row(gap: 0).Width);
        Column(children: [Text("a")]).Height.Should().Be(SizeValue.Hug);
    }

    [Fact]
    public void PressableFactoryStatesItsCompositeRole()
    {
        var node = Pressable(Text("Overview"), () => { }, selected: true, role: PressableRole.Radio);
        var html = WebRealizer.Lower(node, Theme).Render();
        IEnumerable<HtmlNode> Walk(HtmlNode n) { yield return n; foreach (var c in n.Children) foreach (var d in Walk(c)) yield return d; }
        var radio = Walk(html).First(n => n.Attributes.TryGetValue("role", out var role) && role == "radio");
        radio.Attributes["aria-checked"].Should().Be("true", "a radio states its selection as checked, not pressed");
    }
}
