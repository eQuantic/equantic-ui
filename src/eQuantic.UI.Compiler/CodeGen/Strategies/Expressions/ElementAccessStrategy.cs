using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for element access (indexers).
/// Handles: dict[key], array[0]
/// </summary>
public class ElementAccessStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is ElementAccessExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var elementAccess = (ElementAccessExpressionSyntax)node;
        var expr = context.Converter.ConvertExpression(elementAccess.Expression);
        
        // Convert indexer arguments
        var args = string.Join(", ", elementAccess.ArgumentList.Arguments
            .Select(arg => context.Converter.ConvertExpression(arg.Expression)));

        return $"{expr}[{args}]";
    }

    public int Priority => 1;
}
