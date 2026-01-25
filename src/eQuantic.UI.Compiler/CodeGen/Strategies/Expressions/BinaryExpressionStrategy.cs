using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for binary expressions (operators).
/// Handles:
/// - == -> === (strict)
/// - != -> !==
/// - &&, || pass through
/// </summary>
public class BinaryExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is BinaryExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var binary = (BinaryExpressionSyntax)node;
        var left = context.Converter.ConvertExpression(binary.Left);
        var right = context.Converter.ConvertExpression(binary.Right);
        var op = binary.OperatorToken.Text;
        
        // Convert C# operators to JS equivalents
        // Use loose equality for null checks to catch both null and undefined
        if ((left == "null" || right == "null") && (op == "==" || op == "!="))
        {
            // Keep op as == or != (loose)
        }
        else
        {
            op = op switch
            {
                "&&" => "&&",
                "||" => "||",
                "==" => "===", // Use strict equality in JS for non-null
                "!=" => "!==",
                _ => op
            };
        }

        return $"{left} {op} {right}";
    }

    public int Priority => 0; 
}
