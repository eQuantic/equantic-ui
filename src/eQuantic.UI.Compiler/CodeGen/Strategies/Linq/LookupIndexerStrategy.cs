using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// The <c>ILookup&lt;TKey, TElement&gt;</c> indexer <c>lookup[key]</c>. A lookup (from
/// <see cref="ToLookupStrategy"/> / GroupBy) is represented as an array of groupings, each an items
/// array carrying a <c>key</c> property, so the indexer finds the matching group — and, matching .NET
/// `ILookup`, returns an <b>empty sequence</b> (never throws) for an absent key. Equality is by
/// <c>===</c>, consistent with how the groups were built (primitive keys). Priority above the generic
/// element-access strategy so it wins for lookup receivers.
/// </summary>
public class LookupIndexerStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not ElementAccessExpressionSyntax ea) return false;
        if (ea.ArgumentList.Arguments.Count != 1) return false;
        var type = context.SemanticHelper.GetType(ea.Expression);
        return type is INamedTypeSymbol named
            && named.OriginalDefinition?.ContainingNamespace?.ToDisplayString() == "System.Linq"
            && named.OriginalDefinition.Name == "ILookup";
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var ea = (ElementAccessExpressionSyntax)node;
        var lookup = context.Converter.ConvertExpression(ea.Expression);
        var key = context.Converter.ConvertExpression(ea.ArgumentList.Arguments[0].Expression);
        return $"({lookup}.find((g) => g.key === {key}) ?? [])";
    }

    public int Priority => 12;
}
