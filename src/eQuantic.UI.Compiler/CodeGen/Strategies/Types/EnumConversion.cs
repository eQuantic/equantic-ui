using System.Globalization;
using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// C#'s enumeration conversions (§10.3.3), between an enum and a number or between two enums, each
/// read as the numeric conversion between the underlying types it is: the value the enum holds,
/// converted as its underlying type converts, and a value brought into an enum's underlying type and
/// held as the enum. A 64-bit enum's value is a BigInt (#551), so <c>(int)aWideFlag</c> takes its low
/// 32 bits and <c>(Wide)anInt</c> makes a BigInt of it, where the cast handed the number through, and
/// a non-flags enum's key reads its value before it converts, where <c>(double)status</c> was its key.
/// </summary>
internal static class EnumConversion
{
    /// <summary>
    /// The conversion from <paramref name="from"/> to <paramref name="to"/>, a nullable of either
    /// included, or null where neither is an enum. A <paramref name="constant"/>, the value the bound tree
    /// already converted, is written as the target holds it; a checked context throws where .NET throws.
    /// </summary>
    internal static JsExpr? Convert(ITypeSymbol? from, ITypeSymbol? to, object? constant, JsExpr translated,
        bool isChecked, ConversionContext context)
    {
        var source = from.UnwrapNullable();
        var target = to.UnwrapNullable();
        var sourceEnum = EnumOperators.EnumOf(source);
        var targetEnum = EnumOperators.EnumOf(target);
        if (sourceEnum is null && targetEnum is null) return null;
        if (targetEnum is not null && constant is not null && Integral(constant) is { } held)
            return JsExpr.Literal(EnumShape.HeldLiteral(targetEnum, held));
        if (sourceEnum is not null && targetEnum is not null && SymbolEqualityComparer.Default.Equals(sourceEnum, targetEnum))
            return translated;
        JsExpr Converted(JsExpr operand)
        {
            var value = sourceEnum is null ? operand : EnumShape.ValueOf(sourceEnum, operand, context);
            var underlying = sourceEnum?.EnumUnderlyingType ?? source;
            return targetEnum is null
                ? ValueFlow.Numeric(underlying, target, value, null, isChecked, context)
                : EnumShape.Held(targetEnum,
                    ValueFlow.Numeric(underlying, targetEnum.EnumUnderlyingType, value, null, isChecked, context), context);
        }
        // A nullable converted to a nullable lets null pass, and converts only a value.
        return from.IsNullableValue() && to.IsNullableValue() ? NullableLift.Unary(translated, Converted, context) : Converted(translated);
    }

    /// <summary>A constant's integral value, an enum's included, or null for one that is no integer.</summary>
    private static decimal? Integral(object constant) => constant switch
    {
        System.Enum or sbyte or byte or short or ushort or int or uint or long or ulong or char =>
            System.Convert.ToDecimal(constant, CultureInfo.InvariantCulture),
        _ => null,
    };
}
