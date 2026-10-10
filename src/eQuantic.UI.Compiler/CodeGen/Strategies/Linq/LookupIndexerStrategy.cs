using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// The <c>ILookup&lt;TKey, TElement&gt;</c> indexer <c>lookup[key]</c>. A lookup (from
/// <see cref="LinqTableStrategy"/>'s <c>ToLookup</c> / GroupBy) is represented as an array of groupings, each an items
/// array carrying a <c>key</c> property, so the indexer finds the matching group — and, matching .NET
/// `ILookup`, returns an <b>empty sequence</b> (never throws) for an absent key. Equality is the key
/// type's (<see cref="LinqKeys"/>), the one the groups were built with. Priority above the generic
/// element-access strategy so it wins for lookup receivers. The key is evaluated once, before the
/// search runs, where it ran once per group, and the search's name takes a `$` (#397).
/// </summary>
public class LookupIndexerStrategy : IExpressionIrStrategy
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

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var ea = (ElementAccessExpressionSyntax)node;
        var lookup = context.Converter.ConvertIr(ea.Expression);
        var key = context.Converter.ConvertIr(ea.ArgumentList.Arguments[0].Expression);
        var keyType = context.SemanticHelper.GetType(ea.Expression) is INamedTypeSymbol { TypeArguments: [var first, ..] }
            ? first
            : null;
        if (LinqKeys.ComparesByValue(keyType)) context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Template($"({{0}}.find(($g) => {LinqKeys.Matches(keyType, "$g.key", "{1}")}) ?? [])", [lookup, key]);
    }

    public int Priority => 12;
}
