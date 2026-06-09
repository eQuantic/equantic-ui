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

        // Build the changed members as `camelKey: value` entries from the initializer.
        var entries = new StringBuilder();
        if (withExpr.Initializer is InitializerExpressionSyntax initializer)
        {
            foreach (var expr in initializer.Expressions)
            {
                if (expr is AssignmentExpressionSyntax assignment)
                {
                    if (entries.Length > 0) entries.Append(", ");
                    // The left side is a property name (`X`) — emit it as a camelCased object key.
                    var key = assignment.Left.ToString().ToCamelCase();
                    var value = context.Converter.ConvertExpression(assignment.Right);
                    entries.Append($"{key}: {value}");
                }
            }
        }

        // Records are JS classes — copy via their generated `with` so the prototype (methods) survives.
        // Other value shapes (non-record structs, anonymous types) are plain objects — spread is fine.
        if (context.SemanticHelper.GetType(withExpr.Expression) is { IsRecord: true })
        {
            return $"{receiver}.with({{ {entries} }})";
        }
        return $"{{ ...{receiver}, {entries} }}";
    }

    public int Priority => 10;
}
