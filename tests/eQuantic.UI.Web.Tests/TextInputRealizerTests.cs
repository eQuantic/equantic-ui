using eQuantic.UI.Components;
using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
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
    public void TextEntry_LowersToARealChromelessInput_InsideTheStableFieldShell()
    {
        var node = Render(new TextEntry("ana@equantic")
        {
            Placeholder = "you@company.com",
        });

        // The .eq-field shell is STABLE: input first, the sr-only description twin second — the
        // twin exists (empty) even without a description, so an error appearing later swaps
        // attributes instead of replacing the <input> (which would drop focus mid-edit).
        node.Tag.Should().Be("div");
        node.Attributes["class"].Should().Be("eq-field");
        node.Children.Should().HaveCount(2);

        var input = node.Children[0];
        input.Tag.Should().Be("input");
        input.Attributes["class"].Should().Be("eq-entry eq-type-bodyl");
        input.Attributes["type"].Should().Be("text");
        input.Attributes["value"].Should().Be("ana@equantic");
        input.Attributes["placeholder"].Should().Be("you@company.com");
        input.Attributes["style"].Should().Be(
            $"width: 100%; padding: 0; background: none; border: none; " +
            $"color: {TokenCss.Value(Theme.TextPrimary)}; font-family: inherit; " +
            // The field declares itself a hit target: `pointer-events: none` inherits from any
            // transparent row above it, and a field that cannot be clicked cannot be typed into.
            "pointer-events: auto");

        var description = node.Children[1];
        description.Tag.Should().Be("span");
        description.Attributes["class"].Should().Be("eq-desc");
        description.Attributes["aria-live"].Should().Be("polite");
        description.Attributes.Should().NotContainKey("id", "no description was authored");
        input.Attributes.Should().NotContainKey("aria-describedby");
    }

    [Fact]
    public void TextEntry_Description_IsAssociated_AndAnnouncesSwaps()
    {
        var node = Render(new TextEntry("") { Description = "We never share it." });

        var input = Find(node, n => n.Tag == "input")!;
        var description = Find(node, n => n.Tag == "span")!;
        var id = description.Attributes["id"];
        id.Should().StartWith("eq-desc-");
        input.Attributes["aria-describedby"].Should().Be(id);
        description.Attributes["aria-live"].Should().Be("polite");
        SpanText(description).Should().Be("We never share it.");
        input.Attributes.Should().NotContainKey("aria-invalid", "a described field is not thereby invalid");
    }

    [Fact]
    public void TextEntry_Invalid_IsAState_NeverWordedIntoTheName()
    {
        var node = Render(new TextEntry("nope") { Label = "Email", Invalid = true });

        var input = Find(node, n => n.Tag == "input")!;
        input.Attributes["aria-invalid"].Should().Be("true");
        input.Attributes["aria-label"].Should().Be("Email", "the name stays the label — state is a state");
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

        var textarea = node.Children[0];
        textarea.Tag.Should().Be("textarea");
        textarea.Attributes["rows"].Should().Be("5");
        textarea.Attributes.Should().NotContainKey("value");
        textarea.Attributes["placeholder"].Should().Be("Describe your request…");
        textarea.Children.Single(c => c.Tag == "#text").TextContent.Should().Be("Tell us about the project");
        textarea.Attributes["style"].Should().Contain("resize: vertical");
    }

    [Fact]
    public void TextEntry_Obscure_And_Disabled_MapToInputSemantics()
    {
        var password = Find(Render(new TextEntry("secret") { Obscure = true }), n => n.Tag == "input")!;
        password.Attributes["type"].Should().Be("password");

        var disabled = Find(Render(new TextEntry("") { Disabled = true }), n => n.Tag == "input")!;
        disabled.Attributes.Should().ContainKey("disabled");
    }

    [Fact]
    public void GeneratedStylesheet_CarriesTheEntryMechanics()
    {
        var css = PhotonCssGenerator.Generate(Theme);
        css.Should().Contain(".eq-entry { outline: none; }",
            "the container shows focus — the input itself is chrome-less");
        css.Should().Contain(".eq-entry::placeholder { color: var(--eq-color-text-muted); }");
        css.Should().Contain(".eq-desc { position: absolute;",
            "the description twin is clipped, not display:none — hidden targets read for " +
            "describedby but only a rendered one announces as a live region");
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

        // Two spans now carry the error text — the VISIBLE caption and the entry's sr-only twin.
        // The visible one is the one wearing the destructive color.
        var caption = Find(node, n =>
            n.Tag == "span" && SpanText(n) == "Enter a valid email address."
            && n.Attributes.GetValueOrDefault("class") != "eq-desc")!;
        caption.Attributes["style"].Should().Contain($"color: {destructive}");
    }

    [Fact]
    public void TextInput_NamesItsInput_AndWiresTheErrorAsInvalidPlusDescription()
    {
        var node = Render(new TextInput("", label: "Email", error: "Enter a valid email address."));

        var input = Find(node, n => n.Tag == "input")!;
        input.Attributes["aria-label"].Should().Be("Email",
            "the visible label is a sibling span the input cannot reference — the entry restates it");
        input.Attributes["aria-invalid"].Should().Be("true");

        var descriptionId = input.Attributes["aria-describedby"];
        var description = Find(node, n =>
            n.Tag == "span" && n.Attributes.GetValueOrDefault("id") == descriptionId)!;
        description.Should().NotBeNull();
        description.Attributes["class"].Should().Be("eq-desc");
        description.Attributes["aria-live"].Should().Be("polite");
        SpanText(description).Should().Be("Enter a valid email address.",
            "what the screen reader hears is exactly what the sighted user reads");
    }

    [Fact]
    public void TextInput_Helper_IsDescribed_WithoutClaimingInvalid()
    {
        var node = Render(new TextInput("", label: "Email", helper: "We never share it."));

        var input = Find(node, n => n.Tag == "input")!;
        input.Attributes.Should().ContainKey("aria-describedby");
        input.Attributes.Should().NotContainKey("aria-invalid", "helper text is not an error");
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
        input.Attributes["aria-label"].Should().Be("Search…",
            "the pill has no visible label — the placeholder text is promoted to the real name");
    }
}
