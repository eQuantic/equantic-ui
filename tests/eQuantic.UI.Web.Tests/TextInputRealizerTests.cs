using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The text-entry web half (spec B9/B10): the primitive lowers to a REAL chrome-less &lt;input&gt;
/// (the browser owns caret/selection/IME) with the type role riding the generated class; the
/// TextInput container carries the B9 frame per state (SSR renders the at-rest state; the focused
/// 2dp-Primary rebuild is the component's INTERNAL state, exercised in the vitest E2E). Cross-pinned
/// with text-input.spec.ts.
/// </summary>
public class TextInputRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    private static HtmlNode? Find(HtmlNode node, Func<HtmlNode, bool> match)
    {
        if (match(node)) return node;
        foreach (var child in node.Children)
        {
            if (Find(child, match) is { } found) return found;
        }
        return null;
    }

    /// <summary>Text lowers as a span wrapping a #text child — this reads the effective content.</summary>
    private static string SpanText(HtmlNode span) =>
        span.Children.FirstOrDefault(c => c.Tag == "#text")?.TextContent ?? "";

    [Fact]
    public void TextEntry_LowersToARealChromelessInput()
    {
        var node = Render(new TextEntry("ana@equantic")
        {
            Placeholder = "you@company.com",
        });

        node.Tag.Should().Be("input");
        node.Attributes["class"].Should().Be("eq-entry eq-type-bodyl");
        node.Attributes["type"].Should().Be("text");
        node.Attributes["value"].Should().Be("ana@equantic");
        node.Attributes["placeholder"].Should().Be("you@company.com");
        node.Attributes["style"].Should().Be(
            $"width: 100%; padding: 0; background: none; border: none; " +
            $"color: {TokenCss.Value(Theme.TextPrimary)}; font-family: inherit");
    }

    [Fact]
    public void TextEntry_WithSeveralLines_LowersToATextareaCarryingItsValueAsContent()
    {
        // A message box is the same primitive with a line count — and a textarea's value is its
        // CONTENT (there is no value attribute), which is what SSR has to write for hydration.
        var node = Render(new TextEntry("Tell us about the project")
        {
            Lines = 5,
            Placeholder = "Describe your request…",
        });

        node.Tag.Should().Be("textarea");
        node.Attributes["rows"].Should().Be("5");
        node.Attributes.Should().NotContainKey("value");
        node.Attributes["placeholder"].Should().Be("Describe your request…");
        node.Children.Single(c => c.Tag == "#text").TextContent.Should().Be("Tell us about the project");
        node.Attributes["style"].Should().Contain("resize: vertical");
    }

    [Fact]
    public void TextEntry_Obscure_And_Disabled_MapToInputSemantics()
    {
        var password = Render(new TextEntry("secret") { Obscure = true });
        password.Attributes["type"].Should().Be("password");

        var disabled = Render(new TextEntry("") { Disabled = true });
        disabled.Attributes.Should().ContainKey("disabled");
    }

    [Fact]
    public void GeneratedStylesheet_CarriesTheEntryMechanics()
    {
        var css = PhotonCssGenerator.Generate(Theme);
        css.Should().Contain(".eq-entry { outline: none; }",
            "the container shows focus — the input itself is chrome-less");
        css.Should().Contain(".eq-entry::placeholder { color: var(--eq-color-text-muted); }");
    }

    [Fact]
    public void TextInput_AtRest_CarriesTheB9Frame()
    {
        var node = Render(new TextInput("", label: "Email", placeholder: "you@company.com",
            helper: "We never share it.", leading: Icons.Mail));

        var container = Find(node, n =>
            n.Tag == "div" && n.Attributes.TryGetValue("style", out var s) && s!.Contains("border:"))!;
        container.Attributes["style"].Should().Contain("height: 48px", "Large is the default");
        container.Attributes["style"].Should().Contain(
            $"border: 1px solid {TokenCss.Value(Theme.BorderStrong)}");
        container.Attributes["style"].Should().Contain("border-radius: 10px");
        container.Attributes["style"].Should().Contain("padding: 0 14px 0 14px");

        Find(node, n => n.Tag == "input").Should().NotBeNull();

        // Label above, helper below — the caption line exists even when helper text is present.
        var spans = new List<HtmlNode>();
        void Collect(HtmlNode n) { if (n.Tag == "span") spans.Add(n); foreach (var c in n.Children) Collect(c); }
        Collect(node);
        spans.Select(SpanText).Should().Contain(new[] { "Email", "We never share it." });
    }

    [Fact]
    public void TextInput_Error_SwapsBorderAndCaptionToDestructive_KeepingTheLine()
    {
        var node = Render(new TextInput("", label: "Email", error: "Enter a valid email address."));

        var destructive = TokenCss.Value(Theme.Colors(Variant.Destructive).Base);
        var container = Find(node, n =>
            n.Tag == "div" && n.Attributes.TryGetValue("style", out var s) && s!.Contains("border:"))!;
        container.Attributes["style"].Should().Contain($"border: 1px solid {destructive}");

        var caption = Find(node, n =>
            n.Tag == "span" && SpanText(n) == "Enter a valid email address.")!;
        caption.Attributes["style"].Should().Contain($"color: {destructive}");
    }

    [Fact]
    public void SearchField_IsTheBorderlessPill_WithClearOnlyWhenNonEmpty()
    {
        var empty = Render(new SearchField(""));
        empty.Attributes["style"].Should().Contain("height: 40px");
        empty.Attributes["style"].Should().Contain($"background-color: {TokenCss.Value(Theme.SurfaceSubtle)}");
        empty.Attributes["style"].Should().Contain("border-radius: 999px");
        empty.Attributes["style"].Should().NotContain("border:", "no border (E0)");

        var buttons = 0;
        void Count(HtmlNode n) { if (n.Tag == "button") buttons++; foreach (var c in n.Children) Count(c); }
        Count(empty);
        buttons.Should().Be(0, "no clear button while empty");

        var withQuery = Render(new SearchField("rio"));
        Count(withQuery);
        buttons.Should().Be(1, "the clear button appears when non-empty");

        var input = Find(withQuery, n => n.Tag == "input")!;
        input.Attributes["class"].Should().Be("eq-entry eq-type-bodym", "SearchField rides BodyM (15/400)");
    }
}
