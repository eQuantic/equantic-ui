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

/// <summary>The review's ten, each pinned — most were silent losses, two were injection holes.</summary>
public class EmailReviewFindingsTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static string Html(VisualNode node) => EmailRealizer.Lower(node, Theme);

    [Fact]
    public void AttributeValuesCannotBreakOutOfTheirQuotes()
    {
        var html = Html(new Image("https://cdn.example.com/a.png?w=1&h=2", 10, 10,
            alt: "he said \"hi\" & left"));

        html.Should().Contain("a.png?w=1&amp;h=2", "a bare & is malformed HTML some clients rewrite");
        html.Should().Contain("&quot;hi&quot;", "a quote would end the attribute and inject markup");
        html.Should().NotContain("alt=\"he said \"");
    }

    [Fact]
    public void AMailtoImageIsAnErrorNotABrokenPicture()
    {
        var act = () => Html(new Image("mailto:x@y.z", 10, 10));

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void FractionalSizesSurviveInvariantly()
    {
        var html = Html(new Text("x", styleOverride: new TypeStyle(13.5f, 20, FontWeight.Regular, 0.25f, 1f)));

        // (int) truncation turned 13.5 into 13 — and a pt-PT machine would print 13,5, which CSS
        // cannot read. Invariant, fraction-preserving.
        html.Should().Contain("font-size: 13.5px").And.Contain("letter-spacing: 0.25px");
    }

    [Fact]
    public void RunsKeepTheirEmphasisColorAndLink()
    {
        var text = new Text("")
        {
            Spans =
            [
                new TextRun("plain "),
                new TextRun("bold") { Weight = FontWeight.Bold },
                new TextRun(" and "),
                new TextRun("a link") { Destination = "https://example.com/x" },
            ],
        };

        var html = Html(text);

        // Flattening to PlainContent silently dropped every run's emphasis — and turned a LINKED
        // run into text the reader cannot act on, which in a transactional mail is the content.
        html.Should().Contain("font-weight: 700").And.Contain("href=\"https://example.com/x\"");
        html.Should().Contain(">bold<").And.Contain(">a link<");
    }

    [Fact]
    public void ATranslucentTokenIsFlattenedNotEightDigitHex()
    {
        var translucent = new ColorToken(Color.FromRgba(0, 0, 0, 128));
        var html = Html(new Text("x", color: translucent));

        // #RRGGBBAA is CSS Color 4 — Word's engine does not read it, and the ink would be LOST
        // there, not dimmed. Flattened over the theme's surface instead: a mid-gray, six digits.
        html.Should().NotContainAny("#00000080", "rgba(");
        html.Should().MatchRegex("color: #[0-9a-f]{6}[\";]");
    }

    [Fact]
    public void ANestedComponentExpandsInsteadOfThrowing()
    {
        var column = new Column(gap: Space.S2);
        column.Add(new Footer());

        var html = Html(column);

        html.Should().Contain("from a component");
    }

    private sealed class Footer : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
            => new Text("from a component", TypeRole.Caption);
    }

    [Fact]
    public void MonoItalicTrackingAndAlignmentReachTheInlineStyle()
    {
        var html = Html(new Text("42", TypeRole.BodyM, mono: true, tabular: true, align: TextAlignment.Center));

        html.Should().Contain("monospace").And.Contain("tabular-nums").And.Contain("text-align: center");
    }

    [Fact]
    public void ContainScalesByWidthAndLetsHeightFollow()
    {
        var html = Html(new Image("https://cdn.example.com/a.png", 300, 200, ImageFit.Contain));

        // No object-fit exists in this medium. Contain = whole source visible: width pinned,
        // height auto, the box's height follows the image instead of cropping it.
        html.Should().Contain("height: auto").And.NotContain("height=\"200\"");
    }
}

