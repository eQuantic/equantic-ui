using System.Text.RegularExpressions;
using eQuantic.UI.Components;
using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The radio contract on web (spec B13): ONE Tab stop — the Adjustable wrapper, role
/// <c>radiogroup</c>, carrying the group's name — and every option a <c>role="radio"</c> stating
/// its checked-ness, OUT of the tab order (roving: arrows walk the choice, wrap at the ends,
/// pointer still presses). The SegmentedControl rides the same mechanism, so its segments are
/// pinned here too.
/// </summary>
public class RadioGroupRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    private static void Collect(HtmlNode node, Func<HtmlNode, bool> match, List<HtmlNode> found)
    {
        if (match(node)) found.Add(node);
        foreach (var child in node.Children) Collect(child, match, found);
    }

    private static List<HtmlNode> All(HtmlNode node, Func<HtmlNode, bool> match)
    {
        var found = new List<HtmlNode>();
        Collect(node, match, found);
        return found;
    }

    [Fact]
    public void RadioGroup_IsOneTabStop_WithRadioItemsOutsideTheTabOrder()
    {
        var node = Render(new RadioGroup(["Standard", "Express", "Overnight"], selected: 1,
            onChanged: _ => { }, label: "Shipping"));

        var group = All(node, n => n.Attributes.GetValueOrDefault("role") == "radiogroup").Single();
        group.Attributes["tabindex"].Should().Be("0");
        group.Attributes["aria-label"].Should().Be("Shipping",
            "the group announces its name — the visible caption is a sibling it restates");

        var radios = All(node, n => n.Attributes.GetValueOrDefault("role") == "radio");
        radios.Should().HaveCount(3);
        radios.Select(r => r.Attributes["aria-checked"]).Should().Equal("false", "true", "false");
        radios.Select(r => r.Attributes["aria-label"]).Should().Equal("Standard", "Express", "Overnight");
        radios.Should().OnlyContain(r => r.Attributes["tabindex"] == "-1",
            "roving: the group is the stop, the arrows do the walking");
        radios.Should().OnlyContain(r => !r.Attributes.ContainsKey("aria-pressed"),
            "a radio states checked-ness, never pressed-ness");

        All(node, n => n.Attributes.GetValueOrDefault("tabindex") == "0").Should().HaveCount(1,
            "ONE Tab stop for the whole group — the native <input type=radio> contract");
    }

    [Fact]
    public void RadioGroup_Disabled_HasNoTabStopAtAll_ButKeepsItsSemantics()
    {
        var node = Render(new RadioGroup(["A", "B"], selected: 0, onChanged: _ => { })
        {
            Disabled = true,
        });

        All(node, n => n.Attributes.GetValueOrDefault("role") == "radiogroup").Should().BeEmpty(
            "a disabled group takes no keyboard, so it mounts no Adjustable");
        All(node, n => n.Attributes.GetValueOrDefault("tabindex") == "0").Should().BeEmpty();

        var radios = All(node, n => n.Attributes.GetValueOrDefault("role") == "radio");
        radios.Should().HaveCount(2, "what the rows ARE does not change with their availability");
        radios[0].Attributes["aria-checked"].Should().Be("true");
    }

    [Fact]
    public void SegmentedControl_SegmentsAreRadios_InsideTheSameMechanism()
    {
        var node = Render(new SegmentedControl(["All", "Income", "Expenses"], selectedIndex: 0,
            onChanged: _ => { }));

        var group = All(node, n => n.Attributes.GetValueOrDefault("role") == "radiogroup").Single();
        group.Attributes["tabindex"].Should().Be("0");

        var radios = All(node, n => n.Attributes.GetValueOrDefault("role") == "radio");
        radios.Should().HaveCount(3);
        radios.Select(r => r.Attributes["aria-checked"]).Should().Equal("true", "false", "false");
        radios.Should().OnlyContain(r => r.Attributes["tabindex"] == "-1");

        All(node, n => n.Attributes.GetValueOrDefault("tabindex") == "0").Should().HaveCount(1,
            "the control's own doc promises ONE Tab stop — now the web keeps the promise");
    }

    /// <summary>
    /// An option builder draws what an option shows, and the group keeps its chrome (#1606, the design's
    /// decision 5): each row is still a radio named by the option's name, which the builder's content does
    /// not replace, and the content takes the option's colour from the box around it. Photon's half is
    /// <c>RadioGroupOptionBuilderTests</c>.
    /// </summary>
    [Fact]
    public void AnOptionBuilder_DrawsTheContent_AndTheRadiosKeepTheirNames()
    {
        string[] shown = ["Standard, in five days", "Express, tomorrow"];
        foreach (var (disabled, colour) in new[] { (false, Theme.TextPrimary), (true, Theme.TextMuted) })
        {
            var sink = new StyleSink();
            var node = WebRealizer.Lower(new RadioGroup(["Standard", "Express"], selected: 1, onChanged: _ => { },
                optionBuilder: index => new Text(shown[index]))
            {
                Disabled = disabled,
            }, Theme, 1f, sink).Render();

            var radios = All(node, n => n.Attributes.GetValueOrDefault("role") == "radio");
            radios.Select(r => r.Attributes["aria-label"]).Should().Equal(["Standard", "Express"],
                "an option's name is its accessible name, whatever the builder draws");

            var texts = All(node, n => n.TextContent is not null).Select(n => n.TextContent).ToList();
            texts.Should().Contain(shown, "the builder draws what each option shows");
            texts.Should().NotContain(["Standard", "Express"], "the names are not drawn beside the builder's content");

            // The atomizer writes a theme's colour as its variable with the value as the fallback.
            sink.Css.Should().MatchRegex($@"\{{color:(var\(--eq-color-[a-z-]+, )?{Regex.Escape(TokenCss.Value(colour))}",
                $"the builder's text takes the option's {(disabled ? "muted" : "primary")} colour from the box around it");
        }
    }
}
