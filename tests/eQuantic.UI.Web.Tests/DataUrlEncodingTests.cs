using System.Runtime.CompilerServices;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The one table a tile's data URL is written with (#1937), and the CSS string a raster's source is
/// written as. Pinned here, and written to a fixture the runtime's twin replays through its own table
/// (<c>data-url.spec.ts</c>), so the two producers write the same bytes and hash a tile's layer to
/// the same atomic class. Regenerate the fixture with <c>EQ_UPDATE_DATA_URL_FIXTURE=1</c>.
/// </summary>
public class DataUrlEncodingTests
{
    private const string Escaped = "%#\"<>";

    [Fact]
    public void EveryAsciiCodePoint_IsEscapedByTheTableAndNothingElse()
    {
        for (var c = (char)0; c < 128; c++)
        {
            var expected = c < 0x20 || c == 0x7F || Escaped.Contains(c) ? $"%{(int)c:X2}" : c.ToString();
            DataUrl.Encode(c.ToString()).Should().Be(expected, $"code point {(int)c}");
        }
    }

    [Theory]
    [InlineData("é", "%C3%A9")]
    [InlineData("€", "%E2%82%AC")]
    [InlineData("😀", "%F0%9F%98%80")]
    [InlineData("#", "%23")]
    [InlineData(" '()*!~", " '()*!~")]
    [InlineData("<g opacity='0.5'>", "%3Cg opacity='0.5'%3E")]
    public void TheTable_WritesUtf8ForEverythingPastAscii_AndLeavesTheRest(string text, string expected) =>
        DataUrl.Encode(text).Should().Be(expected);

    [Fact]
    public void ASource_IsACssString_WithItsQuoteBackslashAndLineBreaksEscaped() =>
        DataUrl.Css("img/a\"b\\c\nd\re\ff.png").Should().Be("url(\"img/a\\\"b\\\\c\\A d\\D e\\C f.png\")");

    [Fact]
    public void APlainSource_IsWrittenAsItIs() =>
        DataUrl.Css("img/linen.png").Should().Be("url(\"img/linen.png\")");

    /// <summary>
    /// Every control character in a source is its code point in hexadecimal and a space, not only the
    /// line breaks that end a CSS string: the server's sheet reads a control character as the marker
    /// of a rule's state, so a raw one inside a URL split the rule (security review W1, #1937).
    /// </summary>
    [Fact]
    public void EveryControlCharacter_InASource_IsAHexEscape()
    {
        foreach (var c in Enumerable.Range(0, 0x20).Append(0x7F).Select(code => (char)code))
            DataUrl.Css($"a{c}b").Should().Be($"url(\"a\\{(int)c:X} b\")", $"code point {(int)c}");
    }

    /// <summary>
    /// A brace in a source is a hex escape: the runtime adopts the server's sheet by finding each
    /// <c>.class{</c> in its text, so a raw brace inside a URL could mark a class as already styled
    /// (security review W2, #1937).
    /// </summary>
    [Fact]
    public void ABrace_InASource_IsAHexEscape() =>
        DataUrl.Css("a{b}c").Should().Be("url(\"a\\7B b\\7D c\")");

    /// <summary>Every other ASCII character is written as it is, the two a backslash escapes aside.</summary>
    [Fact]
    public void EveryOtherAsciiCharacter_InASource_IsWrittenAsItIs()
    {
        for (var c = (char)0x20; c < 0x7F; c++)
        {
            if (c is '\\' or '"' or '{' or '}') continue;
            DataUrl.Css($"a{c}b").Should().Be($"url(\"a{c}b\")", $"code point {(int)c}");
        }
    }

    /// <summary>The fixture the twin replays: every case above, as the C# table writes it.</summary>
    [Fact]
    public void TheFixture_IsWhatTheTableWrites()
    {
        var encode = Enumerable.Range(0, 128).Select(c => ((char)c).ToString())
            .Concat(["é", "€", "😀", "<svg xmlns='http://www.w3.org/2000/svg' fill='#C8796B'>100%</svg>"])
            .Select(text => new[] { text, DataUrl.Encode(text) });
        var css = new[] { "img/linen.png", "img/a\"b\\c\nd\re\ff.png", "data:image/svg+xml,%3Csvg/%3E", "a{b}c", "é" }
            .Concat(Enumerable.Range(0, 128).Select(c => $"a{(char)c}b"))
            .Select(text => new[] { text, DataUrl.Css(text) });
        var text = FixtureJson.Write(new { encode, css }, FixtureJson.Readable);

        var path = FixturePath();
        if (Environment.GetEnvironmentVariable("EQ_UPDATE_DATA_URL_FIXTURE") == "1")
        {
            File.WriteAllText(path, text);
            return;
        }
        File.Exists(path).Should().BeTrue("the fixture is written with EQ_UPDATE_DATA_URL_FIXTURE=1 and committed");
        File.ReadAllText(path).Replace("\r\n", "\n").Should().Be(text,
            "the twin replays this fixture: regenerate it with EQ_UPDATE_DATA_URL_FIXTURE=1 when the table changes");
    }

    private static string FixturePath([CallerFilePath] string sourcePath = "") =>
        Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", "..", "src", "eQuantic.UI.Runtime", "src", "shared",
            "data-url.fixture.json");
}
