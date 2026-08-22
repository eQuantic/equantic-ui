using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// What happens to a value on its way to its PARENT in the bound tree — the one place where C#'s
/// implicit conversions live. Every syntax strategy translates a node as written; this settles
/// the translation by what the bound tree says flows around it: the implicit conversion Roslyn
/// inserted (<see cref="IConversionOperation"/> with <c>IsImplicit</c> — char to int, int to
/// long, a boxing on its way into a string), the interpolation hole that formats it, the string
/// concatenation that prints it. One mechanism, applied at the dispatcher after every expression,
/// so a conversion the syntax never shows is honoured at every site C# applies it — arguments,
/// returns, initializers, branches — not only where a strategy remembered to look.
/// <para>
/// The strangler rule: a syntax strategy must NOT apply a conversion this settles, or the value
/// converts twice. What it settles is listed here and nowhere else.
/// </para>
/// </summary>
public static class ValueFlow
{
    /// <summary>The translation of <paramref name="node"/>, settled for where it flows.</summary>
    public static JsExpr Settle(ExpressionSyntax node, JsExpr translated, ConversionContext context)
    {
        var operation = context.SemanticHelper.GetOperation(node);
        if (operation is null) return translated;

        // A VALUE ON ITS WAY INTO TEXT converts by C#'s rules — a null string prints as nothing, a
        // bool as "True", an enum as its member name (StringConversion). The bound tree shows the
        // three ways a value gets there: boxed into a concatenation (`"v=" + n`, `s += flag`), a
        // string operand of one (no conversion — a string is already a string), or the expression
        // of an interpolation hole, which has no conversion either (the handler is generic). Only
        // the VALUE of a compound assignment flows; its target is being written, not printed.
        if (FlowsIntoText(operation)) return StringConversion.ToDotNetString(node, translated, context);

        if (operation.Parent is not IConversionOperation { IsImplicit: true } conversion) return translated;
        if (!ReferenceEquals(conversion.Operand, operation)) return translated;

        // TWO CHARS COMPARED stay characters. C# promotes both to int and compares the code units;
        // JavaScript compares 1-length strings by the same code units, in the same order — so
        // `text[i] >= 'A'` is the comparison C# means, and spelling it charCodeAt on both sides
        // would only make the generated code harder to read.
        if (BothOperandsAreChars(conversion)) return translated;

        return Convert(conversion, node, translated, context);
    }

    /// <summary>Whether this conversion is one side of a comparison whose BOTH sides are chars.</summary>
    private static bool BothOperandsAreChars(IConversionOperation conversion)
    {
        if (conversion.Operand.Type is not { SpecialType: SpecialType.System_Char }) return false;
        if (conversion.Parent is not IBinaryOperation binary) return false;
        if (binary.OperatorKind is not (BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals
            or BinaryOperatorKind.LessThan or BinaryOperatorKind.LessThanOrEqual
            or BinaryOperatorKind.GreaterThan or BinaryOperatorKind.GreaterThanOrEqual)) return false;
        return Unconverted(binary.LeftOperand) is { SpecialType: SpecialType.System_Char }
            && Unconverted(binary.RightOperand) is { SpecialType: SpecialType.System_Char };
    }

    /// <summary>The type of an operand BEFORE the implicit conversion wrapping it.</summary>
    private static ITypeSymbol? Unconverted(IOperation operand) =>
        operand is IConversionOperation { IsImplicit: true } conversion ? conversion.Operand.Type : operand.Type;

    /// <summary>Whether this operation's value is on its way into TEXT: through the boxing a
    /// concatenation wraps it in, directly as a string operand of one, or as a plain hole of an
    /// interpolated string (a hole with a format or an alignment hands the raw value to the
    /// formatter instead).</summary>
    private static bool FlowsIntoText(IOperation operation)
    {
        var parent = operation.Parent;
        if (parent is IConversionOperation { IsImplicit: true } boxing && ReferenceEquals(boxing.Operand, operation))
        {
            operation = boxing;
            parent = boxing.Parent;
        }
        return parent switch
        {
            IBinaryOperation { OperatorKind: BinaryOperatorKind.Add, Type.SpecialType: SpecialType.System_String } => true,
            ICompoundAssignmentOperation { OperatorKind: BinaryOperatorKind.Add, Type.SpecialType: SpecialType.System_String } compound
                => ReferenceEquals(compound.Value, operation),
            IInterpolationOperation { FormatString: null, Alignment: null } hole => ReferenceEquals(hole.Expression, operation),
            _ => false,
        };
    }

