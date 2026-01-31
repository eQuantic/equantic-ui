using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for null-coalescing assignment operator.
/// Handles: x ??= y -> x = x ?? y (or x ?? (x = y))
/// </summary>
public class NullCoalescingAssignmentStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not AssignmentExpressionSyntax assignment)
            return false;

        return assignment.Kind() == SyntaxKind.CoalesceAssignmentExpression;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var assignment = (AssignmentExpressionSyntax)node;
        var left = context.Converter.ConvertExpression(assignment.Left);
        var right = context.Converter.ConvertExpression(assignment.Right);

        // x ??= y converts to: x ?? (x = y)
        // This ensures x is evaluated once and assigned only if null/undefined
        return $"{left} ?? ({left} = {right})";
    }

    public int Priority => 15; // Higher than AssignmentExpressionStrategy (10)
}
