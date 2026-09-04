using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A Text built from RUNS carries an empty <c>Content</c> — the paragraph lives in
/// <c>Spans</c>, and <c>PlainContent</c> is what the node actually says. Anything that reads the
/// FIELD instead decides as if the paragraph were empty, silently and only for styled text.
/// <para>
/// Found by the sibling session in the semantic walk, where a <c>Content.Length > 0</c> guard
/// dropped every styled paragraph and screen readers heard nothing. It was not one site: the same
/// read was in the web realizer twice, and the parity fixture could never catch either, because
/// the TypeScript twin read the same wrong field and the two agreed.
/// </para>
/// </summary>
public class RichTextPlainContentTests
{
    private static Text Styled(string content) =>
        new("", TypeRole.Heading, null) { Spans = new[] { new TextRun(content) } };

    /// <summary>An authored \n is a hard break. In a run it was dropped, so the designed
    /// headline ran on in one line with nothing to say why.</summary>
    [Fact]
    public void AHardBreakInsideARun_StillTurnsTheLine()
    {
        var css = ((HtmlElement)WebRealizer.Lower(Styled("first\nsecond"), PhotonTheme.Instance))
            .Style!.ToCssString();

        css.Should().Contain("pre-line");
    }

    /// <summary>And a run WITHOUT one still must not ask for it — the fix must not turn the
    /// property on for every styled paragraph there is.</summary>
    [Fact]
    public void ARunWithoutABreak_AsksForNoWhiteSpaceRule()
    {
        var css = ((HtmlElement)WebRealizer.Lower(Styled("one line"), PhotonTheme.Instance))
            .Style!.ToCssString();

        css.Should().NotContain("pre-line").And.NotContain("pre-wrap");
    }

    /// <summary>
    /// The tooltip id hashes the panel's visible TEXT, which is what makes it deterministic across
    /// SSR and hydration. Read from the field, every styled panel hashed the same empty string —
    /// so two tooltips shared one id, and the aria-describedby pointing at one named the other.
    /// </summary>
    [Fact]
    public void TwoTooltipsWithDifferentStyledText_DoNotShareOneId()
    {
        static string TooltipId(string hint)
        {
            var tooltip = new Anchored(
                new Pressable(new Text("Save", TypeRole.Label), () => { }) { Label = "Save" },
                Styled(hint))
            {
                OpenOnHover = true,
                DescribesAnchor = true,
            };
            var element = WebRealizer.Lower(tooltip, PhotonTheme.Instance);
            return ((HtmlElement)element.Children[^1]).Id!;
        }

        TooltipId("saves the draft").Should().NotBe(TooltipId("discards the draft"),
            "the id hashes the panel's visible text, and a styled paragraph keeps all of it in runs");
    }
}
