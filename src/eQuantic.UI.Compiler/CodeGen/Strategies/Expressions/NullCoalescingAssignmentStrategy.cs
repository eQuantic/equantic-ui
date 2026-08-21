using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for null-coalescing assignment operator.
/// Handles: x ??= y -> x = x ?? y (or x ?? (x = y))
/// </summary>
public class NullCoalescingAssignmentStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not AssignmentExpressionSyntax assignment)
            return false;

        return assignment.Kind() == SyntaxKind.CoalesceAssignmentExpression;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var assignment = (AssignmentExpressionSyntax)node;
        var left = context.Converter.ConvertExpression(assignment.Left);
        var right = context.Converter.ConvertExpression(assignment.Right);

        // x ??= y converts to: x ?? (x = y) — the target is named twice but evaluated once, and
        // assigned only when null/undefined. Built as a `??` NODE so the writer knows to fence it
        // off from a surrounding && or ||, which JavaScript refuses to parse beside it.
        return JsExpr.Binary(JsExpr.Opaque(left), "??", JsExpr.Opaque($"({left} = {right})"));
    }

    public int Priority => 15; // Higher than AssignmentExpressionStrategy (10)
}
