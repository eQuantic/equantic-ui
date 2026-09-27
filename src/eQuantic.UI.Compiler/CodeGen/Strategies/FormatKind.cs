using eQuantic.UI.Compiler.CodeGen.Extensions;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// Which C# number a JavaScript number stands for, as the runtime's formatter reads it
/// (<c>NumberKind</c>): the name of its .NET type. A number cannot say it is a float or an int, and
/// each formats its own way: a float writes a single's own digits (#378), an integer rounds a
/// formatted half away from zero where a double rounds it to even (#393), so
/// <c>125.ToString("E1")</c> is <c>1.3E+002</c> and <c>125.0.ToString("E1")</c> is <c>1.2E+002</c>,
/// and <c>X</c> writes a negative integer as its two's complement at the type's width, so
/// <c>((short)-1).ToString("X")</c> is <c>FFFF</c> where an int's is <c>FFFFFFFF</c> (#445). A long
/// and a decimal say what they are on their own, as a BigInt and a Decimal. The one place the static
/// type is turned into the kind.
/// </summary>
internal static class FormatKind
{
    /// <summary>The kind for <paramref name="type"/>, or null for a double, which is the formatter's
    /// default, and for anything that is not a number of these widths.</summary>
    public static string? Of(ITypeSymbol? type) => type.UnwrapNullable()?.SpecialType switch
    {
        SpecialType.System_Single => "single",
        SpecialType.System_SByte => "sbyte",
        SpecialType.System_Byte => "byte",
        SpecialType.System_Int16 => "int16",
        SpecialType.System_UInt16 => "uint16",
        SpecialType.System_Int32 => "int32",
        SpecialType.System_UInt32 => "uint32",
        _ => null,
    };

    /// <summary>Whether <paramref name="kind"/> names an integer: its text with no specifier is its
    /// digits whatever it is, so it says what it is only where a specifier is written.</summary>
    public static bool IsInteger(string? kind) => kind is not null and not "single";
}
