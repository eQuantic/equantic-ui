using System.Text;

namespace eQuantic.UI.Web;

/// <summary>
/// How a tile's image is written into a <c>background-image</c> layer (#1937), by ONE explicit table
/// that the runtime's twin transcribes character for character (<c>dataUrlText</c> and
/// <c>cssUrl</c> in lowering.ts), so the server and the browser hash the same declaration to the same
/// atomic class.
/// <para>
/// Neither language's own escaping can be the shared rule: .NET's <c>Uri.EscapeDataString</c> escapes
/// <c>'()*!</c>, which JavaScript's <c>encodeURIComponent</c> leaves alone, and both escape far more
/// than a data URL inside a CSS string needs. A fixture of every ASCII code point pins the two tables
/// against each other.
/// </para>
/// </summary>
internal static class DataUrl
{
    /// <summary>A standalone SVG document as a data URL: <c>data:image/svg+xml,</c> and the markup
    /// through <see cref="Encode"/>.</summary>
    public static string Svg(string markup) => "data:image/svg+xml," + Encode(markup);

    /// <summary>
    /// The text of a data URL's body: <c>%</c>, <c>#</c>, <c>"</c>, <c>&lt;</c>, <c>&gt;</c>, every
    /// control character (0 to 31, and 127) and every character past ASCII, as its UTF-8 bytes, become
    /// <c>%XX</c> with uppercase digits, and everything else stays as it is.
    /// </summary>
    public static string Encode(string text)
    {
        var builder = new StringBuilder(text.Length);
        Span<byte> bytes = stackalloc byte[4];
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c < 0x80)
            {
                if (c < 0x20 || c == 0x7F || c is '%' or '#' or '"' or '<' or '>') Escape(builder, (byte)c);
                else builder.Append(c);
                continue;
            }
            // A code point, so a pair of surrogates is one character of four UTF-8 bytes.
            var length = char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])
                ? Encoding.UTF8.GetBytes(text.AsSpan(i++, 2), bytes)
                : Encoding.UTF8.GetBytes(text.AsSpan(i, 1), bytes);
            foreach (var b in bytes[..length]) Escape(builder, b);
        }
        return builder.ToString();
    }

    /// <summary>
    /// A URL as a CSS <c>url("…")</c>: written as a CSS string, where <c>\</c> and <c>"</c> are escaped
    /// with a backslash, and every control character (0 to 31, and 127), <c>{</c> and <c>}</c> as its
    /// code point in hexadecimal and a space (<c>\A </c>, <c>\1 </c>, <c>\7B </c>), so a source an
    /// <see cref="Primitives.Image"/> would take works unchanged.
    /// <para>
    /// A line break ends a CSS string, so it must be escaped to stay inside one. The other controls
    /// and the braces are escaped because a stylesheet's text is read again after it is written: the
    /// server's sheet marks a rule's state with a control character, and the runtime adopts that
    /// sheet by finding each <c>.class{</c> in its text. A raw one in a source was read as part of
    /// that structure, not of the URL. CSS reads every escape back as the character it stands for.
    /// </para>
    /// </summary>
    public static string Css(string url)
    {
        var builder = new StringBuilder(url.Length + 7).Append("url(\"");
        foreach (var c in url)
        {
            if (c is '\\' or '"') builder.Append('\\').Append(c);
            else if (c < 0x20 || c == 0x7F || c is '{' or '}')
                builder.Append('\\').Append(((int)c).ToString("X", System.Globalization.CultureInfo.InvariantCulture)).Append(' ');
            else builder.Append(c);
        }
        return builder.Append("\")").ToString();
    }

    private static void Escape(StringBuilder builder, byte value) =>
        builder.Append('%').Append(value.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
}
