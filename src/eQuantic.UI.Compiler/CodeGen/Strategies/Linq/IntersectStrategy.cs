using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Strategy for Intersect LINQ method.
/// Handles: source.Intersect(other) -> source.filter(x => other.includes(x))
/// Returns elements that exist in both sequences.
/// </summary>
public class IntersectStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return context.SemanticHelper.IsLinqMethod(node, "Intersect");
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var source = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments;

        if (args.Count == 0) return source;

        var other = context.Converter.ConvertExpression(args[0].Expression);

        // source.Intersect(other) -> [...new Set(source)].filter(x => other.includes(x))
        // Use Set to remove duplicates from source, then filter by other
        return $"[...new Set({source})].filter(x => {other}.includes(x))";
    }

    public int Priority => 10;
}
