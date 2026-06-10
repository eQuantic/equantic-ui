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
/// dictionaries → <c>sortedDictionary</c>/<c>sortedList</c>. The whole node surface is owned uniformly:
/// construction, indexer read (<c>d[k]</c> → <c>get</c>) and assignment (<c>d[k] = v</c> / <c>op=</c> →
/// <c>set</c>), <c>ContainsKey</c>/<c>Add</c>/<c>Remove</c>/<c>Clear</c>/<c>TryGetValue</c>/
/// <c>GetValueOrDefault</c>, <c>Keys</c>/<c>Values</c>/<c>Count</c>, and <c>foreach</c>.
///
/// Registered above the plain-object dictionary (20), indexer (1), member-access (0) and assignment
/// (10) strategies, so for an owned type it wins every relevant node.
/// </summary>
public abstract class MapBackedDictionaryStrategy : ConversionStrategyBase
{
    private static readonly string[] Methods =
        { "ContainsKey", "TryGetValue", "TryGetValueOrDefault", "GetValueOrDefault", "Add", "Remove", "Clear" };

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

            case ElementAccessExpressionSyntax ea:
                return ReceiverMatches(ea.Expression, context);

            case AssignmentExpressionSyntax { Left: ElementAccessExpressionSyntax la }:
                return ReceiverMatches(la.Expression, context);

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma }:
                return Methods.Contains(ma.Name.Identifier.Text) && ReceiverMatches(ma.Expression, context);

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

            case ElementAccessExpressionSyntax ea:
            {
                var receiver = context.Converter.ConvertExpression(ea.Expression);
                var key = context.Converter.ConvertExpression(ea.ArgumentList.Arguments[0].Expression);
                return $"{receiver}.get({key})";
            }

            case AssignmentExpressionSyntax { Left: ElementAccessExpressionSyntax la } assignment:
            {
                var receiver = context.Converter.ConvertExpression(la.Expression);
                var key = context.Converter.ConvertExpression(la.ArgumentList.Arguments[0].Expression);
                var value = context.Converter.ConvertExpression(assignment.Right);
                var op = assignment.OperatorToken.Text;

                // Compound assignment `d[k] op= v` → `d.set(k, d.get(k) op v)`.
                if (op != "=")
                {
                    var binaryOp = op.TrimEnd('=');
                    value = $"{receiver}.get({key}) {binaryOp} {value}";
                }
                return $"{receiver}.set({key}, {value})";
            }

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma } inv:
                return ConvertMethod(ma, inv.ArgumentList.Arguments, context);

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
                return node.ToString();
        }
    }

    private bool ReceiverMatches(ExpressionSyntax receiver, ConversionContext context) =>
        Matches(context.SemanticHelper.GetType(receiver));

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
        MemberAccessExpressionSyntax ma, SeparatedSyntaxList<ArgumentSyntax> args, ConversionContext context)
    {
        var receiver = context.Converter.ConvertExpression(ma.Expression);
        var method = ma.Name.Identifier.Text;

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

            case "GetValueOrDefault" when args.Count > 0:
            {
                var k = context.Converter.ConvertExpression(args[0].Expression);
                var def = args.Count > 1 ? context.Converter.ConvertExpression(args[1].Expression) : "null";
                return $"({receiver}.get({k}) ?? {def})";
            }

            case "TryGetValue" or "TryGetValueOrDefault" when args.Count > 1:
            {
                var k = context.Converter.ConvertExpression(args[0].Expression);
                var outVar = ExtractOutVar(args[1], context);
                return $"({outVar} = {receiver}.get({k})) !== undefined";
            }

            default:
                var argList = string.Join(", ", args.Select(a => context.Converter.ConvertExpression(a.Expression)));
                return $"{receiver}.{method.ToCamelCase()}({argList})";
        }
    }

    /// <summary>The receiving variable name of a <c>TryGetValue(key, out var x)</c> out-argument.</summary>
    private static string ExtractOutVar(ArgumentSyntax outArg, ConversionContext context)
    {
        if (outArg.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword))
        {
            return outArg.Expression is DeclarationExpressionSyntax decl
                ? decl.Designation.ToString()
                : outArg.Expression.ToString().Trim();
        }
        return context.Converter.ConvertExpression(outArg.Expression);
    }

    public override int Priority => 25;
}
