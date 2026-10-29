using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// The two members an <c>ILookup&lt;TKey, TElement&gt;</c> answers by a key: its indexer
/// <c>lookup[key]</c> and <c>lookup.Contains(key)</c>. A lookup (from <see cref="LinqTableStrategy"/>'s
/// <c>ToLookup</c> / GroupBy) is represented as an array of groupings, each an items array carrying a
/// <c>key</c> property, so both search the groups for the one of that key: the indexer answers it, and,
/// matching .NET `ILookup`, an <b>empty sequence</b> (never throws) for an absent key; <c>Contains</c>
/// answers whether there is one. Equality is the key type's (<see cref="LinqKeys"/>), the one the groups
/// were built with. Priority above the generic element-access strategy and <see cref="ContainsStrategy"/>,
/// so it wins for lookup receivers: <c>Contains</c> was a collection's there, which looked for an ELEMENT
/// equal to the key among the groups and answered false for every key (#1145). The key is evaluated once,
/// before the search runs, where it ran once per group, and the search's name takes a `$` (#397).
/// </summary>
public class LookupStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context) => node switch
    {
        ElementAccessExpressionSyntax { ArgumentList.Arguments.Count: 1 } access =>
            IsLookup(context.SemanticHelper.GetType(access.Expression)),
        InvocationExpressionSyntax { ArgumentList.Arguments.Count: 1 } invocation =>
            invocation.TryGetInstanceCall(out _, out var name) && name.Identifier.ValueText == "Contains"
            && ContainsOf(invocation, context) is not null,
        _ => false,
    };

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        if (node is ElementAccessExpressionSyntax access)
        {
            var lookupType = context.SemanticHelper.GetType(access.Expression);
            return JsExpr.Template($"({Search("find", lookupType, context)} ?? [])",
                [context.Converter.ConvertIr(access.Expression), context.Converter.ConvertIr(access.ArgumentList.Arguments[0].Expression)]);
        }

        var invocation = (InvocationExpressionSyntax)node;
        invocation.TryGetInstanceCall(out var receiver, out _);
        return JsExpr.Template(Search("some", ContainsOf(invocation, context)!.ContainingType, context),
            [context.Converter.ConvertIr(receiver), context.Converter.ConvertIr(invocation.ArgumentList.Arguments[0].Expression)]);
    }

    /// <summary>The groups of the lookup <c>{0}</c> searched by <paramref name="method"/> for the one whose
    /// key is the key <c>{1}</c>, which the writer binds outside the search.</summary>
    private static string Search(string method, ITypeSymbol? lookupType, ConversionContext context)
    {
        var keyType = lookupType is INamedTypeSymbol { TypeArguments: [var first, ..] } ? first : null;
        return $"{{0}}.{method}(($g) => {LinqKeys.Matches(keyType, "$g.key", "{1}", context)})";
    }

    /// <summary>The lookup's own <c>Contains(key)</c> a call binds to, or null for any other: a LINQ
    /// <c>Contains</c> over a lookup's groups is <see cref="ContainsStrategy"/>'s.</summary>
    private static IMethodSymbol? ContainsOf(InvocationExpressionSyntax invocation, ConversionContext context) =>
        context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol { IsStatic: false } method && IsLookup(method.ContainingType)
            ? method
            : null;

    /// <summary>Whether a type is <c>System.Linq.ILookup&lt;TKey, TElement&gt;</c>, what <c>ToLookup</c>
    /// answers.</summary>
    private static bool IsLookup(ITypeSymbol? type) =>
        type is INamedTypeSymbol named
        && named.OriginalDefinition?.ContainingNamespace?.ToDisplayString() == "System.Linq"
        && named.OriginalDefinition.Name == "ILookup";

    public int Priority => 12;
}
