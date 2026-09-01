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
