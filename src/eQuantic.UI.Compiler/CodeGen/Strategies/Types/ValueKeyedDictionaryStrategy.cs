using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Owns the whole emit surface of a <c>Dictionary&lt;TKey, TValue&gt;</c> whose KEY is a structural
/// value type (record / <c>struct</c> / value tuple). Such a key cannot index a plain JS object — the
/// object coerces it to a string via <c>toString</c>, collapsing distinct values — so this strategy
/// routes the dictionary to the runtime <c>$eq.collections.valueMap</c>, which keys by structural
/// equality (<c>$eq.equals</c>). String/number/enum-keyed dictionaries are left entirely to
/// <see cref="Invocation.DictionaryStrategy"/> (plain objects); this strategy never fires for them.
///
/// Type-gated on <see cref="TypeSymbolExtensions.IsValueKeyedDictionary"/> and registered above the
/// plain-object dictionary (20), indexer (1), member-access (0) and assignment (10) strategies, so for
/// a value-keyed dictionary it wins every relevant node:
/// <list type="bullet">
///   <item>construction <c>new Dictionary&lt;Point,int&gt;{ {p,1} }</c> → <c>$eq.collections.valueMap([[p, 1]])</c></item>
///   <item>indexer read <c>d[k]</c> → <c>d.get(k)</c>; assignment <c>d[k] = v</c> → <c>d.set(k, v)</c></item>
///   <item><c>ContainsKey/Add/Remove/Clear/TryGetValue/GetValueOrDefault</c> → <c>has/set/delete/clear/get</c></item>
///   <item><c>Keys/Values</c> → <c>keys()/values()</c>; <c>Count</c> → <c>size</c></item>
///   <item><c>foreach</c> iterates <c>{ key, value }</c> entries (the ValueMap is iterable)</item>
/// </list>
/// </summary>
public class ValueKeyedDictionaryStrategy : ConversionStrategyBase
{
    private static readonly string[] Methods =
        { "ContainsKey", "TryGetValue", "TryGetValueOrDefault", "GetValueOrDefault", "Add", "Remove", "Clear" };

    public override bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        switch (node)
        {
            case ObjectCreationExpressionSyntax oc:
                return context.SemanticHelper.GetType(oc).IsValueKeyedDictionary();

            case ImplicitObjectCreationExpressionSyntax ioc:
                return context.SemanticHelper.GetType(ioc).IsValueKeyedDictionary();

            // d[k] as a read (the assignment form is owned by the AssignmentExpression case below).
            case ElementAccessExpressionSyntax ea:
                return ReceiverIsValueDict(ea.Expression, context);

            // d[k] = v / d[k] += v — we own the whole assignment so we can emit `set`.
            case AssignmentExpressionSyntax { Left: ElementAccessExpressionSyntax la }:
                return ReceiverIsValueDict(la.Expression, context);

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma }:
                return Methods.Contains(ma.Name.Identifier.Text) && ReceiverIsValueDict(ma.Expression, context);

            case MemberAccessExpressionSyntax member:
                return member.Name.Identifier.Text is "Keys" or "Values" or "Count"
                    && ReceiverIsValueDict(member.Expression, context);

            default:
                return false;
        }
    }

    public override string Convert(SyntaxNode node, ConversionContext context)
    {
        switch (node)
        {
            case ObjectCreationExpressionSyntax oc:
                return BuildConstruction(oc.Initializer, context);

            case ImplicitObjectCreationExpressionSyntax ioc:
                return BuildConstruction(ioc.Initializer, context);

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

    private static bool ReceiverIsValueDict(ExpressionSyntax receiver, ConversionContext context) =>
        context.SemanticHelper.GetType(receiver).IsValueKeyedDictionary();

    /// <summary>
    /// Emits <c>$eq.collections.valueMap(...)</c>, seeding from a dictionary collection-initializer
    /// (<c>{ {k, v}, … }</c> or the indexed form <c>[k] = v</c>) as an array of <c>[key, value]</c> pairs.
    /// </summary>
    private static string BuildConstruction(InitializerExpressionSyntax? initializer, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);

        if (initializer == null || initializer.Expressions.Count == 0)
            return $"{Eq.ValueMap}()";

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

        return $"{Eq.ValueMap}([{string.Join(", ", pairs)}])";
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
