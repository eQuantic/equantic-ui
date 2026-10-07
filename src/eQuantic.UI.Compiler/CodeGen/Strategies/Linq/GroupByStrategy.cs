using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// <c>GroupBy</c> in every selector shape: <c>(key)</c>, <c>(key, element)</c>,
/// <c>(key, result)</c>, <c>(key, element, result)</c>. Each IGrouping is the items array itself
/// with a <c>key</c> property attached, so a group works as a sequence (iterate, g.Select(…),
/// g.Count()) AND exposes g.Key — matching .NET; groups stay in first-occurrence key order, as
/// LINQ's do. The element selector transforms what goes INTO a group; the result selector maps
/// each finished group through <c>(key, group)</c>. Which role an argument plays is read from the
/// bound overload's parameter names, and from lambda arity where nothing binds. Keys group by the
/// key type's equality (<see cref="LinqKeys"/>). A key comparer is the collection fence's to judge:
/// one that asks for that equality is dropped, and any other has no translation and is refused (EQ2007),
/// never dropped.
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
        var source = LinqSource.Text(memberAccess.Expression, context);
        var args = invocation.ArgumentList.Arguments;

        if (args.Count == 0) return source;

        var keySelector = context.Converter.ConvertExpression(args[0].Expression);
        string? elementSelector = null;
        string? resultSelector = null;

        var bound = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;
        var parameters = bound?.Parameters;
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
                    // A key comparer is the collection fence's to judge (#578): one that asks for the
                    // key type's own equality, which the groups below are found by, is dropped, and any
                    // other is refused, as a ToDictionary's is. Every comparer was refused here,
                    // StringComparer.Ordinal included.
                    if (context.SemanticHelper.GetOperation(args[i].Expression) is not { } comparer)
                        return context.Unhandled(invocation, "GroupBy with a comparer");
                    if (comparer.RefusesAsUntranslatable("GroupBy", context)) return invocation.ToString();
                    break;
            }
        }

        var pushed = elementSelector is null ? "item" : $"({elementSelector})(item)";
        // A key that is an object here (a record, a date, a decimal) groups by its VALUE, as .NET's
        // default equality does: by === two equal records were two groups.
        var key = bound is { TypeArguments.Length: > 1 } ? bound.TypeArguments[1] : null;
        if (LinqKeys.ComparesByValue(key)) context.UsedHelpers.Add(Eq.Import);
        var grouped = $"{source}.reduce((groups, item) => {{ " +
                      $"const key = ({keySelector})(item); " +
                      $"let g = groups.find(x => {LinqKeys.Matches(key, "x.key", "key")}); " +
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
