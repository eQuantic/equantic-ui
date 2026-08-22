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
        ConversionContext context)
    {
        var kind = conversion.GetConversion();
        var from = conversion.Operand.Type;
        var to = conversion.Type;

        // A constant converts at compile time ONLY where the JavaScript REPRESENTATION changes —
        // `1` flowing into a long is `1n`. An int constant flowing into a byte or a double is
        // already the number JavaScript wants, and folding it would rewrite the author's notation
        // (0x10 is not 16 to a reader) for no gain.
        if (conversion.ConstantValue.HasValue && Constant(conversion.ConstantValue.Value, to) is { } folded)
            return folded;

        // A USER-DEFINED implicit operator passes the value through, which is what the JavaScript
        // model of these types already is: `SizeValue`, `Index`, `ColorToken` and their kin wrap a
        // single primitive, and their twin IS that primitive — so `Size = 34f` and `name[..cut]`
        // cross by carrying the number. A wrapper with real structure would need its operator
        // lowered and called; no such conversion is reachable from transpiled code today, and the
        // conformance suite is where that would show up as a divergence, not here as a refusal.
        if (kind.IsUserDefined)
            return kind.MethodSymbol is { } method
                && UserDefinedOperators.Conversion(method, JsExprWriter.Write(translated)) is { } call
                ? call
                : translated;

        if (kind.IsNumeric)
            return Numeric(from, to, translated,
                conversion.Operand.ConstantValue.HasValue ? conversion.Operand.ConstantValue.Value : null, context);

        if (kind.IsEnumeration && to is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType && !enumType.IsFlagsEnum())
        {
            // Only the literal 0 converts implicitly to an enum: the member that is 0, by name.
            var zero = enumType.GetMembers().OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.HasConstantValue && System.Convert.ToInt64(f.ConstantValue, CultureInfo.InvariantCulture) == 0);
            return zero is null ? translated : JsExpr.Literal($"'{zero.Name.ToCamelCase()}'");
        }

        // Identity, reference, boxing, nullable wrapping, null/default literals, method groups,
        // lambdas, interpolated strings, tuples: the value is already what JavaScript needs.
        return translated;
    }

    /// <summary>A numeric conversion between the primitives JavaScript represents differently:
    /// a char is a 1-length string, a long a BigInt, a decimal a Decimal, a float a rounded double.</summary>
    private static JsExpr Numeric(ITypeSymbol? from, ITypeSymbol? to, JsExpr translated, object? constant,
        ConversionContext context)
    {
        var text = JsExprWriter.WriteIn(translated, JsPrecedence.Call);
        var value = translated;
        if (from is { SpecialType: SpecialType.System_Char })
        {
            // A constant char folds to its code unit: `'A' + col` reads as 65 + col, the way the
            // hand-written rule this replaced always emitted it.
            value = constant is char c
                ? JsExpr.Literal(((int)c).ToString(CultureInfo.InvariantCulture))
                : JsExpr.Callish($"{text}.charCodeAt(0)");
            text = JsExprWriter.Write(value);
        }

        switch (to?.SpecialType)
        {
            case SpecialType.System_Int64 or SpecialType.System_UInt64 when !from.IsLong():
                context.UsedHelpers.Add(Eq.Import);
                return JsExpr.Callish($"{Eq.Long}({text})");
            // DECIMAL is still the binary strategy's: it wraps both operands on its way to the
            // runtime Decimal, and settling here as well would wrap twice. Moves over next.
            case SpecialType.System_Decimal:
                return value;
            case SpecialType.System_Single or SpecialType.System_Double when from.IsLong():
                var widened = JsExpr.Callish($"Number({text})");
                return to.SpecialType == SpecialType.System_Single ? FloatStore.Round(widened) : widened;
            // A DOUBLE narrowing to a float genuinely loses precision, so it rounds. An INTEGER
            // widening to one does not — every int a UI computes is exact as a single — and
            // rounding it would put a fround around half the layout arithmetic (FloatStore: the
            // rounding belongs at the store, not at every step).
            case SpecialType.System_Single when from is { SpecialType: SpecialType.System_Double }:
                return FloatStore.Round(value);
            default:
                return value;
        }
    }

    private static JsExpr? Constant(object? constant, ITypeSymbol? to) => to?.SpecialType switch
    {
        // A long is a BigInt: the literal carries the suffix, or nothing else will.
        SpecialType.System_Int64 or SpecialType.System_UInt64 =>
            JsExpr.Literal(System.Convert.ToString(constant, CultureInfo.InvariantCulture) + "n"),
        _ => null,
    };
}