    /// <summary>An implicit conversion, by what Roslyn classified it as.</summary>
    private static JsExpr Convert(IConversionOperation conversion, ExpressionSyntax node, JsExpr translated,
        ConversionContext context) =>
        Apply(conversion.GetConversion(), conversion.Operand.Type, conversion.Type,
            conversion.ConstantValue.HasValue ? conversion.ConstantValue.Value : null,
            conversion.Operand.ConstantValue.HasValue ? conversion.Operand.ConstantValue.Value : null,
            translated, context, conversion.IsChecked);

    /// <summary>
    /// A conversion APPLIED to a translated value — the one table, usable wherever the bound tree
    /// reports a conversion: the implicit conversion around an expression, the element conversion
    /// of a <c>foreach</c>, the explicit conversion a cast spells out. Implicit and explicit are
    /// the same table because the PAIR of types decides — a narrowing pair only ever arrives from
    /// an explicit conversion, so no flag is needed; <paramref name="isChecked"/> is the one thing
    /// the pair cannot say (a checked cast throws where the unchecked one wraps).
    /// </summary>
    public static JsExpr Apply(Conversion kind, ITypeSymbol? from, ITypeSymbol? to, object? convertedConstant,
        object? operandConstant, JsExpr translated, ConversionContext context, bool isChecked = false)
    {
        // A constant converts at compile time ONLY where the JavaScript REPRESENTATION changes —
        // `1` flowing into a long is `1n`. An int constant flowing into a byte or a double is
        // already the number JavaScript wants, and folding it would rewrite the author's notation
        // (0x10 is not 16 to a reader) for no gain.
        if (convertedConstant is not null && Constant(convertedConstant, to) is { } folded)
            return folded;

        if (kind.IsUserDefined)
            return kind.MethodSymbol is { } method
                && UserDefinedOperators.Conversion(method, JsExprWriter.Write(translated)) is { } call
                ? call
                : translated;

        // A NULLABLE conversion converts the underlying value and lets null pass: `(int?)aDouble?`
        // is null for a null, the numeric narrowing otherwise. The bound tree names only the
        // lifted conversion; the unwrapped types say what happens under it. A non-nullable SOURCE
        // cannot be null, so its value converts directly.
        if (kind.IsNullable)
        {
            var fromUnwrapped = from.UnwrapNullable();
            var toUnwrapped = to.UnwrapNullable();
            if (!ReferenceEquals(fromUnwrapped, from))
            {
                var name = JsExpr.Identifier("__v");
                var converted = Numeric(fromUnwrapped, toUnwrapped, name, null, isChecked, context);
                if (ReferenceEquals(converted, name)) return translated;   // identity under the lift
                return JsExpr.Callish($"((__v) => __v == null ? null : {JsExprWriter.Write(converted)})"
                    + $"({JsExprWriter.Write(translated)})");
            }
            return Numeric(fromUnwrapped, toUnwrapped, translated, operandConstant, isChecked, context);
        }

        if (kind.IsNumeric) return Numeric(from, to, translated, operandConstant, isChecked, context);

        if (kind.IsEnumeration && to is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType && !enumType.IsFlagsEnum())
        {
            // Only the literal 0 converts implicitly to an enum: the member that is 0, by name.
            var zero = enumType.GetMembers().OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.HasConstantValue && System.Convert.ToInt64(f.ConstantValue, CultureInfo.InvariantCulture) == 0);
            return zero is null ? translated : JsExpr.Literal($"'{zero.Name.ToCamelCase()}'");
        }

