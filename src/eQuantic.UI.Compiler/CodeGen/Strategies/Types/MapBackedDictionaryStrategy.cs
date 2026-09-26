using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Shared emit surface for dictionaries that are backed by a runtime map class exposing the
/// <c>get</c>/<c>set</c>/<c>has</c>/<c>delete</c>/<c>clear</c>/<c>keys</c>/<c>values</c>/<c>size</c> +
/// <c>{key,value}</c>-iterator interface (rather than a plain JS object). Concrete subclasses only
/// declare WHICH dictionary types they own (<see cref="Matches"/>) and WHICH runtime factory to
/// construct (<see cref="FactoryFor"/>) — e.g. record-keyed dictionaries → <c>valueMap</c>, sorted
/// dictionaries → <c>sortedDictionary</c>/<c>sortedList</c>. This strategy owns construction, the
/// entry's plain assignment (<c>d[k] = v</c>), <c>ContainsKey</c>/<c>Add</c>/<c>Remove</c>/<c>Clear</c>/
/// <c>TryGetValue</c>/<c>GetValueOrDefault</c>, <c>Keys</c>/<c>Values</c>/<c>Count</c>, and <c>foreach</c>.
/// <para>
/// The ENTRY's read and its read-modify-writes are not here. A read, a compound, a step and <c>??=</c>
/// go where the plain dictionary's go, and take the runtime map's read and write from
/// <see cref="DictionaryEntry"/>: a text template of its own had answered all of them wrong, from
/// <c>m[k] *= 1 + 2</c> to <c>m[k]++</c>, which did not even parse.
/// </para>
///
/// Registered above the plain-object dictionary (20), indexer (1), member-access (0) and assignment
/// (10) strategies, so for an owned type it wins every node it claims.
/// </summary>
public abstract class MapBackedDictionaryStrategy : ConversionStrategyBase
{
    private static readonly string[] Methods =
        { "ContainsKey", "TryGetValue", "GetValueOrDefault", "Add", "Remove", "Clear" };

    /// <summary>True when this strategy owns dictionaries of <paramref name="type"/>.</summary>
    protected abstract bool Matches(ITypeSymbol? type);

    /// <summary>The <c>$eq.collections.*</c> factory used to construct an instance of <paramref name="type"/>.</summary>
    protected abstract string FactoryFor(ITypeSymbol? type);

