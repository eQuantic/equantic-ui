using eQuantic.UI.Email;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Email.Tests;

/// <summary>
/// The email realizer's contract, written against what M0 measured. The web lowering already
/// produces inline styles, and they are the WRONG inline styles for this medium: flex layout
/// (Outlook renders with Word's engine — no flexbox, no gap), <c>light-dark()</c> colors (no email
/// client resolves the function), and typography living in <c>eq-type-*</c> classes (an email has
/// no stylesheet, so every text came out the same size).
/// <para>
/// So the contract here is stated as three prohibitions and their replacements: tables instead of
/// flex, one literal color instead of a function, the theme's type ramp inlined on every text.
/// </para>
/// </summary>
public class EmailRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static string Html(VisualNode node) => EmailRealizer.Lower(node, Theme);

    private static Column EmailShape()
    {
        var actions = new Row(gap: Space.S2);
        actions.Add(new Text("Confirm", TypeRole.Label));
        actions.Add(new Text("Later", TypeRole.Label));

        var email = new Column(gap: Space.S4);
        email.Add(new Text("Welcome", TypeRole.Heading));
        email.Add(new Text("Your account is ready.", TypeRole.BodyM));
        email.Add(actions);
        email.Add(new Text("eQuantic", TypeRole.Caption));
        return email;
    }

    [Fact]
    public void AColumnIsATableWithOneRowPerChild()
    {
        var html = Html(EmailShape());

        // Outlook on Windows renders with Word's engine: no flexbox, no grid, no gap. Layout that
        // survives every client is the layout email has always had — nested tables.
        html.Should().Contain("<table").And.NotContain("display: flex").And.NotContain("display:flex");
    }

    [Fact]
    public void ARowLaysItsChildrenOutAsCellsOnOneRow()
    {
        var row = new Row(gap: Space.S2);
        row.Add(new Text("a", TypeRole.Label));
        row.Add(new Text("b", TypeRole.Label));

        var html = Html(row);

        // One <tr>, two content cells.
        html.Split("<tr").Length.Should().Be(2, "a Row is exactly one table row");
        html.Split("<td").Length.Should().BeGreaterThanOrEqualTo(3, "one cell per child");
    }

    [Fact]
    public void EveryColorIsALiteralNotAFunction()
    {
        var html = Html(EmailShape());

        // light-dark() is what the web emits and what no email client resolves. Email renders in
        // ONE mode; the theme's light leg is that mode.
        html.Should().NotContain("light-dark(").And.NotContain("var(--");
        html.Should().Contain("#171b21", "the heading's ink is the theme's light text color, spelled out");
    }

    [Fact]
    public void TypographyIsInlinedFromTheThemesRamp()
    {
        var html = Html(EmailShape());

        // M0's new finding: the web keeps type in eq-type-* classes, and an email has no stylesheet
        // to put them in — every text rendered the same size. The ramp inlines instead.
        html.Should().NotContain("class=");
        var heading = Theme.Type(TypeRole.Heading);
        var body = Theme.Type(TypeRole.BodyM);
        html.Should().Contain($"font-size: {(int)heading.Size}px");
        html.Should().Contain($"font-size: {(int)body.Size}px");
        html.Should().Contain($"font-weight: {(int)heading.Weight}");
    }

    [Fact]
    public void AGapIsASpacerTheWordEngineUnderstands()
    {
        var html = Html(EmailShape());

        // `gap` does not exist in Outlook. A Column's gap is a spacer ROW with an explicit height;
        // it must appear BETWEEN children (3 gaps for 4 children), never after the last. The probe
        // is the whole spacer-cell opening, because a bare "height: 16px" also matches inside the
        // texts' line-height and counted them.
        html.Split("<tr><td style=\"height: 16px").Length.Should().Be(4, "three spacers between four children");
        html.Should().NotContain("display: flex").And.NotContain("gap:");
    }

    [Fact]
    public void ABoxPaintsItsBackgroundAndPadding()
    {
        var box = new Box(new BoxStyle
        {
            Background = Theme.Surface,
            Padding = EdgeInsets.All(Space.S4),
        });

        var html = Html(box);

        html.Should().Contain("background-color: #").And.Contain("padding: 16px");
    }

    [Fact]
    public void AnUnrealizableNodeFailsLoudAndNamesItself()
    {
        // The repo's rule: anything the SDK cannot realize faithfully is an ERROR, never a silent
        // divergence. A ScrollView in an email is not a smaller ScrollView — it is nothing.
        var scroll = new ScrollView(new Text("content", TypeRole.BodyM));

        var act = () => Html(scroll);

        act.Should().Throw<NotSupportedException>().WithMessage("*ScrollView*email*");
    }
}

