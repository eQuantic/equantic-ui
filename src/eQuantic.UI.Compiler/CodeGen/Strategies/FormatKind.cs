using eQuantic.UI.Compiler.CodeGen.Extensions;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// Which C# number a JavaScript number stands for, as the runtime's formatter reads it
/// (<c>NumberKind</c>): the name of its .NET type. A number cannot say it is a double, a float or an
/// int, and each formats its own way: a float writes a single's own digits (#378), an integer rounds
/// a formatted half away from zero where a double rounds it to even (#393), so
/// <c>125.ToString("E1")</c> is <c>1.3E+002</c> and <c>125.0.ToString("E1")</c> is <c>1.2E+002</c>,
/// <c>X</c> writes a negative integer as its two's complement at the type's width, so
/// <c>((short)-1).ToString("X")</c> is <c>FFFF</c> where an int's is <c>FFFFFFFF</c> (#445) and a
/// <c>nint</c>'s sixteen F's, and only an integer takes <c>D</c>, <c>X</c> and <c>B</c>, so
/// <c>(2.0).ToString("D")</c> throws as .NET throws (#455). A value whose type is none of these (an
/// <c>object</c>, a generic) reaches the formatter with no kind, as <c>unknown</c>, and is read by
/// what it holds. A long and a decimal say what they are on their own, as a BigInt and a Decimal.
/// The one place the static type is turned into the kind.
/// </summary>
internal static class FormatKind
{
    /// <summary>The kind for <paramref name="type"/>, or null for anything that is not a number of
    /// these types.</summary>
    public static string? Of(ITypeSymbol? type) => type.UnwrapNullable()?.SpecialType switch
    {
        SpecialType.System_Double => "double",
        SpecialType.System_Single => "single",
        SpecialType.System_SByte => "sbyte",
        SpecialType.System_Byte => "byte",
        SpecialType.System_Int16 => "int16",
        SpecialType.System_UInt16 => "uint16",
        SpecialType.System_Int32 => "int32",
        SpecialType.System_UInt32 => "uint32",
        SpecialType.System_IntPtr => "nint",
        SpecialType.System_UIntPtr => "nuint",
        _ => null,
    };

    /// <summary>The kind a value's text with NO specifier needs: every number's but a double's, which
    /// is how the formatter reads a number it is told nothing about. A float writes its own digits,
    /// not the double's, and an integer writes no sign on a zero: <c>-1 / 2</c> truncates to
    /// <c>-0</c> in JavaScript, which is a double's text, and to <c>0</c> in C#.</summary>
    public static string? OfText(ITypeSymbol? type) => Of(type) is "double" ? null : Of(type);
}
