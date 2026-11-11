using System.Text.RegularExpressions;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Button = eQuantic.UI.Components.Button;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// WHERE each component that has a pressed fill paints it on the web, and in what colour (#616):
/// the web half of <c>PressedFillPlacementTests</c>, over the same eight components and the same
/// places, so the two targets cannot drift apart again.
/// <para>
/// The handoff names the place. A button, an icon button, a chip, a list row, the search field's
/// clear button and a tab fill the surface the control wraps, which is its first child; the
/// navigation bar and the rail show their pill, on every destination but the selected one, whose
/// pill already shows. Until #616 the web filled the first child of every
/// pressed control while Photon filled the first box it drew, which for a tab was the 3dp strip
/// under its label.
/// </para>
/// </summary>
public class PressedFillPlacementRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;
    private static readonly VariantColors Primary = Theme.Colors(Variant.Primary);

    public enum Place { Surface, Pill }

    public sealed record Case(string Name, Func<VisualNode> Build, ColorToken Fill, Place Where,
        float PillWidth = 0, float PillHeight = 0)
    {
        public override string ToString() => Name;
    }

    private static readonly NavItem[] Items =
        [new NavItem(Icons.CheckCircle, "Home"), new NavItem(Icons.Mail, "Cards"), new NavItem(Icons.Person, "Profile")];

    public static TheoryData<Case> Cases => new()
    {
        new Case("button", () => new Button("Pay", onPressed: () => { }), Primary.Pressed, Place.Surface),
        new Case("filled icon button", () => new IconButton(new Icon(Icons.Search), "Search",
            IconButtonKind.Filled, onPressed: () => { }), Primary.Pressed, Place.Surface),
        new Case("selected filter chip", () => new Chip("Income", ChipKind.Filter, selected: true,
            onPressed: () => { }), Primary.Pressed.WithOpacity(0.24f), Place.Surface),
        new Case("list item", () => new ListItem("Wallet", onPressed: () => { }), Theme.SurfaceSubtle, Place.Surface),
        new Case("search field's clear button", () => new SearchField("query", _ => { }), Theme.Border, Place.Surface),
        new Case("tab", () => new Tabs(["Overview", "Activity", "Settings"], 1, _ => { }), Theme.SurfaceSubtle, Place.Surface),
        new Case("navigation bar item", () => new BottomNavigation(Items, 0, _ => { }), Theme.SurfaceSubtle,
            Place.Pill, 56, 26),
        new Case("navigation rail destination", () => new NavigationRail(Items, 0, _ => { }), Theme.SurfaceSubtle,
            Place.Pill, 52, 30),
    };

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static string[] Classes(HtmlNode node) =>
        node.Attributes.TryGetValue("class", out var value) && value is not null
            ? value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            : [];

    /// <summary>Whether one of the node's classes carries a declaration matching
    /// <paramref name="declaration"/> in the pressed family, or at rest when <paramref name="pressed"/>
    /// is false.</summary>
    private static bool Writes(string css, HtmlNode node, string declaration, bool pressed) =>
        Classes(node).Any(cls => Regex.IsMatch(css,
            (pressed ? @"\.eq-pressable\.eq-pressable:active:not\(:where\(:has\(\.eq-pressable:active\)\)\) " : @"(?:^|\})")
            + $@"\.{Regex.Escape(cls)}" + (pressed ? @"(?::not\([^{}]*\))?" : "")
            + $@"\{{{declaration}\}}"));

    /// <summary>A colour declaration, through the theme's custom property when the token is named.</summary>
    private static string Colour(string property, ColorToken token) =>
        $@"{property}:[^{{}};]*{Regex.Escape(TokenCss.Value(token))}[^{{}};]*";

    /// <summary>A selected row's fill IS its state, and a press does not wash it away.</summary>
    [Fact]
    public void APressedSelectedRow_KeepsItsSelection()
    {
        var sink = new StyleSink();
        WebRealizer.Lower(new ListItem("Wallet", onPressed: () => { }) { Selected = true }, Theme, 1f, sink);

        sink.Css.Should().NotMatchRegex(@"\.eq-pressable\.eq-pressable:active[^{}]*\{background",
            "a selected row takes no pressed fill");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void APressedComponent_FillsWhereTheHandoffSays(Case c)
    {
        var sink = new StyleSink();
        var root = WebRealizer.Lower(c.Build(), Theme, 1f, sink).Render();
        var css = sink.Css;
        var fill = Colour("background-color", c.Fill);

        var controls = Walk(root).Where(node => Classes(node).Contains("eq-pressable")).ToList();
        controls.Should().NotBeEmpty("the component is pressable");

        foreach (var control in controls)
        {
            var filled = Walk(control).Skip(1).Where(node => Writes(css, node, fill, pressed: true)).ToList();
            if (c.Where == Place.Pill && control.Attributes.ContainsKey("aria-current"))
            {
                filled.Should().BeEmpty($"the selected {c.Name}'s pill already shows, and keeps its colour pressed");
                continue;
            }
            filled.Should().ContainSingle($"a pressed {c.Name} paints its fill once");
            var target = filled[0];

            if (c.Where == Place.Surface)
                target.Should().BeSameAs(control.Children[0],
                    $"a pressed {c.Name} fills the surface it wraps, not a part of it");
            else
            {
                target.Should().NotBeSameAs(control.Children[0], $"a pressed {c.Name} fills its pill, never the item");
                Writes(css, target, $@"width:{c.PillWidth}px", pressed: false).Should().BeTrue(
                    $"the pressed {c.Name}'s fill is its {c.PillWidth}dp-wide pill");
                Writes(css, target, $"height:{c.PillHeight}px", pressed: false).Should().BeTrue(
                    $"the pressed {c.Name}'s fill is its {c.PillHeight}dp-tall pill");
            }
        }
    }
}