        // Identity, reference, boxing, null/default literals, method groups, lambdas,
        // interpolated strings, tuples: the value is already what JavaScript needs.
        return translated;
    }

    /// <summary>A numeric conversion between the primitives JavaScript represents differently:
    /// a char is a 1-length string, a long a BigInt, a decimal a Decimal, a float a rounded
    /// double, and every fixed-width integer a plain number that must wrap (or throw, checked)
    /// where C# says it does.</summary>
    private static JsExpr Numeric(ITypeSymbol? from, ITypeSymbol? to, JsExpr translated, object? constant,
        bool isChecked, ConversionContext context)
    {
        var value = translated;
        var fromSpecial = from?.SpecialType ?? SpecialType.None;

        // A DECIMAL SOURCE has no invariant representation yet (a negated literal is a plain
        // number, a hydrated one a string) — coerce like every decimal use site does, then ask for
        // its number once and narrow or widen like any double. And a decimal shrinking into an
        // integral type ALWAYS throws past the edge in C# — checked or not — so the check rides
        // along. (Only an explicit cast gets here — implicit decimal conversions all go INTO
        // decimal, which still yields below.)
        if (fromSpecial == SpecialType.System_Decimal)
        {
            context.UsedHelpers.Add(Eq.Import);
            value = JsExpr.Callish($"{Eq.Dec}({JsExprWriter.Write(value)}).toNumber()");
            fromSpecial = SpecialType.System_Double;
            isChecked = true;
        }

        // A char is a 1-length string: its number is the code unit. A constant char folds to it:
        // `'A' + col` reads as 65 + col, the way the hand-written rule this replaced emitted it.
        if (fromSpecial == SpecialType.System_Char)
        {
            value = constant is char c
                ? JsExpr.Literal(((int)c).ToString(CultureInfo.InvariantCulture))
                : JsExpr.Callish($"{JsExprWriter.WriteIn(value, JsPrecedence.Call)}.charCodeAt(0)");
        }

        // DECIMAL as the TARGET is still the binary strategy's: it wraps both operands on its way
        // to the runtime Decimal, and settling here as well would wrap twice. Moves with typed
        // hydration.
        if (to is { SpecialType: SpecialType.System_Decimal }) return value;

        return fromSpecial is SpecialType.System_Int64 or SpecialType.System_UInt64
            ? FromLong(fromSpecial, to, value, isChecked, context)
            : FromNumber(fromSpecial, to, value, isChecked, context);
    }

    /// <summary>A BigInt on its way to another representation: a plain number for the double
    /// family, the width's slice of its bits for a narrower integer, the code unit's character
    /// for char — or the overflow a checked context throws.</summary>
    private static JsExpr FromLong(SpecialType from, ITypeSymbol? to, JsExpr value, bool isChecked,
        ConversionContext context)
    {
        var text = JsExprWriter.WriteIn(value, JsPrecedence.Call);
        var target = to?.SpecialType ?? SpecialType.None;
        switch (target)
        {
            case SpecialType.System_Single:
                return FloatStore.Round(JsExpr.Callish($"Number({text})"));
            case SpecialType.System_Double:
                return JsExpr.Callish($"Number({text})");
            case SpecialType.System_Char:
                return JsExpr.Callish(isChecked
                    ? $"String.fromCharCode(Number({Checked(value, (16, true), context)}))"
                    : $"String.fromCharCode(Number(BigInt.asUintN(16, {text})))");
            case SpecialType.System_Int64 or SpecialType.System_UInt64 when target != from:
                // long ↔ ulong reinterprets the same 64 bits; a checked context throws instead.
                var toUnsigned = target == SpecialType.System_UInt64;
                return isChecked
                    ? IntegerWidth.Checked(value, (64, toUnsigned), context)
                    : IntegerWidth.Wrap(value, (64, toUnsigned));
            default:
                if (IntegerWidth.Of(target) is { Bits: < 64 } width)
                    return JsExpr.Callish(isChecked
                        ? $"Number({Checked(value, width, context)})"
                        : $"Number(BigInt.{(width.Unsigned ? "asUintN" : "asIntN")}({width.Bits}, {text}))");
                return value;
        }
    }

    /// <summary>A plain number on its way to another representation: a BigInt for the long
    /// family, truncated and wrapped into a narrower width (a widening pair passes through
    /// untouched), the code unit's character for char, rounded once for a genuine loss of float
    /// precision — or the overflow a checked context throws.</summary>
    private static JsExpr FromNumber(SpecialType from, ITypeSymbol? to, JsExpr value, bool isChecked,
        ConversionContext context)
    {
        // A fractional source truncates toward zero FIRST — C# rounds the value before it checks
        // or wraps it, so `checked((byte)255.9)` is 255, not an overflow.
        var fractional = from is SpecialType.System_Single or SpecialType.System_Double;
        var target = to?.SpecialType ?? SpecialType.None;

        switch (target)
        {
            case SpecialType.System_Int64 or SpecialType.System_UInt64:
                context.UsedHelpers.Add(Eq.Import);
                var unsigned64 = target == SpecialType.System_UInt64;
                var range = isChecked
                    ? Checked(value, (64, unsigned64), context)
                    : JsExprWriter.WriteIn(value, JsPrecedence.Call);
                var asLong = JsExpr.Callish($"{Eq.Long}({range})");
                // A signed (or fractional) source reinterprets into ulong's 64 bits — `(ulong)-1`
                // is 2^64-1, not -1n. A checked context threw instead; an unsigned source fits.
                return !isChecked && unsigned64 && (fractional || WidthOfSource(from) is not { Unsigned: true })
                    ? IntegerWidth.Wrap(asLong, (64, true))
                    : asLong;
            case SpecialType.System_Char:
                var unit = fractional && isChecked ? Truncate(value) : value;
                return JsExpr.Callish(isChecked
                    ? $"String.fromCharCode({Checked(unit, (16, true), context)})"
                    : $"String.fromCharCode({JsExprWriter.WriteIn(unit, JsPrecedence.Call)})");
            // A DOUBLE narrowing to a float genuinely loses precision, so it rounds. An INTEGER
            // widening to one does not — every int a UI computes is exact as a single — and
            // rounding it would put a fround around half the layout arithmetic (FloatStore: the
            // rounding belongs at the store, not at every step).
            case SpecialType.System_Single when from is SpecialType.System_Double:
                return FloatStore.Round(value);
            default:
                if (IntegerWidth.Of(target) is not { } width) return value;
                var whole = fractional ? Truncate(value) : value;
                if (isChecked) return IntegerWidth.Checked(whole, width, context);
                // The same masks every wrapped RESULT uses (IntegerWidth) — a pair whose source
                // range already fits the target needs none: char into an int is its code unit.
                return !fractional && WidthOfSource(from) is { } source && Fits(source, width)
                    ? whole
                    : IntegerWidth.Wrap(whole, width);
        }
    }

    /// <summary>The value truncated toward zero — the integer part C# takes from a double.</summary>
    private static JsExpr Truncate(JsExpr value) =>
        JsExpr.Callish($"Math.trunc({JsExprWriter.WriteIn(value, JsPrecedence.Call)})");

    /// <summary>The checked-cast call: the value, or the OverflowException C# throws.</summary>
    private static string Checked(JsExpr value, (int Bits, bool Unsigned) width, ConversionContext context) =>
        JsExprWriter.Write(IntegerWidth.Checked(value, width, context));

    /// <summary>The width whose RANGE bounds a source value: a char is a 16-bit unsigned code
    /// unit, the fixed-width integers are themselves, anything else is unbounded (null).</summary>
    private static (int Bits, bool Unsigned)? WidthOfSource(SpecialType from) => from == SpecialType.System_Char
        ? (16, true)
        : IntegerWidth.Of(from);

    /// <summary>Whether every value of the source width is already a value of the target width —
    /// the wrap would be a no-op mask on half the arithmetic in a page.</summary>
    private static bool Fits((int Bits, bool Unsigned) from, (int Bits, bool Unsigned) to) => from.Unsigned
        ? to.Bits > from.Bits || (to.Bits == from.Bits && to.Unsigned)
        : !to.Unsigned && to.Bits >= from.Bits;

    private static JsExpr? Constant(object? constant, ITypeSymbol? to) => to?.SpecialType switch
    {
        // A long is a BigInt: the literal carries the suffix, or nothing else will.
        SpecialType.System_Int64 or SpecialType.System_UInt64 =>
            JsExpr.Literal(System.Convert.ToString(constant, CultureInfo.InvariantCulture) + "n"),
        _ => null,
    };
}
