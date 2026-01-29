using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for conditional expressions (ternary).
/// Handles: condition ? a : b
/// </summary>
public class ConditionalExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is ConditionalExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var conditional = (ConditionalExpressionSyntax)node;
        var condition = context.Converter.ConvertExpression(conditional.Condition);
        var whenTrue = context.Converter.ConvertExpression(conditional.WhenTrue);
        var whenFalse = context.Converter.ConvertExpression(conditional.WhenFalse);
        
        return $"{condition} ? {whenTrue} : {whenFalse}";
    }

    public int Priority => 10;
}
