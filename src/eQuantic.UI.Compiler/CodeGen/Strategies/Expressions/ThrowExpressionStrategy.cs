using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for Throw expressions (C# 7.0).
/// Handles: x ?? throw new Exception() -> x ?? (() => { throw new Exception(); })()
/// </summary>
public class ThrowExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is ThrowExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var throwExpr = (ThrowExpressionSyntax)node;
        var exception = context.Converter.ConvertExpression(throwExpr.Expression);
        
        // Wrap in IIFE to allow use as expression
        return $"(() => {{ throw {exception}; }})()";
    }

    public int Priority => 10;
}
