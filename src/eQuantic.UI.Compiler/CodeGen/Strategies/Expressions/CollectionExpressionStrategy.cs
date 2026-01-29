using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for C# 12 Collection Expressions.
/// Handles:
/// - [1, 2, 3] -> [1, 2, 3]
/// - [..items, 4] -> [...items, 4]
/// </summary>
public class CollectionExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is CollectionExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var collection = (CollectionExpressionSyntax)node;
        var elements = collection.Elements.Select(e => ConvertElement(e, context));
        return $"[{string.Join(", ", elements)}]";
    }

    private string ConvertElement(CollectionElementSyntax element, ConversionContext context)
    {
        return element switch
        {
            ExpressionElementSyntax expr => context.Converter.ConvertExpression(expr.Expression),
            SpreadElementSyntax spread => $"...{context.Converter.ConvertExpression(spread.Expression)}",
            _ => throw new NotSupportedException($"Unknown collection element: {element.GetType().Name}")
        };
    }

    public int Priority => 10;
}
