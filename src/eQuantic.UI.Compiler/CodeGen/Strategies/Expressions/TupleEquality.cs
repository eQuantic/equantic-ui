using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// C#'s <c>==</c> and <c>!=</c> between two value tuples, which the C# compiler lowers to the
/// ELEMENTS' own operators, left to right: a number by value (a NaN element is unequal to itself,
/// where the tuple's <c>Equals</c> holds it equal), a decimal, a date or a record by its value, a
/// type with an <c>==</c> of its own by that operator, an array or a class by reference, and a
/// nested tuple the same way. A tuple and an array are both arrays on this side, so no runtime
/// helper can tell them apart: the lowering happens here, where the element types are known.
/// </summary>
internal static class TupleEquality
{
    /// <summary>The comparison of two tuples, or of two nullable tuples (equal when both are absent),
    /// each operand evaluated once and in order. Null when the operands are not both tuples.</summary>
    public static JsExpr? Lower(BinaryExpressionSyntax binary, string op, JsExpr leftIr, JsExpr rightIr,
        ConversionContext context)
    {
        if (op is not ("==" or "!=")) return null;
        var leftType = context.SemanticHelper.GetType(binary.Left);
        var rightType = context.SemanticHelper.GetType(binary.Right);
        if (leftType.UnwrapNullable() is not INamedTypeSymbol { IsTupleType: true } left
            || rightType.UnwrapNullable() is not INamedTypeSymbol { IsTupleType: true } right)
            return null;

        if (Elements(left, right, "$a", "$b", context) is not { } elements)
            return JsExpr.Opaque(context.Unhandled(binary, $"tuple {op} over {left.ToDisplayString()}"));
        var body = leftType.IsNullableValue() || rightType.IsNullableValue()
            ? $"$a == null || $b == null ? $a == null && $b == null : {elements}"
            : elements;
        var compare = $"(($a, $b) => {body})({{0}}, {{1}})";
        return JsExpr.Template(op == "==" ? compare : $"!{compare}", new[] { leftIr, rightIr }, context.TypeAnnotations);
    }

    /// <summary>Each pair of elements by its own <c>==</c>, joined as C# joins them; null when an
    /// element has an operator of its own that no twin carries.</summary>
    private static string? Elements(INamedTypeSymbol left, INamedTypeSymbol right, string a, string b,
        ConversionContext context)
    {
        var comparisons = new List<string>();
        var rightElements = right.TupleElements;
        for (var i = 0; i < left.TupleElements.Length && i < rightElements.Length; i++)
        {
            if (Element(left.TupleElements[i].Type, rightElements[i].Type, $"{a}[{i}]", $"{b}[{i}]", context)
                is not { } comparison)
                return null;
            comparisons.Add(comparison);
        }
        return comparisons.Count == 0 ? "true" : string.Join(" && ", comparisons);
    }

    private static string? Element(ITypeSymbol left, ITypeSymbol right, string a, string b, ConversionContext context)
    {
        if (left.UnwrapNullable() is INamedTypeSymbol { IsTupleType: true } nestedLeft
            && right.UnwrapNullable() is INamedTypeSymbol { IsTupleType: true } nestedRight)
        {
            return Elements(nestedLeft, nestedRight, a, b, context) is { } nested
                ? left.IsNullableValue() || right.IsNullableValue()
                    ? $"({a} == null || {b} == null ? {a} == null && {b} == null : {nested})"
                    : $"({nested})"
                : null;
        }

        var type = left.UnwrapNullable() ?? left;
        // A type of the app's own with an == it wrote: that operator, which its twin carries. A
        // record's == is written by the compiler, and is its value, below.
        if (type.GetMembers("op_Equality").OfType<IMethodSymbol>()
                .FirstOrDefault(method => !method.IsImplicitlyDeclared && method.Parameters.Length == 2) is { } equality
            && UserDefinedOperators.IsInSource(equality))
        {
            return UserDefinedOperators.Binary(equality, "==", a, b) is { } call ? JsExprWriter.Write(call) : null;
        }
        // A decimal, a date, a record or a struct is an object here whose == is its value.
        if (LinqKeys.ComparesByValue(type))
        {
            context.UsedHelpers.Add(Eq.Import);
            return $"{Eq.Equals}({a}, {b})";
        }
        // A number, a bool, a char, a string, an enum's name, a long, and every reference.
        return $"{a} === {b}";
    }
}