/// <summary>Renderer-level findings: the shell constant and the plain-text gap.</summary>
public class EmailRendererReviewTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    [Fact]
    public void ThePlainTextSeparatesSectionsLikeTheHtmlDoes()
    {
        var tree = new Column(gap: Space.S4);
        tree.Add(new Text("First section", TypeRole.Heading));
        tree.Add(new Text("Second section", TypeRole.BodyM));

        var message = EmailRenderer.Render(tree, Theme);

        // The gap that separates sections in the HTML separates them here too — the documented
        // contract the walker was ignoring.
        message.PlainText.Should().Contain("First section\n\nSecond section");
    }
}

/// <summary>The second review wave, pinned — plus the one claim that was wrong, kept as a note.</summary>
public class EmailSecondWaveTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static string Html(VisualNode node) => EmailRealizer.Lower(node, Theme);

    [Fact]
    public void TheNodeLevelItalicFlagSlants()
        => Html(new Text("x", TypeRole.BodyM) { Italic = true }).Should().Contain("font-style: italic");

    [Fact]
    public void ARunsSizeOverrideIsItsOwn()
    {
        var text = new Text("")
        {
            Spans = [new TextRun("code") { Mono = true, StyleOverride = new TypeStyle(13.5f, 20, FontWeight.Regular, 0, 1f) }],
        };

        Html(text).Should().Contain("font-size: 13.5px");
    }

    [Fact]
    public void AuthoredLineBreaksSurviveAsBr()
        // HTML collapses a raw newline to a space, and white-space is exactly the kind of CSS
        // Word ignores — the break becomes an element.
        => Html(new Text("one\ntwo", TypeRole.BodyM)).Should().Contain("one<br>two");

    [Fact]
    public void PerCornerRadiiKeepTheirShape()
    {
        var box = new Box(new BoxStyle { CornerRadius = new CornerRadii(8, 8, 0, 0) });

        Html(box).Should().Contain("border-radius: 8px 8px 0px 0px");
    }

    [Fact]
    public void ABorderedBoxCarriesItsOutline()
    {
        var card = new Box(new BoxStyle { BorderWidth = 1, BorderColor = Theme.Border });
        var divider = new Box(new BoxStyle { BorderWidth = 1, BorderColor = Theme.Border, BorderSides = BorderSides.Top });

        Html(card).Should().MatchRegex("border: 1px solid #[0-9a-f]{6}");
        Html(divider).Should().Contain("border-top: 1px").And.NotContain("border: 1px");
    }

    [Fact]
    public void RowCrossAlignmentReachesTheCells()
    {
        var row = new Row(gap: 0, cross: CrossAlign.End);
        row.Add(new Text("x", TypeRole.BodyM));

        Html(row).Should().Contain("vertical-align: bottom");
    }

    [Fact]
    public void AHeadingLevelKeepsItsElement()
        => Html(new Text("Title", TypeRole.Heading, headingLevel: 2))
            .Should().Contain("<h2 ").And.Contain("margin: 0").And.Contain("</h2>");

    [Fact]
    public void ALinkLabelNamesTheAnchor()
    {
        var link = new Link("https://example.com/x",
            new Image("https://cdn.example.com/logo.png", 10, 10)) { Label = "Open the dashboard" };

        Html(link).Should().Contain("aria-label=\"Open the dashboard\"");
    }

    [Fact]
    public void AHostlessAddressFailsTheParse()
    {
        // "https://" alone passed the old prefix check and became a guaranteed-broken href.
        var act = () => Html(new Link("https://", new Text("x", TypeRole.BodyM)));

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ALinkAroundLinkedRunsRefusesToNestAnchors()
    {
        var text = new Text("") { Spans = [new TextRun("inner") { Destination = "https://example.com/a" }] };
        var act = () => Html(new Link("https://example.com/b", text));

        act.Should().Throw<NotSupportedException>().WithMessage("*nest*");
    }

    [Fact]
    public void AThrowingComponentFailsTheSendNotTheReader()
    {
        var column = new Column(gap: 0);
        column.Add(new Exploding());

        // The web catches a component's throw and renders a describe-box in its place — right on a
        // live page a developer is watching, wrong in a message about to be SENT. Email propagates:
        // a broken component fails the send, it never reaches an inbox dressed as content.
        var act = () => Html(column);

        act.Should().Throw<InvalidOperationException>().WithMessage("*broken on purpose*");
    }

    private sealed class Exploding : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
            => throw new InvalidOperationException("broken on purpose");
    }

    [Fact]
    public void TheShellTableCarriesTheWidthAttribute()
        => EmailRenderer.Render(new Text("x", TypeRole.BodyM), Theme)
            .Html.Should().Contain("width=\"600\"");

    [Fact]
    public void ALinkedRunKeepsItsAddressInPlainText()
    {
        var text = new Text("")
        {
            Spans = [new TextRun("Reset your password") { Destination = "https://example.com/reset" }],
        };

        EmailRenderer.Render(text, Theme).PlainText
            .Should().Contain("Reset your password (https://example.com/reset)");
    }
}

