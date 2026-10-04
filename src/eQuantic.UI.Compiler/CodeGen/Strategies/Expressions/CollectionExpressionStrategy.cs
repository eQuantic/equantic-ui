using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for C# 12 Collection Expressions.
/// Handles:
/// - [1, 2, 3] -> [1, 2, 3]
/// - [..items, 4] -> [...items, 4]
/// - a SET target -> the runtime's set, each element added in turn as C# adds it, found by the
///   element type's equality, because the elements are only half of what `[…]` means: the TARGET
///   TYPE says what is being built. `HashSet&lt;string&gt; _selected = ["#3841"]` lowered to a plain
///   array, and every `Add`/`Remove`/`Count` on it then threw at the first click.
/// <para>
/// Built as IR (#384): the elements are the lists a screen composes, `children: [ … ]`, and a
/// lambda among them reaches the writer as an arrow whose block maps line by line.
/// </para>
/// </summary>
public class CollectionExpressionStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is CollectionExpressionSyntax;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var collection = (CollectionExpressionSyntax)node;

        // C# 15 `with(…)` element: constructor arguments for the built collection. Capacity is a
        // pre-allocation hint with no JS meaning — dropped. Anything else (a comparer, above all)
        // CHANGES what the collection considers equal, and a JS array/Set has no such knob: that is
        // an error, not a silent drop — `[with(OrdinalIgnoreCase), "Hello", "HELLO"]` keeping two
        // elements instead of one is a wrong answer nothing would flag.
        foreach (var with in collection.Elements.OfType<WithElementSyntax>())
        {
            if (!WithArgumentsAreSemanticFree(with, context))
            {
                context.Report(with, ConversionSeverity.Error, "EQ2007",
                    "'with(…)' collection arguments beyond a capacity hint have no JavaScript "
                    + "translation — a JS array/Set takes no constructor comparer. Drop the "
                    + "argument, or build the collection explicitly.");
            }
        }

        var array = JsExpr.Array(collection.Elements
            .Where(element => element is not WithElementSyntax)
            .Select(e => ConvertElement(e, context))
            .ToList());

        // What the expression is CONVERTED to, not what it looks like: a collection expression takes
        // its shape from the target, exactly as it does in C#.
        var target = context.SemanticHelper.GetConvertedType(collection);
        var definition = target?.OriginalDefinition?.ToString() ?? "";

        if (definition.StartsWith("System.Collections.Generic.SortedSet"))
        {
            // In its element type's order (ValueOrdering), as a constructed one is.
            context.UsedHelpers.Add(Eq.Import);
            return target is INamedTypeSymbol { TypeArguments: [var element] } && ValueOrdering.Of(element) is { } ordering
                ? JsExpr.Call(JsExpr.Identifier(Eq.SortedSet), array, JsExpr.Literal(ordering))
                : JsExpr.Call(JsExpr.Identifier(Eq.SortedSet), array);
        }
        if (definition.StartsWith("System.Collections.Generic.HashSet")
            || definition.StartsWith("System.Collections.Generic.ISet")
            || definition.StartsWith("System.Collections.Generic.IReadOnlySet"))
        {
            context.UsedHelpers.Add(Eq.Import);
            var element = target is INamedTypeSymbol { TypeArguments: [var item] } ? item : null;
            return ElementEquality.Of(element) is { } equality
                ? JsExpr.Call(JsExpr.Identifier(Eq.HashSetOf), array, JsExpr.Literal(equality))
                : JsExpr.Call(JsExpr.Identifier(Eq.HashSetOf), array);
        }

        return array;
    }

    private static JsExpr ConvertElement(CollectionElementSyntax element, ConversionContext context) => element switch
    {
        ExpressionElementSyntax expr => context.Converter.ConvertIr(expr.Expression),
        SpreadElementSyntax spread => JsExpr.Spread(context.Converter.ConvertIr(spread.Expression)),
        _ => JsExpr.Opaque(context.Unhandled(element, "collection expression")),
    };

    /// <summary>Whether every <c>with(…)</c> argument is a capacity-style hint (integral, or named
    /// <c>capacity</c>) that dropping cannot change behaviour. A comparer is never that.</summary>
    private static bool WithArgumentsAreSemanticFree(WithElementSyntax with, ConversionContext context)
    {
        foreach (var argument in with.ArgumentList.Arguments)
        {
            if (argument.NameColon?.Name.Identifier.Text == "capacity") continue;

            var type = context.SemanticHelper.GetType(argument.Expression);
            if (type?.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64
                or SpecialType.System_UInt32 or SpecialType.System_Int16 or SpecialType.System_Byte)
                continue;
            if (type is null && argument.Expression is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.NumericLiteralExpression))
                continue;

            return false;
        }

        return true;
    }

    public int Priority => 10;
}
