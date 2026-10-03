using System.Globalization;
using System.Text;

namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>
/// THE SPELLING OF A STRING in the emitted JavaScript: single-quoted, with the backslash, the quote
/// and the two line breaks escaped. A raw line feed or carriage return inside a string literal is a
/// syntax error that takes the whole module with it, and the text was quoted by hand in nine places:
/// five of them, a record's declared defaults among them, escaped the backslash and the quote only,
/// so `string Joined = "a" + "\n"` was written with a line break inside its quotes.
/// <para>
/// Every character with no glyph of its own is escaped too, as <c>\uXXXX</c> in the upper case C#
/// source spells it in: a control (the tab as <c>\t</c>), a format character, a mark, a separator
/// other than the space (U+2028 and U+2029, legal inside a literal since ES2019, a line break to every
/// tool older than that), a code point unassigned or for private use. Raw, each is text no reader of
/// the module can see. So is a LONE surrogate, which UTF-8 has no bytes for: the module holding one
/// could not be written, a strict encoder throwing and a lenient one writing U+FFFD, another
/// character (#450). A surrogate PAIR is one character, which UTF-8 writes as it is.
/// </para>
/// </summary>
internal static class JsStringLiteral
{
    public static string Quote(string value)
    {
        var quoted = new StringBuilder(value.Length + 2).Append('\'');
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                quoted.Append(c).Append(value[++i]);
                continue;
            }
            switch (c)
            {
                case '\\': quoted.Append("\\\\"); break;
                case '\'': quoted.Append("\\'"); break;
                case '\n': quoted.Append("\\n"); break;
                case '\r': quoted.Append("\\r"); break;
                case '\t': quoted.Append("\\t"); break;
                default:
                    if (HasNoGlyph(c))
                        quoted.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    else
                        quoted.Append(c);
                    break;
            }
        }
        return quoted.Append('\'').ToString();
    }

    /// <summary>Whether a character shows nothing of its own where it stands: raw in the module, it is
    /// text no reader can see, and a lone surrogate is text UTF-8 cannot hold.</summary>
    private static bool HasNoGlyph(char c) => CharUnicodeInfo.GetUnicodeCategory(c) switch
    {
        UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.NonSpacingMark
            or UnicodeCategory.EnclosingMark or UnicodeCategory.LineSeparator
            or UnicodeCategory.ParagraphSeparator or UnicodeCategory.PrivateUse
            or UnicodeCategory.OtherNotAssigned or UnicodeCategory.Surrogate => true,
        UnicodeCategory.SpaceSeparator => c != ' ',
        _ => false,
    };
}
