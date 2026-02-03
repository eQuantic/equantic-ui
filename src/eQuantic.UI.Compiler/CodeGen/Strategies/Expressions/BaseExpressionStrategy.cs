using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

public class BaseExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is BaseExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        return "super";
    }

    public int Priority => 10;
}
