using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// <c>GroupBy</c> in every selector shape: <c>(key)</c>, <c>(key, element)</c>,
/// <c>(key, result)</c>, <c>(key, element, result)</c>. Each IGrouping is the items array itself
/// with a <c>key</c> property attached, so a group works as a sequence (iterate, g.Select(…),
/// g.Count()) AND exposes g.Key — matching .NET; groups stay in first-occurrence key order, as
/// LINQ's do. The element selector transforms what goes INTO a group; the result selector maps
/// each finished group through <c>(key, group)</c>. Which role an argument plays is the parameter the
/// bound tree hands it to, named or not, and from lambda arity where nothing binds. Keys group by the
/// key type's equality (<see cref="LinqKeys"/>). A key comparer is the collection fence's to judge:
/// one that asks for that equality is dropped, and any other has no translation and is refused (EQ2007),
/// never dropped.
/// <para>
/// The element selector used to be silently ignored — <c>GroupBy(w => w.Length, w => w.ToUpper())</c>
/// grouped the raw words — which the query-syntax differential (<c>group w.ToUpper() by w.Length</c>
/// lowers to exactly that call) was the first to catch.
/// </para>
/// <para>
/// The names the reduce declares take a `$`, which no C# name holds: a key selector that read a captured
/// <c>key</c> met the reduce's own <c>const key</c> before it was set and threw (#397). A selector that is
/// not a lambda is evaluated once, before the reduce runs, where it ran once per element.
/// </para>
/// </summary>
public class GroupByStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return context.IsLinqMethod(node, "GroupBy");
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var source = LinqSource.Ir(memberAccess.Expression, context);
        var args = invocation.ArgumentList.Arguments;

        if (args.Count == 0) return source;

        // {0} is the source and the selectors follow it, in the order C# evaluates them: the order they
        // are written, whatever part each plays.
        var parts = new List<JsExpr> { source };
        int? keySelector = null;
        int? elementSelector = null;
        int? resultSelector = null;

        var bound = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;
        var roles = Roles(invocation, context);
        for (var i = 0; i < args.Count; i++)
        {
            switch (roles[i])
            {
                case "keySelector":
                    keySelector = parts.Count;
                    parts.Add(context.Converter.ConvertIr(args[i].Expression));
                    break;
                case "elementSelector":
                    elementSelector = parts.Count;
                    parts.Add(context.Converter.ConvertIr(args[i].Expression));
                    break;
                case "resultSelector":
                    resultSelector = parts.Count;
                    parts.Add(context.Converter.ConvertIr(args[i].Expression));
                    break;
                default:
                    // A key comparer is the collection fence's to judge (#578): one that asks for the
                    // key type's own equality, which the groups below are found by, is dropped, and any
                    // other is refused, as a ToDictionary's is. Every comparer was refused here,
                    // StringComparer.Ordinal included.
                    if (context.SemanticHelper.GetOperation(args[i].Expression) is not { } comparer)
                        return JsExpr.Opaque(context.Unhandled(invocation, "GroupBy with a comparer"));
                    if (comparer.RefusesAsUntranslatable("GroupBy", context)) return JsExpr.Opaque(invocation.ToString());
                    break;
            }
        }
        if (keySelector is not { } keyAt)
            return JsExpr.Opaque(context.Unhandled(invocation, "GroupBy without a key selector"));

        var pushed = elementSelector is { } element ? $"({{{element}}})($item)" : "$item";
        // A key that is an object here (a record, a date, a decimal) groups by its VALUE, as .NET's
        // default equality does: by === two equal records were two groups.
        var key = bound is { TypeArguments.Length: > 1 } ? bound.TypeArguments[1] : null;
        if (LinqKeys.ComparesByValue(key)) context.UsedHelpers.Add(Eq.Import);
        var grouped = "{0}.reduce(($groups, $item) => { " +
                      $"const $key = ({{{keyAt}}})($item); " +
                      $"let $g = $groups.find(($x) => {LinqKeys.Matches(key, "$x.key", "$key")}); " +
                      "if (!$g) { $g = []; $g.key = $key; $groups.push($g); } " +
                      $"$g.push({pushed}); return $groups; }}, [])";

        // A selector that is a lambda is written where it is called; the writer binds any other.
        return JsExpr.Template(
            resultSelector is { } result ? $"{grouped}.map(($g) => ({{{result}}})($g.key, $g))" : grouped,
            parts);
    }

    /// <summary>
    /// The role of each argument, by the position it is written at: the parameter the bound tree hands
    /// it to, so a named argument written out of order plays its own part,
    /// <c>GroupBy(comparer: c, keySelector: k)</c> included (#578). Read by position among the arguments
    /// the bound tree names, as <see cref="LinqTableStrategy"/> reads a key comparer, so a call a
    /// null-conditional rebuilt, whose arguments are copies, keeps its roles. The first argument was
    /// always the key selector and the rest were matched to the parameters from the end, so a named
    /// comparer was converted as the key selector and the key selector refused as a comparer. Without a
    /// binding, the first is the key selector and the lambda's arity names the rest: <c>(key, group)</c>
    /// is a result selector, a one-parameter lambda an element selector, anything else a comparer.
    /// </summary>
    private static IReadOnlyList<string> Roles(InvocationExpressionSyntax invocation, ConversionContext context)
    {
        var args = invocation.ArgumentList.Arguments;
        if (context.SemanticHelper.GetOperation(invocation) is IInvocationOperation call)
        {
            var written = (context.SemanticHelper.Original(invocation) as InvocationExpressionSyntax)?.ArgumentList.Arguments ?? args;
            var roles = new string?[args.Count];
            foreach (var argument in call.Arguments)
            {
                if (argument is { Syntax: ArgumentSyntax syntax, Parameter.Name: var name }
                    && written.IndexOf(syntax) is var at && at >= 0 && at < roles.Length)
                    roles[at] = name;
            }
            if (roles.All(role => role is not null)) return Array.ConvertAll(roles, role => role!);
        }

        return [.. args.Select((argument, at) => at == 0 ? "keySelector" : argument.Expression switch
        {
            SimpleLambdaExpressionSyntax => "elementSelector",
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } => "elementSelector",
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 2 } => "resultSelector",
            _ => "comparer",
        })];
    }

    public int Priority => 10;
}
