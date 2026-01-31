using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Strategy for Union LINQ method.
/// Handles: source.Union(other) -> [...new Set([...source, ...other])]
/// Removes duplicates from the concatenation of two sequences.
/// </summary>
public class UnionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return context.SemanticHelper.IsLinqMethod(node, "Union");
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var source = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments;

        if (args.Count == 0) return source;

        var other = context.Converter.ConvertExpression(args[0].Expression);

        // source.Union(other) -> [...new Set([...source, ...other])]
        // Creates a set to remove duplicates, then spreads back to array
        return $"[...new Set([...{source}, ...{other}])]";
    }

    public int Priority => 10;
}
