using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Strategy for LINQ <c>ToLookup(keySelector[, elementSelector])</c>. Mirrors the
/// <see cref="GroupByStrategy"/> representation — an array of <c>{ key, items }</c> groupings (so
/// <c>.Count</c>/iteration behave the same) — applying the optional element selector to each item.
/// (The <c>ILookup[key]</c> indexer is not modelled, consistent with GroupBy's grouping shape.)
/// </summary>
public class ToLookupStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return context.SemanticHelper.IsLinqMethod(node, "ToLookup");
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var source = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments;
        if (args.Count == 0) return source;

        var keySelector = context.Converter.ConvertExpression(args[0].Expression);
        var elementSelector = args.Count > 1
            ? context.Converter.ConvertExpression(args[1].Expression)
            : "x => x";

        return $"{source}.reduce((map, item) => {{ " +
               $"var key = ({keySelector})(item); " +
               "var entry = map.find(e => e.key === key); " +
               "if (!entry) { entry = { key, items: [] }; map.push(entry); } " +
               $"entry.items.push(({elementSelector})(item)); return map; }}, [])";
    }

    public int Priority => 10;
}
