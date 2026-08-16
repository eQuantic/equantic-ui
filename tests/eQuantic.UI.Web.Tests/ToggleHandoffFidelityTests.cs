using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A13 IconButton and B8 Chip: a TOGGLE says so.
/// <para>
/// Both had a <c>Selected</c> property that changed the paint — a swapped glyph, a Primary tint, a
/// Subtle fill — and stopped there. `Pressable.Selected` is the only route to <c>aria-pressed</c>
/// on either producer, and neither handed it over, so the state was the whole story for anyone who
/// could see it and nothing at all for anyone who could not. The handoff asks for it in words:
/// "role toggle button, announces selected" (A13) and "Filter = toggle button, 'Income, filter,
/// selected'" (B8).
/// </para>
/// </summary>
public class ToggleHandoffFidelityTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static HtmlNode Control(VisualNode node) =>
        Walk(WebRealizer.Lower(node, Theme).Render())
            .First(candidate => candidate.Attributes.ContainsKey("aria-label"));

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void AToggleIconButtonStatesItsState(bool selected, string pressed)
    {
        var control = Control(new IconButton(Icons.Heart, "Favourite", onPressed: () => { })
        {
            Selected = selected,
            SelectedGlyph = Icons.HeartFilled,
        });

        control.Attributes["aria-pressed"].Should().Be(pressed);
        control.Attributes.GetValueOrDefault("aria-label", "").Should().Be("Favourite",
            "the NAME is what the control is for — it must not change with the state");
    }

    /// <summary>
    /// A plain IconButton does not toggle, and must not claim to. "Not pressed" on every icon button
    /// in an app is noise, and it is a different claim from "toggles, currently off".
    /// </summary>
    [Fact]
    public void APlainIconButtonSaysNothingAboutSelection()
    {
        var control = Control(new IconButton(Icons.Search, "Search", onPressed: () => { }));

        control.Attributes.Should().NotContainKey("aria-pressed");
    }

    /// <summary>A filter chip ALWAYS carries selection — that is what makes it a filter — so false is
    /// the honest answer for an unselected one rather than silence.</summary>
    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void AFilterChipStatesItsState(bool selected, string pressed)
    {
        var control = Control(new Chip("Income", ChipKind.Filter, selected, () => { }));

        control.Attributes["aria-pressed"].Should().Be(pressed);
        control.Attributes.GetValueOrDefault("aria-label", "").Should().Be("Income");
    }

    /// <summary>A Tag is an annotation, not a control: it is not pressable at all, so there is no
    /// state to announce and nothing to tab to.</summary>
    [Fact]
    public void ATagChipIsNotAControl()
    {
        var html = WebRealizer.Lower(new Chip("Crypto", ChipKind.Tag), Theme).Render();

        Walk(html).Should().NotContain(node => node.Attributes.ContainsKey("aria-pressed"));
        Walk(html).Should().NotContain(node => node.Tag == "button");
    }
}
