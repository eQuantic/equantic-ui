using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

public class WithExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is WithExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var withExpr = (WithExpressionSyntax)node;
        var receiver = context.Converter.ConvertExpression(withExpr.Expression);

        var sb = new StringBuilder();
        sb.Append($"{{ ...{receiver}");

        if (withExpr.Initializer is InitializerExpressionSyntax initializer)
        {
            foreach (var expr in initializer.Expressions)
            {
                if (expr is AssignmentExpressionSyntax assignment)
                {
                    sb.Append(", ");
                    var left = context.Converter.ConvertExpression(assignment.Left);
                    var right = context.Converter.ConvertExpression(assignment.Right);
                    // Handle "this." prefix removal if the converter adds it inappropriately to property names in object literals
                    // though usually ConvertExpression handles identifiers. 
                    // For object literals { Prop: Val }, Prop is typically an identifier. 
                    // But here it's an assignment in C# { Prop = Val }.
                    // We need 'Prop: Val' in JS.
                    
                    // Simple hack: if left is "this.Prop", take "Prop".
                    if (left.StartsWith("this.")) left = left.Substring(5);
                    
                    sb.Append($"{left}: {right}");
                }
            }
        }
        
        sb.Append(" }");
        return sb.ToString();
    }

    public int Priority => 10;
}
