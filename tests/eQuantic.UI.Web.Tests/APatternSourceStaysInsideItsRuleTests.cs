using System.Text.RegularExpressions;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The text a tile carries (an image's source, a drawing's path) reaches the server's atomic sheet,
/// and that sheet is read again after it is written: by the sink itself, which marks a rule's state
/// with a control character, and by the runtime, which adopts the sheet by finding each
/// <c>.class{</c> in its text. So a control character or a brace from the tile must never reach the
/// sheet raw, or it is read as the sheet's structure rather than as the URL (security review W1 and
/// W2, #1937). The escape is <see cref="DataUrl.Css"/>'s, pinned against its twin by
/// <see cref="DataUrlEncodingTests"/>.
/// </summary>
public class APatternSourceStaysInsideItsRuleTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    /// <summary>The runtime's adoption of the server's sheet, transcribed: <c>registry()</c> in style-atomizer.ts.</summary>
    private static readonly Regex Adopted = new(@"\.(eq-[0-9a-z]+)\{");

    private static string Sheet(Pattern pattern)
    {
        var sink = new StyleSink();
        WebRealizer.Lower(new Box(new BoxStyle { Patterns = [pattern] }), Theme, 1f, sink);
        return sink.Css;
    }

    [Fact]
    public void AControlCharacterInASource_IsEscapedInsideItsOwnRule()
    {
        var css = Sheet(new ImagePattern("/a.png\u0001x", 120));

        css.Should().Contain("{background-image:url(\"/a.png\\1 x\")}",
            "the source is one declaration of its own rule, its control character escaped");
        css.ToCharArray().Should().NotContain(c => c < 0x20 || c == 0x7F, "no control character reaches the sheet");
    }

    [Fact]
    public void ABraceInASource_AdoptsNoClassButTheRulesOwn()
    {
        var css = Sheet(new ImagePattern("/a.png?.eq-1x2y3z{", 120));

        Adopted.Matches(css).Select(m => m.Groups[1].Value).Should().NotContain("eq-1x2y3z",
            "a class named inside a source is not a rule of the sheet");
        css.Should().Contain("url(\"/a.png?.eq-1x2y3z\\7B \")");
    }

    [Fact]
    public void ABraceInADrawingsPath_AdoptsNoClassButTheRulesOwn()
    {
        var drawing = new VectorDrawing(0, 0, 10, 10,
            [new VectorShape("M0 0H10V10H0Z.eq-1x2y3z{", VectorPaint.Solid(Color.FromRgb(0xC8, 0x79, 0x6B)))]);
        var css = Sheet(new DrawingPattern(drawing, 40));

        css.Should().Contain("M0 0H10V10H0Z.eq-1x2y3z\\7B ", "the path reached the sheet, its brace escaped");
        Adopted.Matches(css).Select(m => m.Groups[1].Value).Should().NotContain("eq-1x2y3z");
    }
}
