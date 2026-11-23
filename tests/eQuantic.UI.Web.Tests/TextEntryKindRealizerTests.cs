using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// What a <see cref="TextEntry"/>'s kind says to the browser on the server's side (#1761): the keyboard a
/// phone opens, the autofill offered and that a value is not prose, and a <see cref="TextInput"/>'s prefix and
/// suffix. Cross-pinned by text-input.spec.ts, which lowers the same entries in the browser's twin, and by the
/// component parity fixture.
/// </summary>
public class TextEntryKindRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Input(TextEntry entry) => WebRealizer.Lower(entry, Theme).Render().Children[0];

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static string Text(HtmlNode span) =>
        span.Children.FirstOrDefault(c => c.Tag == "#text")?.TextContent ?? "";

    public static TheoryData<TextEntryKind, string, string?, string?> Kinds => new()
    {
        { TextEntryKind.Email, "text", "email", "email" },
        { TextEntryKind.Phone, "tel", "tel", "tel" },
        { TextEntryKind.Url, "text", "url", "url" },
        { TextEntryKind.Number, "text", "numeric", null },
        { TextEntryKind.Decimal, "text", "decimal", null },
    };

    [Theory]
    [MemberData(nameof(Kinds))]
    public void AKind_OpensItsKeyboard_OffersItsAutofill_AndIsNoProse(
        TextEntryKind kind, string type, string? mode, string? autofill)
    {
        var input = Input(new TextEntry("") { Kind = kind });

        input.Attributes["type"].Should().Be(type);
        input.Attributes.GetValueOrDefault("inputmode").Should().Be(mode);
        input.Attributes.GetValueOrDefault("autocomplete").Should().Be(autofill);
        input.Attributes["spellcheck"].Should().Be("false");
        input.Attributes["autocapitalize"].Should().Be("none");
    }

    [Fact]
    public void Text_SaysNothingOfAKind()
    {
        var input = Input(new TextEntry("hello"));

        input.Attributes["type"].Should().Be("text");
        input.Attributes.Should().NotContainKeys("inputmode", "autocomplete", "spellcheck", "autocapitalize");
    }

    /// <summary>The selection API refuses `type="email"` and `type="number"` (`setSelectionRange` throws), and
    /// the client places an autofocused entry's caret with it: the kind is the keyboard's, never the type's.</summary>
    [Fact]
    public void NoKind_TakesATypeTheSelectionApiRefuses()
    {
        foreach (var kind in Enum.GetValues<TextEntryKind>())
            Input(new TextEntry("") { Kind = kind }).Attributes["type"].Should().BeOneOf("text", "tel");
    }

    [Fact]
    public void AnObscuredNumber_IsAPassword_OnTheDigits_WithNoAutofill()
    {
        var input = Input(new TextEntry("") { Kind = TextEntryKind.Number, Obscure = true });
        input.Attributes["type"].Should().Be("password");
        input.Attributes["inputmode"].Should().Be("numeric");

        var phone = Input(new TextEntry("") { Kind = TextEntryKind.Phone, Obscure = true });
        phone.Attributes["type"].Should().Be("password");
        phone.Attributes["inputmode"].Should().Be("tel");
        phone.Attributes.Should().NotContainKey("autocomplete");
    }

    [Fact]
    public void AMultiLineEntry_IsProse_WhateverItsKind()
    {
        var area = Input(new TextEntry("") { Kind = TextEntryKind.Email, Lines = 3 });
        area.Tag.Should().Be("textarea");
        area.Attributes.Should().NotContainKeys("type", "inputmode", "autocomplete", "spellcheck");
    }

    [Fact]
    public void ATextInput_HandsItsKindToTheEntry()
    {
        var tree = WebRealizer.Lower(new TextInput("", null, "E-mail", kind: TextEntryKind.Email), Theme).Render();
        var input = Walk(tree).Single(n => n.Tag == "input");
        input.Attributes["inputmode"].Should().Be("email");
        input.Attributes["aria-label"].Should().Be("E-mail");
    }

    [Fact]
    public void APrefixAndASuffix_AreDrawnAroundTheEntry_AndNameIt()
    {
        var tree = WebRealizer.Lower(new TextInput("12", null, "Price", prefix: "€", suffix: "per kg"), Theme).Render();

        var input = Walk(tree).Single(n => n.Tag == "input");
        input.Attributes["aria-label"].Should().Be("Price (€ per kg)");

        // In the row that holds the entry: the prefix before its field, the suffix after it.
        var (row, field) = AmountDriver.Around(tree);
        var texts = row.Children.Select(c => Walk(c).Where(n => n.Tag == "span").Select(Text).FirstOrDefault(t => t.Length > 0)).ToList();
        texts.Should().HaveCount(3);
        texts[field - 1].Should().Be("€");
        texts[field + 1].Should().Be("per kg");
    }

    [Fact]
    public void WithNoAffix_TheNameIsTheLabel_AndWithNoLabel_ThereIsNone()
    {
        Walk(WebRealizer.Lower(new TextInput("", null, "Name"), Theme).Render())
            .Single(n => n.Tag == "input").Attributes["aria-label"].Should().Be("Name");
        Walk(WebRealizer.Lower(new TextInput("", null, "", suffix: "g"), Theme).Render())
            .Single(n => n.Tag == "input").Attributes.Should().NotContainKey("aria-label");
    }
}
