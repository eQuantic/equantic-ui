using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Strategy for LINQ <c>ToLookup(keySelector[, elementSelector])</c>. Mirrors the
/// <see cref="GroupByStrategy"/> representation — an array of groupings, each being the items array
/// with a <c>key</c> property — so groups iterate and expose <c>.Key</c>, applying the optional element
/// selector to each item. (The <c>ILookup[key]</c> indexer is not modelled — use iteration / First.)
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

        return $"{source}.reduce((groups, item) => {{ " +
               $"const key = ({keySelector})(item); " +
               "let g = groups.find(x => x.key === key); " +
               "if (!g) { g = []; g.key = key; groups.push(g); } " +
               $"g.push(({elementSelector})(item)); return groups; }}, [])";
    }

    public int Priority => 10;
}