/// <summary>
/// The document: what wraps the lowered tree so a client renders it — and the text alternative,
/// generated from the SAME tree so it cannot drift from the HTML.
/// </summary>
public class EmailRendererTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private sealed class WelcomeEmail : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var email = new Column(gap: Space.S4);
            email.Add(new Text("Welcome", TypeRole.Heading));
            email.Add(new Text("Your account is ready.", TypeRole.BodyM));
            return email;
        }
    }

    [Fact]
    public void TheDocumentIsSelfContainedAndConstrained()
    {
        var message = EmailRenderer.Render(new WelcomeEmail(), Theme);

        // The shell every client tolerates: doctype, one centered table at the conventional width,
        // a solid page background. No <style> block — one rule beats a matrix of client exceptions.
        message.Html.Should().StartWith("<!DOCTYPE html");
        message.Html.Should().Contain("width: 600px");
        message.Html.Should().NotContain("<style");
    }

    [Fact]
    public void ThePlainTextAlternativeComesFromTheSameTree()
    {
        var message = EmailRenderer.Render(new WelcomeEmail(), Theme);

        message.PlainText.Should().Contain("Welcome").And.Contain("Your account is ready.");
        message.PlainText.Should().NotContain("<", "it is text, not markup");
    }

    [Fact]
    public void ThePlainTextCarriesLinksAndImageAlts()
    {
        var tree = new Column(gap: Space.S4);
        tree.Add(new Image("https://cdn.example.com/logo.png", 132, 26, alt: "eQuantic"));
        tree.Add(new Link("https://example.com/confirm", new Text("Confirm your account", TypeRole.Label)));

        var message = EmailRenderer.Render(tree, Theme);

        // The URL is the only ACTIONABLE thing in most transactional mail — a text alternative
        // without it is a message the reader cannot act on. The convention: label, then address.
        message.PlainText.Should().Contain("Confirm your account: https://example.com/confirm");
        message.PlainText.Should().Contain("eQuantic");
    }

    [Fact]
    public void APreheaderRidesInvisiblyWhenGiven()
    {
        var message = EmailRenderer.Render(new WelcomeEmail(), Theme, preheader: "Your account is ready");

        // The inbox preview line: present in the HTML, hidden in the rendering.
        message.Html.Should().Contain("Your account is ready").And.Contain("display: none");
    }
}

/// <summary>
/// Images and links — the two nodes where email's rules are about ADDRESSES, not layout: the
/// message is opened with no origin to resolve a relative URL against, and Gmail drops data: URIs.
/// </summary>
public class EmailMediaTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static string Html(VisualNode node) => EmailRealizer.Lower(node, Theme);

    [Fact]
    public void AnImageCarriesItsAddressSizeAndAlt()
    {
        var html = Html(new Image("https://cdn.example.com/logo.png", 132, 26, alt: "eQuantic"));

        // Width/height as ATTRIBUTES, not only CSS: Word's engine sizes images from the attributes.
        html.Should().Contain("src=\"https://cdn.example.com/logo.png\"")
            .And.Contain("width=\"132\"").And.Contain("height=\"26\"")
            .And.Contain("alt=\"eQuantic\"");
        // display:block kills the mystery baseline gap every mail client adds under inline images.
        html.Should().Contain("display: block");
    }

    [Fact]
    public void ARelativeOrDataImageIsAnErrorNotABrokenPicture()
    {
        // The reader's client opens this with NO ORIGIN: /brand.svg points nowhere, and Gmail
        // strips data: URIs. A broken picture in someone's inbox is not a rendering choice.
        var relative = () => Html(new Image("/brand.svg", 132, 26));
        var data = () => Html(new Image("data:image/png;base64,AAAA", 10, 10));

        relative.Should().Throw<NotSupportedException>().WithMessage("*absolute*");
        data.Should().Throw<NotSupportedException>().WithMessage("*data:*");
    }

    [Fact]
    public void TheDarkArtworkIsResolvedTheSameWayColorsAre()
    {
        // One mode, decided once: colors take the light leg, artwork takes the light source.
        var html = Html(new Image("https://cdn.example.com/logo.png", 132, 26)
        {
            DarkSource = "https://cdn.example.com/logo-white.png",
        });

        html.Should().Contain("logo.png").And.NotContain("logo-white.png");
    }

    [Fact]
    public void ATextLinkIsAnUnderlinedAnchor()
    {
        var html = Html(new Link("https://example.com/confirm", new Text("Confirm", TypeRole.Label)));

        // A text link must LOOK like a link: there is no hover in this medium to reveal it.
        html.Should().Contain("<a href=\"https://example.com/confirm\"")
            .And.Contain("text-decoration: underline");
    }

    [Fact]
    public void ABoxLinkIsTheBulletproofButton()
    {
        var button = new Link("https://example.com/confirm",
            new Box(new BoxStyle
            {
                Background = Theme.Colors(Variant.Primary).Base,
                Padding = EdgeInsets.Symmetric(Space.S4, Space.S2),
            }, new Text("Confirm", TypeRole.Label, Theme.Colors(Variant.Primary).OnBase)));

        var html = Html(button);

        // The button pattern: the anchor wraps the painted table, and is NOT underlined — the box
        // is the affordance. display:inline-block so the anchor takes the box's size.
        html.Should().Contain("<a href=").And.Contain("text-decoration: none")
            .And.Contain("background-color: #");
    }

    [Fact]
    public void ARelativeLinkIsAnErrorToo()
    {
        var act = () => Html(new Link("/confirm", new Text("Confirm", TypeRole.Label)));

        act.Should().Throw<NotSupportedException>().WithMessage("*absolute*");
    }

    [Fact]
    public void MailtoIsAValidDestination()
    {
        var html = Html(new Link("mailto:support@equantic.dev", new Text("Support", TypeRole.Label)));

        html.Should().Contain("href=\"mailto:support@equantic.dev\"");
    }
}
