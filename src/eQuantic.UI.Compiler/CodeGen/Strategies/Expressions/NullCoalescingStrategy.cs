using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for null-coalescing expressions.
/// Handles:
/// - ?? (null-coalescing): a ?? b -> a ?? b (JS supports this natively since ES2020)
/// - ??= (null-coalescing assignment): a ??= b -> a ??= b
/// </summary>
public class NullCoalescingStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is BinaryExpressionSyntax binary &&
               binary.IsKind(SyntaxKind.CoalesceExpression);
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var binary = (BinaryExpressionSyntax)node;
        var left = context.Converter.ConvertExpression(binary.Left);
        var right = context.Converter.ConvertExpression(binary.Right);

        // JavaScript ?? operator works the same as C# ??
        return $"{left} ?? {right}";
    }

    public int Priority => 10; // Higher than BinaryExpressionStrategy
}
