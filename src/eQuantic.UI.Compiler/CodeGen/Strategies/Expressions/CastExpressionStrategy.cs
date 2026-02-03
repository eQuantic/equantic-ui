using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

public class CastExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is CastExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var cast = (CastExpressionSyntax)node;
        var expr = context.Converter.ConvertExpression(cast.Expression);
        var type = cast.Type.ToString();

        // Specific cases for numeric truncation
        if (type == "int" || type == "long" || type == "short" || type == "byte")
        {
            return $"Math.trunc({expr})";
        }
        
        if (type == "string")
        {
            return $"String({expr})";
        }

        // Default passthrough for other types (compile-time assertion)
        return expr;
    }

    public int Priority => 10;
}
