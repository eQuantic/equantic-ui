using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for converting lambda expressions.
/// Handles:
/// - () => expr
/// - (a, b) => { stmt; }
/// - x => x + 1
/// </summary>
public class LambdaExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is LambdaExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        if (node is ParenthesizedLambdaExpressionSyntax parenthesized)
        {
            var parameters = string.Join(", ", parenthesized.ParameterList.Parameters.Select(p => p.Identifier.Text));
            var body = parenthesized.Block != null 
                ? context.Converter.ConvertBlock(parenthesized.Block) 
                : context.Converter.ConvertExpression(parenthesized.ExpressionBody!);
            return $"({parameters}) => {body}";
        }
        
        if (node is SimpleLambdaExpressionSyntax simple)
        {
            var param = simple.Parameter.Identifier.Text;
            var body = simple.Block != null 
                ? context.Converter.ConvertBlock(simple.Block) 
                : context.Converter.ConvertExpression(simple.ExpressionBody!);
            return $"({param}) => {body}";
        }
        
        return "() => {}";
    }

    public int Priority => 10;
}
