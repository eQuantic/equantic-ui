using System.Globalization;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// A compile-time constant written as the JavaScript value of its C# type. One writer for every
/// place a constant's VALUE reaches JavaScript rather than the syntax that wrote it: an inlined const
/// field, a parameter's default filled in for a skipped argument, and a decimal literal.
/// <list type="bullet">
/// <item>A <c>decimal</c> is the runtime's Decimal, built from its exact invariant text, scale kept:
/// <c>$eq.num.dec("1.50")</c>. Nothing else holds <c>decimal.MaxValue</c>'s 29 digits.</item>
/// <item>A <c>long</c> or a <c>ulong</c> is a BigInt, whatever its size: a small one written as a
/// plain number met the first long it was mixed with as a TypeError (<c>t / TimeSpan.TicksPerSecond</c>).</item>
/// <item>A <c>float</c> is the double it is ("R" of the widened value), since its own shortest text
/// names the single to .NET and a different number to JavaScript.</item>
/// <item>A <c>char</c> and a <c>string</c> are quoted text, escaped.</item>
/// </list>
/// </summary>
internal static class ConstantLiteral
{
    /// <summary>The literal, registering the runtime import a decimal needs: for the paths that emit it.</summary>
    public static string? Write(object? value, ITypeSymbol? type, ConversionContext context)
    {
        var literal = Write(value, type);
        if (literal is not null && NeedsRuntime(literal)) context.UsedHelpers.Add(Eq.Import);
        return literal;
    }

    /// <summary>Whether a literal this writer produced calls into the runtime.</summary>
    public static bool NeedsRuntime(string literal) => literal.StartsWith(Eq.Dec, System.StringComparison.Ordinal);

    /// <summary>The literal for <paramref name="value"/>, a constant of <paramref name="type"/>, or
    /// null when it has no JavaScript spelling here. Pure, for a question asked before any emission.</summary>
    public static string? Write(object? value, ITypeSymbol? type)
    {
        // A native-sized integer is a plain number here, whichever box its constant arrived in.
        if (type?.SpecialType is SpecialType.System_IntPtr or SpecialType.System_UIntPtr && value is not null)
            return System.Convert.ToString(value, CultureInfo.InvariantCulture);

        switch (value)
        {
            case null:
                return "null";
            case string text:
                return $"'{Escape(text)}'";
            case char character:
                return $"'{Escape(character.ToString())}'";
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

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "\\r");
}
