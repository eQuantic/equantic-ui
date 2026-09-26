using eQuantic.UI.Compiler.CodeGen.Extensions;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// Which C# number a JavaScript number stands for, as the runtime's formatter reads it
/// (<c>NumberKind</c>). A number cannot say it is a float or an int, and each formats its own way: a
/// float writes a single's own digits (#378), and an integer rounds a formatted half away from zero
/// where a double rounds it to even (#393), so <c>125.ToString("E1")</c> is <c>1.3E+002</c> and
/// <c>125.0.ToString("E1")</c> is <c>1.2E+002</c>. A long and a decimal say what they are on their
/// own, as a BigInt and a Decimal. The one place the static type is turned into the kind.
/// </summary>
internal static class FormatKind
{
    /// <summary>The kind for <paramref name="type"/>, or null for a double, which is the formatter's
    /// default, and for anything that is not a number of these widths.</summary>
    public static string? Of(ITypeSymbol? type) => type.UnwrapNullable()?.SpecialType switch
    {
        SpecialType.System_Single => "single",
        SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_Int16
            or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 => "integer",
        _ => null,
    };
}
