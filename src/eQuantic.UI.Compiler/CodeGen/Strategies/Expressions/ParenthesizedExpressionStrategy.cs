using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

public class ParenthesizedExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is ParenthesizedExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var parens = (ParenthesizedExpressionSyntax)node;
        return $"({context.Converter.ConvertExpression(parens.Expression)})";
    }

    public int Priority => 10;
}
