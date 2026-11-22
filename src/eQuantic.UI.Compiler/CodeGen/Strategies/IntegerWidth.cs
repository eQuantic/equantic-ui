using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// What a fixed-width integer result becomes in JavaScript, decided by the RESULT TYPE of the
/// operation, the one thing the syntax does not say and the bound tree does. A C# `int` past
/// int.MaxValue wraps to int.MinValue; a JavaScript number keeps counting, and a BigInt never wraps
/// at all. So every width wraps where C# wraps, in its default context, `int` and `long` included
/// (#1807): a rule ported from the server that leans on the wrap (a hash, a packed value) answers in
/// the browser what it answers there. A <c>checked</c> context throws, at run time, exactly where C#
/// throws, and a value C#'s compiler folded and proved in range is left as written
/// (<see cref="ArithmeticContext.ProvedInRange"/>).
/// </summary>
public static class IntegerWidth
{
    /// <summary>The bit width and signedness of a fixed-width integer type, or null.</summary>
    public static (int Bits, bool Unsigned)? Of(ITypeSymbol? type) => Of(type?.SpecialType ?? SpecialType.None);

    /// <summary>The bit width and signedness of a fixed-width integer type, or null.</summary>
    public static (int Bits, bool Unsigned)? Of(SpecialType type) => type switch
    {
        SpecialType.System_SByte => (8, false),
        SpecialType.System_Byte => (8, true),
        SpecialType.System_Int16 => (16, false),
        SpecialType.System_UInt16 => (16, true),
        SpecialType.System_Int32 => (32, false),
        SpecialType.System_UInt32 => (32, true),
        // A native integer is 64 bits wide on every target this SDK ships to, the server included (#649).
        SpecialType.System_Int64 or SpecialType.System_IntPtr => (64, false),
        SpecialType.System_UInt64 or SpecialType.System_UIntPtr => (64, true),
        _ => null,
    };

