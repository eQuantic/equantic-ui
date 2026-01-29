using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for Range expressions (C# 8.0).
/// Handles: 1..5 -> { start: 1, end: 5 }
/// </summary>
public class RangeExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is RangeExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var range = (RangeExpressionSyntax)node;
        
        var left = range.LeftOperand != null 
            ? context.Converter.ConvertExpression(range.LeftOperand) 
            : "0";
            
        var right = range.RightOperand != null 
            ? context.Converter.ConvertExpression(range.RightOperand) 
            : "null";

        // Simple runtime object representation
        return $"{{ start: {left}, end: {right} }}";
    }

    public int Priority => 10;
}
