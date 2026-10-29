using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.CodeGen.Strategies.Types;

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
/// - a DICTIONARY, QUEUE, STACK or LINKED LIST target -> the runtime's class, as `new()` builds it
///   (#1144). None of them has the `Add` an element is added by, so C# allows only `[]` and
///   `with(…)`, whose capacity the class keeps. `Dictionary&lt;string, int&gt; ids = []`, the form the
///   IDE suggests for `new()`, went out as an array: its first write threw and its count was null.
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
        // pre-allocation hint an array or a set has no use for, dropped there and handed on to a class
        // of the runtime's that keeps one (Capacity below). Anything else (a comparer, above all)
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

        // What the expression is CONVERTED to, not what it looks like: a collection expression takes
        // its shape from the target, exactly as it does in C#.
        var target = context.SemanticHelper.GetConvertedType(collection);
        var definition = target?.OriginalDefinition?.ToString() ?? "";

        // An element reaches a dictionary, a queue, a stack or a linked list only through an Add of the
        // app's own, an extension C# calls per element, which nothing here writes: refused, where an
        // array was written.
        var kind = BoundaryShape.CollectionClass(target);
        var runtimeClass = target.IsDictionary() || kind is "queue" or "stack" or "linkedList";
        if (runtimeClass && collection.Elements.Any(element => element is not WithElementSyntax))
            return JsExpr.Opaque(context.Unhandled(collection, "collection expression"));
        if (target.IsDictionary()) return DictionaryStrategy.Empty(target!, Capacity(collection, context), context);
        if (runtimeClass) return QueueStackStrategy.Empty(kind!, Capacity(collection, context), context);

        var array = JsExpr.Array(collection.Elements
            .Where(element => element is not WithElementSyntax)
            .Select(e => ConvertElement(e, context))
            .ToList());

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

    private static JsExpr ConvertElement(CollectionElementSyntax element, ConversionContext context)
    {
        switch (element)
        {
            case ExpressionElementSyntax expr:
                return context.Converter.ConvertIr(expr.Expression);
            case SpreadElementSyntax spread:
                var before = context.Diagnostics.Count;
                var spreadExpr = context.Converter.ConvertIr(spread.Expression);
                if (context.Diagnostics.Count == before)
                    IterableTwin.FenceEnumeration(context.SemanticHelper.GetType(spread.Expression), spread.Expression, context);
                return JsExpr.Spread(spreadExpr);
            default:
                return JsExpr.Opaque(context.Unhandled(element, "collection expression"));
        }
    }

    /// <summary>The capacity <c>with(…)</c> hands the constructor, converted: its one argument, where the
    /// fence above lets it through. Null with no <c>with(…)</c>, and where the fence refused it.</summary>
    private static JsExpr? Capacity(CollectionExpressionSyntax collection, ConversionContext context) =>
        collection.Elements.OfType<WithElementSyntax>().FirstOrDefault() is { ArgumentList.Arguments: [var capacity] } with
        && WithArgumentsAreSemanticFree(with, context)
            ? context.Converter.ConvertIr(capacity.Expression)
            : null;

    /// <summary>Whether every <c>with(…)</c> argument is a capacity-style hint (integral, or named
    /// <c>capacity</c>) that dropping cannot change behaviour. A comparer is never that.</summary>
    private static bool WithArgumentsAreSemanticFree(WithElementSyntax with, ConversionContext context)
    {
        foreach (var argument in with.ArgumentList.Arguments)
        {
            if (argument.NameColon?.Name.Identifier.ValueText == "capacity") continue;

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
