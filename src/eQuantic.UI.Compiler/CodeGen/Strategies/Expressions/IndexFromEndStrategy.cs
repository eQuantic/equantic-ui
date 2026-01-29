using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for Index from end expressions (hat operator).
/// Handles:
/// - ^1 -> creates Index from end
/// - array[^1] -> array[array.length - 1]
/// - array[^2] -> array[array.length - 2]
/// </summary>
public class IndexFromEndStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        // Handle ^n expressions
        if (node is PrefixUnaryExpressionSyntax prefix &&
            prefix.IsKind(SyntaxKind.IndexExpression))
        {
            return true;
        }

        // Handle element access with ^n index: array[^1]
        if (node is ElementAccessExpressionSyntax elementAccess)
        {
            var arg = elementAccess.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            if (arg is PrefixUnaryExpressionSyntax indexExpr &&
                indexExpr.IsKind(SyntaxKind.IndexExpression))
            {
                return true;
            }
        }

        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        // Handle standalone ^n expression
        if (node is PrefixUnaryExpressionSyntax prefix &&
            prefix.IsKind(SyntaxKind.IndexExpression))
        {
            var operand = context.Converter.ConvertExpression(prefix.Operand);
            // Return as a marker that will be used by element access
            return $"__INDEX_FROM_END__({operand})";
        }

        // Handle array[^n] expression
        if (node is ElementAccessExpressionSyntax elementAccess)
        {
            var array = context.Converter.ConvertExpression(elementAccess.Expression);
            var arg = elementAccess.ArgumentList.Arguments.FirstOrDefault()?.Expression;

            if (arg is PrefixUnaryExpressionSyntax indexExpr &&
                indexExpr.IsKind(SyntaxKind.IndexExpression))
            {
                var offset = context.Converter.ConvertExpression(indexExpr.Operand);
                return $"{array}[{array}.length - {offset}]";
            }
        }

        return node.ToString();
    }

    public int Priority => 20; // Higher than ElementAccessStrategy
}
