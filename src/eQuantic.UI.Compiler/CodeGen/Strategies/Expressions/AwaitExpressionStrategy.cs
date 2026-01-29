using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for await expressions.
/// Handles: await expr → await convertedExpr
/// </summary>
public class AwaitExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is AwaitExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var awaitExpr = (AwaitExpressionSyntax)node;
        var expression = context.Converter.ConvertExpression(awaitExpr.Expression);
        return $"await {expression}";
    }

    public int Priority => 10;
}
