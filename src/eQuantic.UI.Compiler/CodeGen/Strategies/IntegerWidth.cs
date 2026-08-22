using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// What a fixed-width integer result becomes in JavaScript, decided by the RESULT TYPE of the
/// operation — the one thing the syntax does not say and the bound tree does. A C# `byte` past
/// 255 wraps; a JavaScript number keeps counting. Sub-int widths and <c>uint</c> always wrap
/// (packed values and hashes rely on it, and the cost is one mask); <c>int</c> and <c>long</c>
/// wrap where the author wrote <c>unchecked</c>, because every plain `i + 1` in a UI would
/// otherwise carry a `| 0` for an overflow that is a bug anywhere else; and a <c>checked</c>
/// context throws, at run time, exactly where C# throws.
/// </summary>
public static class IntegerWidth
{
    /// <summary>The bit width and signedness of a fixed-width integer type, or null.</summary>
    public static (int Bits, bool Unsigned)? Of(ITypeSymbol? type) => type?.SpecialType switch
    {
        SpecialType.System_SByte => (8, false),
        SpecialType.System_Byte => (8, true),
        SpecialType.System_Int16 => (16, false),
        SpecialType.System_UInt16 => (16, true),
        SpecialType.System_Int32 => (32, false),
        SpecialType.System_UInt32 => (32, true),
        SpecialType.System_Int64 => (64, false),
        SpecialType.System_UInt64 => (64, true),
        _ => null,
    };

    /// <summary>Whether an unchecked result of this type wraps in the emitted code: always for
    /// widths below 32 and for uint; only under an explicit <c>unchecked</c> for int and long.</summary>
    public static bool WrapsByDefault((int Bits, bool Unsigned) width) =>
        width.Bits < 32 || (width.Bits == 32 && width.Unsigned);

    /// <summary>The wrapped result: the JavaScript that brings a number back into the width.</summary>
    public static JsExpr Wrap(JsExpr value, (int Bits, bool Unsigned) width)
    {
        var text = JsExprWriter.WriteIn(value, JsPrecedence.Shift);
        return width switch
        {
            (8, false) => JsExpr.Callish($"(({text} << 24) >> 24)"),
            (8, true) => JsExpr.Callish($"({text} & 0xFF)"),
            (16, false) => JsExpr.Callish($"(({text} << 16) >> 16)"),
            (16, true) => JsExpr.Callish($"({text} & 0xFFFF)"),
            (32, false) => JsExpr.Callish($"({text} | 0)"),
            (32, true) => JsExpr.Callish($"({text} >>> 0)"),
            (64, false) => JsExpr.Callish($"BigInt.asIntN(64, {JsExprWriter.Write(value)})"),
            _ => JsExpr.Callish($"BigInt.asUintN(64, {JsExprWriter.Write(value)})"),
        };
    }

    /// <summary>The checked result: the value, or the overflow C# throws.</summary>
    public static JsExpr Checked(JsExpr value, (int Bits, bool Unsigned) width, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        var unsigned = width.Unsigned ? ", true" : "";
        return JsExpr.Callish($"{Eq.Checked}({JsExprWriter.Write(value)}, {width.Bits}{unsigned})");
    }

    /// <summary>Brings an arithmetic result of a fixed-width type into C#'s semantics for the
    /// context it sits in: checked → throws past the edge; unchecked → wraps where the width
    /// wraps by default or the author asked for it; otherwise the value as computed.</summary>
    public static JsExpr Settle(JsExpr value, ITypeSymbol? resultType, bool isChecked, bool explicitUnchecked,
        ConversionContext context)
    {
        if (Of(resultType) is not { } width) return value;
        if (isChecked) return Checked(value, width, context);
        return WrapsByDefault(width) || explicitUnchecked ? Wrap(value, width) : value;
    }
}
