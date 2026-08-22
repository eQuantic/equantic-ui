using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// Where a C# <c>float</c> becomes single precision on this side. JavaScript computes in doubles;
/// ECMA-335 (I.12.1.3) lets the CLR carry a float INTERMEDIATE at higher precision too, and only
/// guarantees the rounding when the value is STORED — a local, a field, an argument — or compared.
/// So the compiler rounds at exactly those seams: a declaration or assignment of a float, a float
/// operand of a comparison, and <c>ToString</c> (the runtime's <c>single</c> rounds its input).
/// Every other float arithmetic stays a plain double expression, which keeps a layout line readable
/// and is within what the spec allows.
/// </summary>
public static class FloatStore
{
    /// <summary>The value rounded to single precision if it is a float COMPUTED here — an arithmetic
    /// expression, a negation — and the value itself otherwise (a literal, a name, a call already
    /// hand back a single).</summary>
    public static JsExpr Settle(ExpressionSyntax source, JsExpr converted, ConversionContext context)
    {
        if (context.SemanticHelper.GetType(source) is not { SpecialType: SpecialType.System_Single }) return converted;
        return Computes(source) ? Round(converted) : converted;
    }

    /// <summary>The value rounded to single precision.</summary>
    public static JsExpr Round(JsExpr value) => JsExpr.Callish($"Math.fround({JsExprWriter.Write(value)})");

    private static bool Computes(ExpressionSyntax source) => source switch
    {
        ParenthesizedExpressionSyntax parenthesized => Computes(parenthesized.Expression),
        BinaryExpressionSyntax binary => binary.Kind() is SyntaxKind.AddExpression or SyntaxKind.SubtractExpression
            or SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression or SyntaxKind.ModuloExpression,
        PrefixUnaryExpressionSyntax prefix => prefix.IsKind(SyntaxKind.UnaryMinusExpression) && Computes(prefix.Operand),
        ConditionalExpressionSyntax conditional => Computes(conditional.WhenTrue) || Computes(conditional.WhenFalse),
        _ => false,
    };
}
