using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A link INSIDE a sentence, which nothing else in the vocabulary could express.
/// <para>
/// A <c>Link</c> around a <c>Text</c> makes the whole paragraph one link. A Row of Texts breaks
/// between RUNS rather than between words, so a sentence with three code spans wraps at the spans —
/// which is why mixed emphasis has to be one Text with Spans in the first place. Between those two,
/// a link mid-sentence had nowhere to live, and for anything that reads like prose that is most
/// paragraphs.
/// </para>
/// </summary>
public class InlineLinkTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static string Render(VisualNode node) =>
        HtmlRenderer.RenderNode(WebRealizer.Lower(node, Theme).Render());

    private static Text Sentence() => new("", TypeRole.BodyM)
    {
        Spans =
        [
            new TextRun("Read the "),
            new TextRun("getting started") { Weight = FontWeight.SemiBold, Destination = "/docs/start" },
            new TextRun(" guide, or the "),
            new TextRun("API", Mono: true) { Destination = "/api" },
            new TextRun(" reference."),
        ],
    };

    [Fact]
    public void ALinkedRun_BecomesAnAnchorCarryingItsHref()
    {
        var html = Render(Sentence());

        html.Should().Contain("href=\"/docs/start\"");
        html.Should().Contain("href=\"/api\"");
    }

    /// <summary>The runs that do NOT link stay spans — an anchor around plain prose would underline
    /// and colour text nobody can click.</summary>
    [Fact]
    public void UnlinkedRuns_StaySpans()
    {
        var html = Render(Sentence());

        // Five runs, two of them linked.
        System.Text.RegularExpressions.Regex.Matches(html, "<a ").Should().HaveCount(2);
        html.Should().Contain("Read the ").And.Contain(" reference.");
    }

    /// <summary>
    /// A linked run keeps its own emphasis. The whole reason this lives on the run is that a
    /// sentence mixes weights, faces and links in one flowing paragraph.
    /// </summary>
    [Fact]
    public void ALinkedRun_KeepsItsOwnEmphasis()
    {
        var html = Render(Sentence());

        html.Should().Contain("font-weight: 600", "the linked run was SemiBold");
        // The variable name, not the whole stack: the quotes inside it are HTML-escaped on the way
        // out, so comparing against the C# constant compares against a different string.
        html.Should().Contain("--eq-font-mono", "the linked API run was mono");
    }

    /// <summary>Everything still flows in ONE paragraph — the anchors are inline, so the line breaks
    /// between words rather than at the links.</summary>
    [Fact]
    public void TheParagraphStaysOneElement()
    {
        var html = Render(Sentence());

        html.Should().StartWith("<span").And.EndWith("</span>");
    }

    /// <summary>
    /// A run can be a different SIZE from the prose around it — inline code at 13.5 inside a 16
    /// paragraph, which is what every documentation handoff asks for and what a run could not say.
    /// </summary>
    [Fact]
    public void ARunCanBeSmallerThanTheProseAroundIt()
    {
        var html = Render(new Text("", TypeRole.BodyM)
        {
            Spans =
            [
                new TextRun("Call "),
                new TextRun("Build()", Mono: true) { StyleOverride = TypeStyle.OfSize(13.5f, FontWeight.Regular) },
                new TextRun(" first."),
            ],
        });

        html.Should().Contain("font-size: 13.5px");
    }

    /// <summary>It keeps the paragraph's LINE BOX — a run that set its own line-height would open a
    /// gap above and below itself, which is a line, not a run.</summary>
    [Fact]
    public void ASizedRun_DoesNotSetItsOwnLineHeight()
    {
        var html = Render(new Text("", TypeRole.BodyM)
        {
            Spans = [new TextRun("x") { StyleOverride = TypeStyle.OfSize(13.5f, FontWeight.Regular) }],
        });

        // The run's own style is inline (that is where font-size landed); the paragraph's
        // line-height rides an atomic class, so its absence HERE is the run staying silent about it.
        html.Should().Contain("font-size: 13.5px").And.NotContain("line-height");
    }
}
