using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// <c>GroupBy</c> in every selector shape: <c>(key)</c>, <c>(key, element)</c>,
/// <c>(key, result)</c>, <c>(key, element, result)</c>. Each IGrouping is the items array itself
/// with a <c>key</c> property attached, so a group works as a sequence (iterate, g.Select(…),
/// g.Count()) AND exposes g.Key — matching .NET; groups stay in first-occurrence key order, as
/// LINQ's do. The element selector transforms what goes INTO a group; the result selector maps
/// each finished group through <c>(key, group)</c>. Which role an argument plays is read from the
/// bound overload's parameter names, and from lambda arity where nothing binds. A custom key
/// comparer has no translation (keys group by <c>===</c>) and is fenced, never dropped.
/// <para>
/// The element selector used to be silently ignored — <c>GroupBy(w => w.Length, w => w.ToUpper())</c>
/// grouped the raw words — which the query-syntax differential (<c>group w.ToUpper() by w.Length</c>
/// lowers to exactly that call) was the first to catch.
/// </para>
/// </summary>
public class GroupByStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return context.IsLinqMethod(node, "GroupBy");
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var source = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments;

        if (args.Count == 0) return source;

        var keySelector = context.Converter.ConvertExpression(args[0].Expression);
        string? elementSelector = null;
        string? resultSelector = null;

        var parameters = (context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol)?.Parameters;
        for (var i = 1; i < args.Count; i++)
        {
            switch (Role(parameters, args.Count, i, args[i].Expression))
            {
                case "elementSelector":
                    elementSelector = context.Converter.ConvertExpression(args[i].Expression);
                    break;
                case "resultSelector":
                    resultSelector = context.Converter.ConvertExpression(args[i].Expression);
                    break;
                default:
                    context.Report(args[i], ConversionSeverity.Error, "EQ2008",
                        "GroupBy with a custom key comparer has no JavaScript translation — keys "
                        + "group by === here. Drop the comparer, or normalize the key inside the "
                        + "key selector.");
                    break;
            }
        }

        var pushed = elementSelector is null ? "item" : $"({elementSelector})(item)";
        var grouped = $"{source}.reduce((groups, item) => {{ " +
                      $"const key = ({keySelector})(item); " +
                      "let g = groups.find(x => x.key === key); " +
                      "if (!g) { g = []; g.key = key; groups.push(g); } " +
                      $"g.push({pushed}); return groups; }}, [])";

        return resultSelector is null
            ? grouped
            : $"{grouped}.map((g) => ({resultSelector})(g.key, g))";
    }

    /// <summary>The role of the argument after the key selector. The bound overload names it;
    /// without a binding the lambda's arity does — <c>(key, group)</c> is a result selector, a
    /// one-parameter lambda an element selector, anything else a comparer.</summary>
    private static string Role(ImmutableArray<IParameterSymbol>? parameters, int argCount, int index,
        ExpressionSyntax argument)
    {
        // Aligned from the END, so the reduced (receiver-less) and the static forms both map.
        if (parameters is { } bound && bound.Length >= argCount
            && bound[bound.Length - argCount + index].Name is ("elementSelector" or "resultSelector" or "comparer") and var name)
        {
            return name;
        }

        return argument switch
        {
            SimpleLambdaExpressionSyntax => "elementSelector",
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } => "elementSelector",
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 2 } => "resultSelector",
            _ => "comparer",
        };
    }

    public int Priority => 10;
}