/// <summary>Third wave: shared properties this medium cannot express are fenced or honoured — and
/// the one that is VACUOUS here is documented as such, not fenced.</summary>
public class EmailThirdWaveTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static string Html(VisualNode node) => EmailRealizer.Lower(node, Theme);

    [Fact]
    public void GradientInkIsFencedNotApproximated()
    {
        var act = () => Html(new Text("Hero", TypeRole.Heading)
        {
            Gradient = new LinearGradient(Theme.Colors(Variant.Primary).Base, Theme.Colors(Variant.Info).Base),
        });

        // A solid stand-in would be a different ink nobody chose.
        act.Should().Throw<NotSupportedException>().WithMessage("*Gradient*");
    }

    [Fact]
    public void MaxLinesIsFencedBecauseNoClientClamps()
    {
        var act = () => Html(new Text("long", TypeRole.BodyM, maxLines: 2));

        // Showing MORE than the author bounded is a content divergence, not a style one.
        act.Should().Throw<NotSupportedException>().WithMessage("*MaxLines*");
    }

    [Fact]
    public void ContainerPaddingWrapsInTheOneCellShell()
    {
        var column = new Column(gap: 0, padding: EdgeInsets.All(24));
        column.Add(new Text("x", TypeRole.BodyM));

        Html(column).Should().Contain("padding: 24px");
    }

    [Fact]
    public void AlignSelfOverridesTheColumnsCrossForThatChildAlone()
    {
        var column = new Column(gap: 0);
        column.Add(new Text("start", TypeRole.BodyM));
        column.Add(new Text("end", TypeRole.BodyM) { AlignSelf = CrossAlign.End });

        var html = Html(column);

        // Each child owns a cell, so the medium expresses the override for free: one right-aligned
        // cell, and the sibling untouched.
        html.Split("align=\"right\"").Length.Should().Be(2, "exactly the overriding child's cell");
    }

    [Fact]
    public void ColumnCrossAlignsTheCells()
    {
        var column = new Column(gap: 0, cross: CrossAlign.Center);
        column.Add(new Text("x", TypeRole.BodyM));

        // Cross is REAL in a full-width column (a narrower child sits somewhere); Main is vacuous —
        // a content-sized table has no free space to distribute — and stays undeclared on purpose.
        Html(column).Should().Contain("align=\"center\"");
    }
}

/// <summary>Third wave, renderer half.</summary>
public class EmailRendererThirdWaveTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    [Fact]
    public void AnIconOnlyLinkFallsBackToItsLabelInPlainText()
    {
        var link = new Link("https://example.com/dash",
            new Image("https://cdn.example.com/gear.png", 16, 16)) { Label = "Open the dashboard" };

        var message = EmailRenderer.Render(link, Theme);

        // The same name the HTML carries as aria-label — the two alternatives must not drift.
        message.PlainText.Should().Contain("Open the dashboard: https://example.com/dash");
    }
}
