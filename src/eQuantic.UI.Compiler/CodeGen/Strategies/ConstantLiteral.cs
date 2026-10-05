using System.Globalization;
using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Ir;

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
/// <item>A <c>char</c> and a <c>string</c> are quoted text, escaped (<see cref="JsStringLiteral.Quote"/>).</item>
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
                return JsStringLiteral.Quote(text);
            case char character:
                return JsStringLiteral.Quote(character.ToString());
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
