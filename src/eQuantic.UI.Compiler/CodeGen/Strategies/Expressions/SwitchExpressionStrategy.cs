using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

public class SwitchExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is SwitchExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var switchExpr = (SwitchExpressionSyntax)node;
        var governingExpr = context.Converter.ConvertExpression(switchExpr.GoverningExpression);
        
        var sb = new StringBuilder();
        // C# switch expression can be mapped to a series of nested ternary operators 
        // or a self-invoking function with a switch.
        // Nested ternaries are cleaner for simple expressions.
        
        int openParens = 0;
        for (int i = 0; i < switchExpr.Arms.Count; i++)
        {
            var arm = switchExpr.Arms[i];
            var armExpr = context.Converter.ConvertExpression(arm.Expression);
            
            if (arm.Pattern is ConstantPatternSyntax constantPattern)
            {
                var patternVal = context.Converter.ConvertExpression(constantPattern.Expression);
                sb.Append($"({governingExpr} === {patternVal} ? {armExpr} : ");
                openParens++;
            }
            else if (arm.Pattern is DiscardPatternSyntax)
            {
                sb.Append(armExpr);
                break; // Discard is always the last active arm
            }
            
            // If it's the last arm and didn't catch (and no discard was found), provide a null fallback
            if (i == switchExpr.Arms.Count - 1 && arm.Pattern is not DiscardPatternSyntax)
            {
                sb.Append("null");
            }
        }
        
        // Close all open parentheses
        sb.Append(new string(')', openParens));
        
        return sb.ToString();
    }

    public int Priority => 0;
}