    /// <summary>The wrapped result: the JavaScript that brings a number back into the width. The two
    /// 32-bit wraps are operators the writer places, so they carry parentheses only where their
    /// neighbours need them (<c>let half = value + 7 | 0;</c>); text of unknown shape is fenced first,
    /// since the writer cannot see inside it.</summary>
    public static JsExpr Wrap(JsExpr value, (int Bits, bool Unsigned) width)
    {
        if (width.Bits == 32)
        {
            var operand = value.Precedence == JsPrecedence.Opaque ? JsExpr.Group(value) : value;
            return JsExpr.Binary(operand, width.Unsigned ? ">>>" : "|", JsExpr.Literal("0"));
        }
        var text = JsExprWriter.WriteIn(value, JsPrecedence.Shift);
        return width switch
        {
            (8, false) => JsExpr.Callish($"(({text} << 24) >> 24)"),
            (8, true) => JsExpr.Callish($"({text} & 0xFF)"),
            (16, false) => JsExpr.Callish($"(({text} << 16) >> 16)"),
            (16, true) => JsExpr.Callish($"({text} & 0xFFFF)"),
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

    /// <summary>
    /// A bitwise operator's result in <paramref name="width"/>, or null where JavaScript's own
    /// operator already answers C#'s. JavaScript computes <c>&amp;</c>, <c>|</c>, <c>^</c> and <c>&gt;&gt;</c> in
    /// SIGNED 32 bits, so a uint with its top bit set came back negative (<c>uint.MaxValue &amp;
    /// uint.MaxValue</c> was -1) and <c>&gt;&gt;</c> shifted its sign in, where C# shifts an unsigned value
    /// logically: a uint's result goes back to 32 unsigned bits, and its right shift is <c>&gt;&gt;&gt;</c>.
    /// <c>&gt;&gt;&gt;</c> answers an unsigned 32-bit number, which a signed or narrower width brings back to
    /// its own. The other widths' <c>&amp;</c>, <c>|</c>, <c>^</c> and <c>&gt;&gt;</c> stay in range, and a long's
    /// are exact on its BigInt.
    /// </summary>
    public static JsExpr? Bitwise(string op, JsExpr left, JsExpr right, (int Bits, bool Unsigned) width)
    {
        if (width.Bits == 64) return null;
        if (width is (32, true))
        {
            return op switch
            {
                ">>" or ">>>" => JsExpr.Binary(left, ">>>", right),
                "&" or "|" or "^" => Wrap(JsExpr.Binary(left, op, right), width),
                _ => null,
            };
        }
        return op == ">>>" ? Wrap(JsExpr.Binary(left, ">>>", right), width) : null;
    }

    /// <summary>
    /// A shift of a 64-bit value as C# shifts it, whatever the context: the count is masked to six
    /// bits and made a BigInt (C# shifts a long by <c>count &amp; 63</c>, and a BigInt shifts only by a
    /// BigInt, so an int count threw a TypeError), and the bits a left shift pushes past 64 are
    /// discarded (<c>1L &lt;&lt; 63</c> is long.MinValue; a shift never throws, not even checked). JavaScript
    /// has no <c>&gt;&gt;&gt;</c> for a BigInt: a long shifts its unsigned 64-bit pattern and comes back signed,
    /// and a ulong, never negative, shifts logically with <c>&gt;&gt;</c>. The count arrives as C# wrote it,
    /// an int, with its value as <c>constant</c> when it has one.
    /// </summary>
    public static JsExpr LongShift(string op, JsExpr value, JsExpr count, object? constant, bool unsigned,
        ConversionContext context)
    {
        JsExpr shiftBy;
        if (constant is not null)
        {
            shiftBy = JsExpr.Literal($"{System.Convert.ToInt64(constant, System.Globalization.CultureInfo.InvariantCulture) & 63}n");
        }
        else
        {
            context.UsedHelpers.Add(Eq.Import);
            shiftBy = JsExpr.Call(JsExpr.Identifier(Eq.Long), JsExpr.Binary(count, "&", JsExpr.Literal("63")));
        }
        var bits = unsigned ? "BigInt.asUintN" : "BigInt.asIntN";
        return op switch
        {
            "<<" => JsExpr.Callish($"{bits}(64, {JsExprWriter.Write(JsExpr.Binary(value, "<<", shiftBy))})"),
            ">>>" when !unsigned => JsExpr.Callish(
                $"BigInt.asIntN(64, BigInt.asUintN(64, {JsExprWriter.Write(value)}) >> {JsExprWriter.WriteIn(shiftBy, JsPrecedence.Shift)})"),
            _ => JsExpr.Binary(value, ">>", shiftBy),
        };
    }

    /// <summary>Brings an arithmetic result of a fixed-width type into C#'s semantics for the
    /// context it sits in: a value C#'s compiler proved in range is the value as computed, a checked
    /// context throws past the edge, and any other wraps, as C# wraps in its default context.</summary>
    public static JsExpr Settle(JsExpr value, ITypeSymbol? resultType, ArithmeticContext arithmetic,
        ConversionContext context)
    {
        if (Of(resultType) is not { } width || arithmetic.ProvedInRange) return value;
        return arithmetic.IsChecked ? Checked(value, width, context) : Wrap(value, width);
    }

    /// <summary>
    /// <c>+</c>, <c>-</c>, <c>*</c> or <c>&lt;&lt;</c> of two values whose result is in
    /// <paramref name="width"/>, settled as <see cref="Settle"/> settles it. An int product goes through
    /// <c>Math.imul</c>, which wraps exactly where a double past 2^53 would have lost the low bits, and an
    /// int's <c>&lt;&lt;</c> is JavaScript's own, which already answers in signed 32 bits, as C# does.
    /// A constant C# proved in range is the operator as written, which computes the same number, except a
    /// uint's <c>&lt;&lt;</c>: JavaScript's answers SIGNED 32 bits, so <c>1u &lt;&lt; 31</c> came back
    /// -2147483648 where C# folds 2147483648, and it goes back to its width like any other.
    /// </summary>
    public static JsExpr Arithmetic(string op, JsExpr left, JsExpr right, (int Bits, bool Unsigned) width,
        ArithmeticContext arithmetic, ConversionContext context)
    {
        var plain = JsExpr.Binary(left, op, right);
        if (arithmetic.ProvedInRange && !(op == "<<" && width is (32, true))) return plain;
        if (arithmetic.IsChecked) return Checked(plain, width, context);
        if (width.Bits == 32 && op == "*")
        {
            var product = JsExpr.Callish($"Math.imul({JsExprWriter.Write(left)}, {JsExprWriter.Write(right)})");
            return width.Unsigned ? Wrap(product, width) : product;
        }
        return width is (32, false) && op == "<<" ? plain : Wrap(plain, width);
    }

    /// <summary>
    /// A quotient or a remainder settled in <paramref name="resultType"/>: a checked context throws past
    /// the edge, and otherwise it wraps where it can leave the width. One computed in int can leave a
    /// narrower one (<c>sbyte s = -128; s /= -1</c> is -128), and one computed in a wider type a uint;
    /// an int's and a long's stay in range, since <c>MinValue / -1</c> throws (<see cref="IntegerDivision"/>).
    /// </summary>
    public static JsExpr Quotient(JsExpr value, ITypeSymbol? resultType, ArithmeticContext arithmetic,
        ConversionContext context)
    {
        if (Of(resultType) is not { } width || arithmetic.ProvedInRange) return value;
        if (arithmetic.IsChecked) return Checked(value, width, context);
        return width.Bits < 32 || width is (32, true) ? Wrap(value, width) : value;
    }
}
