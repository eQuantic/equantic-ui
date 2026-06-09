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
                    // The left side is a property name (`X`), not an expression to resolve — emit it
                    // directly as a camelCased object key (matching how records/initializers are built).
                    var left = (assignment.Left.ToString()).ToCamelCase();
                    var right = context.Converter.ConvertExpression(assignment.Right);
                    sb.Append($"{left}: {right}");
                }
            }
        }

        sb.Append(" }");
        return sb.ToString();
    }

    public int Priority => 10;
}
