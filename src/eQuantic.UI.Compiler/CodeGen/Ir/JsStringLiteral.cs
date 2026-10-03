using System.Globalization;
using System.Text;

namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>
/// THE SPELLING OF A STRING in the emitted JavaScript: a single-quoted literal, or the text between a
/// template literal's backticks. Every string or char VALUE the transpiler writes into a module is
/// spelled here, a char's default included; what is quoted elsewhere is a name (an enum member's, a
/// type's), which an identifier's characters keep from needing an escape, or a tag of the SDK's own.
/// <para>
/// The text was quoted by hand in nine places, and five of them, a record's declared defaults among
/// them, escaped the backslash and the quote only, so `string Joined = "a" + "\n"` was written with a
/// line break inside its quotes. #520 found five more that wrote a value in a spelling that lost it:
/// an interpolation's format (`$"{date:dd 'de' MMMM}"` closed its own quotes, and Bun refused the
/// module), the default a named argument skips (`M.f('it's', 1)`, and a char with no quotes at all),
/// the resource lookup, and the char literal and `nameof`, which wrote the C# SPELLING where the
/// value belongs: `'\a'` is `'a'` to JavaScript, `'\x1'` a syntax error, and `nameof(@class)` was
/// `'@class'`.
/// </para>
/// <para>
/// A code unit is written as itself when it SHOWS as itself, and as a `\uXXXX` escape when it does not:
/// <list type="bullet">
/// <item>A surrogate that is not half of a pair has no UTF-8 encoding: `"x\uD83D"` stopped the whole
/// build with EQ0001 when its module was written, naming no file, and a writer with a replacing
/// encoder turns it into U+FFFD without a word.</item>
/// <item>A control and a format character are invisible, and a raw line feed or carriage return
/// ends the literal; the line feed, the carriage return and the tab keep their short escapes, and
/// no other one is used (`\0` before a digit is an octal escape, which a module refuses).</item>
/// <item>U+2028 and U+2029 are legal inside a literal since ES2019, and Bun reads them as line breaks
/// when it maps positions, so a raw one moved every later line of the map (#491).</item>
/// <item>A private-use character has no glyph outside the font that defines it, and a space other
/// than U+0020 reads as one.</item>
/// <item>A combining mark is drawn on the character before it, so one with nothing written before it
/// to sit on, the first of a literal or one after an escape, would sit on the quote.</item>
/// </list>
/// Everything else, a well-formed pair included, is written raw: the module is UTF-8, and the
/// bundler chooses the final spelling of what it ships.
/// </para>
/// </summary>
internal static class JsStringLiteral
{
    /// <summary>The value as a single-quoted string literal.</summary>
    public static string Quote(string value)
    {
        var text = new StringBuilder(value.Length + 2).Append('\'');
        Write(value, text, template: false);
        return text.Append('\'').ToString();
    }

    /// <summary>The value as the text of a template literal, between its backticks or its holes. A
    /// backtick and a <c>${</c> are escaped, so the text neither ends the template nor opens a hole.</summary>
    public static string TemplateText(string value)
    {
        var text = new StringBuilder(value.Length);
        Write(value, text, template: true);
        return text.ToString();
    }

    private static void Write(string value, StringBuilder text, bool template)
    {
        var afterRaw = false;
        for (var i = 0; i < value.Length; i++)
        {
            var unit = value[i];
            var escape = unit switch
            {
                '\\' => @"\\",
                '\'' when !template => @"\'",
                '`' when template => @"\`",
                '$' when template && i + 1 < value.Length && value[i + 1] == '{' => @"\$",
                '\n' => @"\n",
                '\r' => @"\r",
                '\t' => @"\t",
                _ => null,
            };
            if (escape is not null)
            {
                text.Append(escape);
                afterRaw = false;
                continue;
            }

            // A pair is one code point and is judged as one; a half of a pair alone is a Surrogate.
            var length = char.IsHighSurrogate(unit) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]) ? 2 : 1;
            var codePoint = length == 2 ? char.ConvertToUtf32(unit, value[i + 1]) : unit;
            var raw = ShowsAsItself(codePoint, afterRaw);
            for (var k = i; k < i + length; k++)
            {
                if (raw) text.Append(value[k]);
                else text.Append(@"\u").Append(((int)value[k]).ToString("X4", CultureInfo.InvariantCulture));
            }
            afterRaw = raw;
            i += length - 1;
        }
    }

    private static bool ShowsAsItself(int codePoint, bool afterRaw) =>
        CharUnicodeInfo.GetUnicodeCategory(codePoint) switch
        {
            UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate
                or UnicodeCategory.PrivateUse or UnicodeCategory.LineSeparator
                or UnicodeCategory.ParagraphSeparator => false,
            UnicodeCategory.SpaceSeparator => codePoint == ' ',
            UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark => afterRaw,
            _ => true,
        };
}
