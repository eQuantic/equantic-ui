using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// A compile-time constant written as the JavaScript value of its C# type. One writer for every
/// place a constant's VALUE reaches JavaScript rather than the syntax that wrote it: an inlined const
/// field, a parameter's default filled in for a skipped argument, the decimal a pattern or a case label
/// compares with, and a decimal or string literal.
/// <list type="bullet">
/// <item>A <c>decimal</c> is the runtime's Decimal, built from its exact invariant text, scale kept:
/// <c>$eq.num.dec("1.50")</c>. Nothing else holds <c>decimal.MaxValue</c>'s 29 digits.</item>
/// <item>A <c>long</c> or a <c>ulong</c> is a BigInt, whatever its size: a small one written as a
/// plain number met the first long it was mixed with as a TypeError (<c>t / TimeSpan.TicksPerSecond</c>).</item>
/// <item>A <c>float</c> is the double it is ("R" of the widened value), since its own shortest text
/// names the single to .NET and a different number to JavaScript.</item>
/// <item>An ENUM's constant is the enum's own representation (EnumStrategy): a <c>[Flags]</c> enum's
/// number, any other enum's camelCase member name. The value arrives as the underlying integer, and
/// written as a number it read a name table at <c>[1]</c>, or reached a flags parameter as a name.</item>
/// <item>A <c>char</c> and a <c>string</c> are quoted text, escaped (<see cref="Quote"/>).</item>
/// </list>
/// </summary>
internal static class ConstantLiteral
{
    /// <summary>The literal, registering the runtime import a decimal needs: for the paths that emit it.</summary>
    public static string? Write(object? value, ITypeSymbol? type, ConversionContext context)
    {
        if (value is decimal) context.UsedHelpers.Add(Eq.Import);
        return Write(value, type);
    }

    /// <summary>The literal for <paramref name="value"/>, a constant of <paramref name="type"/>, or
    /// null when it has no JavaScript spelling here. Pure, for a question asked before any emission.</summary>
    public static string? Write(object? value, ITypeSymbol? type)
    {
        if (value is null) return "null";
        // A native-sized integer is a plain number here, whichever box its constant arrived in.
        if (type?.SpecialType is SpecialType.System_IntPtr or SpecialType.System_UIntPtr)
            return System.Convert.ToString(value, CultureInfo.InvariantCulture);
        if (type.UnwrapNullable() is { TypeKind: TypeKind.Enum } enumType)
            return EnumValue(value, enumType);

        switch (value)
        {
            case string text:
                return Quote(text);
            case char character:
                return Quote(character.ToString());
            case bool flag:
                return flag ? "true" : "false";
            case decimal number:
                return $"{Eq.Dec}(\"{number.ToString(CultureInfo.InvariantCulture)}\")";
            case long or ulong:
                return $"{System.Convert.ToString(value, CultureInfo.InvariantCulture)}n";
            case int or uint or short or ushort or byte or sbyte:
                return System.Convert.ToString(value, CultureInfo.InvariantCulture);
            case float single:
                return ((double)single).ToString("R", CultureInfo.InvariantCulture);
            case double number:
                return number.ToString("R", CultureInfo.InvariantCulture);
            default:
                return null;
        }
    }

    /// <summary>
    /// <paramref name="text"/> as a single-quoted JavaScript string. What cannot stand for itself in the
    /// module is escaped: the quote and the backslash, and every character with no glyph of its own (a
    /// control, the tab and the line terminators included, a format character, a mark, a separator
    /// other than the space, a code point unassigned or for private use) as <c>\uXXXX</c>, in the
    /// upper case C# source spells them in. So is a LONE surrogate: UTF-8 has no bytes for one, so a
    /// module holding it could not be written, a strict encoder throwing and a lenient one writing
    /// U+FFFD, another character. A surrogate PAIR is one character, which UTF-8 writes as it is.
    /// </summary>
    public static string Quote(string text)
    {
        var quoted = new StringBuilder(text.Length + 2).Append('\'');
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                quoted.Append(c).Append(text[++i]);
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

    /// <summary>An enum's constant in the enum's representation: the number for <c>[Flags]</c>, whose
    /// members exist to be OR-combined, otherwise the camelCase name of the member holding the value.
    /// A value no member names has no name to be, and stays its number.</summary>
    private static string EnumValue(object value, ITypeSymbol enumType)
    {
        if (!enumType.IsFlagsEnum()
            && enumType.GetMembers().OfType<IFieldSymbol>()
                .FirstOrDefault(member => member.HasConstantValue && Equals(member.ConstantValue, value)) is { } named)
        {
            return $"'{named.Name.ToCamelCase()}'";
        }
        return System.Convert.ToString(value, CultureInfo.InvariantCulture)!;
    }
}