    public override bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        switch (node)
        {
            case ObjectCreationExpressionSyntax oc:
                return Matches(context.SemanticHelper.GetType(oc));

            case ImplicitObjectCreationExpressionSyntax ioc:
                return Matches(context.SemanticHelper.GetType(ioc));

            case AssignmentExpressionSyntax { Left: ElementAccessExpressionSyntax la } assignment
                when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression):
                return ReceiverMatches(la.Expression, context);

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma } inv:
                return Methods.Contains(ma.Name.Identifier.Text) && ReceiverMatches(DictionaryOf(inv, ma, context), context);

            case MemberAccessExpressionSyntax member:
                return member.Name.Identifier.Text is "Keys" or "Values" or "Count"
                    && ReceiverMatches(member.Expression, context);

            default:
                return false;
        }
    }

    public override string Convert(SyntaxNode node, ConversionContext context)
    {
        switch (node)
        {
            case ObjectCreationExpressionSyntax oc:
                return BuildConstruction(oc.Initializer, FactoryFor(context.SemanticHelper.GetType(oc)), context);

            case ImplicitObjectCreationExpressionSyntax ioc:
                return BuildConstruction(ioc.Initializer, FactoryFor(context.SemanticHelper.GetType(ioc)), context);

            case AssignmentExpressionSyntax { Left: ElementAccessExpressionSyntax la } assignment:
            {
                // The entry's write answers the value, as C#'s assignment does: `set` answers the map.
                context.UsedHelpers.Add(Eq.Import);
                return DictionaryEntry.RuntimeMap.Write(
                    context.Converter.ConvertExpression(la.Expression),
                    context.Converter.ConvertExpression(la.ArgumentList.Arguments[0].Expression),
                    context.Converter.ConvertExpression(assignment.Right));
            }

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma } inv:
                return ConvertMethod(ma, inv, context);

            case MemberAccessExpressionSyntax member:
            {
                var receiver = context.Converter.ConvertExpression(member.Expression);
                return member.Name.Identifier.Text switch
                {
                    "Keys" => $"{receiver}.keys()",
                    "Values" => $"{receiver}.values()",
                    "Count" => $"{receiver}.size",
                    _ => $"{receiver}.{member.Name.Identifier.Text.ToCamelCase()}",
                };
            }

            default:
                return context.Unhandled(node, "Dictionary");
        }
    }

    private bool ReceiverMatches(ExpressionSyntax receiver, ConversionContext context) =>
        Matches(context.SemanticHelper.GetType(receiver));

    /// <summary>The dictionary a call reads: its receiver, or — for a lookup's static form,
    /// <c>CollectionExtensions.GetValueOrDefault(m, key)</c> — the argument that passes it, whose
    /// type is the one that says this is a runtime map.</summary>
    private static ExpressionSyntax DictionaryOf(
        InvocationExpressionSyntax invocation, MemberAccessExpressionSyntax member, ConversionContext context) =>
        member.Name.Identifier.Text is "TryGetValue" or "GetValueOrDefault"
        && DictionaryLookup.DictionaryOf(invocation, context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol) is { } dictionary
            ? dictionary
            : member.Expression;

    /// <summary>
    /// Emits <c>factory(...)</c>, seeding from a dictionary collection-initializer (<c>{ {k, v}, … }</c>
    /// or the indexed form <c>[k] = v</c>) as an array of <c>[key, value]</c> pairs.
    /// </summary>
    private static string BuildConstruction(
        InitializerExpressionSyntax? initializer, string factory, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);

        if (initializer == null || initializer.Expressions.Count == 0)
            return $"{factory}()";

        var pairs = new List<string>();
        foreach (var element in initializer.Expressions)
        {
            // Collection-initializer element: `{ key, value }`.
            if (element is InitializerExpressionSyntax { Expressions.Count: 2 } pair)
            {
                var k = context.Converter.ConvertExpression(pair.Expressions[0]);
                var v = context.Converter.ConvertExpression(pair.Expressions[1]);
                pairs.Add($"[{k}, {v}]");
            }
            // Indexed-element initializer: `[key] = value`.
            else if (element is AssignmentExpressionSyntax { Left: ImplicitElementAccessSyntax iea } assign
                     && iea.ArgumentList.Arguments.Count == 1)
            {
                var k = context.Converter.ConvertExpression(iea.ArgumentList.Arguments[0].Expression);
                var v = context.Converter.ConvertExpression(assign.Right);
                pairs.Add($"[{k}, {v}]");
            }
        }

        return $"{factory}([{string.Join(", ", pairs)}])";
    }

    private static string ConvertMethod(
        MemberAccessExpressionSyntax ma, InvocationExpressionSyntax invocation, ConversionContext context)
    {
        var args = invocation.ArgumentList.Arguments;
        var method = ma.Name.Identifier.Text;

        // The two lookups answer as .NET's do, and evaluate each argument once — DictionaryLookup.
        switch (method)
        {
            case "TryGetValue" when args.Count > 1:
                return DictionaryLookup.RuntimeMap.TryGetValue(invocation, context).ToString();
            case "GetValueOrDefault" when args.Count > 0:
                return DictionaryLookup.RuntimeMap.GetValueOrDefault(invocation, context).ToString();
        }

        var receiver = context.Converter.ConvertExpression(ma.Expression);
        switch (method)
        {
            case "ContainsKey" when args.Count > 0:
                return $"{receiver}.has({context.Converter.ConvertExpression(args[0].Expression)})";

            case "Add" when args.Count >= 2:
            {
                var k = context.Converter.ConvertExpression(args[0].Expression);
                var v = context.Converter.ConvertExpression(args[1].Expression);
                return $"{receiver}.set({k}, {v})";
            }

            case "Remove" when args.Count > 0:
                return $"{receiver}.delete({context.Converter.ConvertExpression(args[0].Expression)})";

            case "Clear":
                return $"{receiver}.clear()";

            default:
                var argList = string.Join(", ", args.Select(a => context.Converter.ConvertExpression(a.Expression)));
                return $"{receiver}.{method.ToCamelCase()}({argList})";
        }
    }

    public override int Priority => 25;
}
