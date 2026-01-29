using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for assignment expressions.
/// Handles:
/// - x = y
/// - x += y
/// - (var a, var b) = (1, 2)
/// </summary>
public class AssignmentExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is AssignmentExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var assignment = (AssignmentExpressionSyntax)node;
        var left = context.Converter.ConvertExpression(assignment.Left);
        var right = context.Converter.ConvertExpression(assignment.Right);
        var op = assignment.OperatorToken.Text;
        
        // Handle discard _ = ...
        if (left == "_" || left == "this._") return right;

        // If it's a declaration deconstruction, prefix with 'let ' if not already handled
        if (assignment.Left is DeclarationExpressionSyntax && !left.StartsWith("let "))
        {
            return $"let {left} {op} {right}";
        }

        return $"{left} {op} {right}";
    }

    public int Priority => 10;
}
