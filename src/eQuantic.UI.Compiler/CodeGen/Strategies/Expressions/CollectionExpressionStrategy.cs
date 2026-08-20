using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for C# 12 Collection Expressions.
/// Handles:
/// - [1, 2, 3] -> [1, 2, 3]
/// - [..items, 4] -> [...items, 4]
/// - a SET target -> new Set([…]), because the elements are only half of what `[…]` means: the
///   TARGET TYPE says what is being built. `HashSet&lt;string&gt; _selected = ["#3841"]` lowered to a
///   plain array, and every `Add`/`Remove`/`Count` on it then threw at the first click.
/// </summary>
public class CollectionExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is CollectionExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
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

        var elements = collection.Elements
            .Where(element => element is not WithElementSyntax)
            .Select(e => ConvertElement(e, context));
        var array = $"[{string.Join(", ", elements)}]";

        // What the expression is CONVERTED to, not what it looks like: a collection expression takes
        // its shape from the target, exactly as it does in C#.
        var target = context.SemanticHelper.Knows(collection)
            ? context.SemanticModel!.GetTypeInfo(collection).ConvertedType
            : null;
        var definition = target?.OriginalDefinition?.ToString() ?? "";

        if (definition.StartsWith("System.Collections.Generic.SortedSet"))
        {
            context.UsedHelpers.Add(Eq.Import);
            return $"{Eq.SortedSet}({array})";
        }
        if (definition.StartsWith("System.Collections.Generic.HashSet")
            || definition.StartsWith("System.Collections.Generic.ISet")
            || definition.StartsWith("System.Collections.Generic.IReadOnlySet"))
            return $"new Set({array})";

        return array;
    }

    private string ConvertElement(CollectionElementSyntax element, ConversionContext context)
    {
        return element switch
        {
            ExpressionElementSyntax expr => context.Converter.ConvertExpression(expr.Expression),
            SpreadElementSyntax spread => $"...{context.Converter.ConvertExpression(spread.Expression)}",
            _ => context.Unhandled(element, "collection expression"),
        };
    }

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
