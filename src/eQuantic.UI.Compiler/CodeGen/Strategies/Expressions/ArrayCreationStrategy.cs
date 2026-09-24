using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for array creation.
/// Handles:
/// - new[] { 1, 2, 3 }      → [1, 2, 3]   (implicit)
/// - new int[] { 1, 2, 3 }  → [1, 2, 3]   (explicit, with initializer)
/// - new T[5]               → new Array(5).fill(default(T))  (sized, default-initialized), or one
///   zero struct per element (see <see cref="Sized"/>)
/// </summary>
public class ArrayCreationStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is ArrayCreationExpressionSyntax or ImplicitArrayCreationExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var initializer = node switch
        {
            ImplicitArrayCreationExpressionSyntax implicitArray => implicitArray.Initializer,
            ArrayCreationExpressionSyntax array => array.Initializer,
            _ => null
        };

        // Initialized array → JS array literal.
        if (initializer != null)
        {
            var elements = initializer.Expressions.Select(e => context.Converter.ConvertExpression(e));
            return $"[{string.Join(", ", elements)}]";
        }

        // Sized array without initializer: new T[n] → new Array(n).fill(<default for T>).
        if (node is ArrayCreationExpressionSyntax sized
            && sized.Type.RankSpecifiers.Count > 0
            && sized.Type.RankSpecifiers[0].Sizes.Count > 0
            && sized.Type.RankSpecifiers[0].Sizes[0] is { } sizeExpr
            && sizeExpr is not OmittedArraySizeExpressionSyntax)
        {
            return Sized(sized, context.Converter.ConvertExpression(sizeExpr), context);
        }

        return "[]";
    }

    /// <summary>
    /// A sized array, each element its type's default as the semantic model gives it (#380): the
    /// default a field of that type starts with, a long's <c>0n</c>, a decimal's zero, a char's
    /// <c>'\0'</c>, an enum's zero member, a struct's zero instance. The element type's SPELLING
    /// decided before, so a long and a decimal started as a plain 0, a char, an enum and a struct as
    /// null, and a type not written as its keyword (<c>Int64</c>) as null too. A struct's zero is an
    /// object, and <c>fill</c> puts ONE object in every slot, so a write through one element showed
    /// through all of them: each slot builds its own. Every other default is a value nothing
    /// mutates, and one fills them all.
    /// </summary>
    private static string Sized(ArrayCreationExpressionSyntax sized, string size, ConversionContext context)
    {
        var fill = context.SemanticHelper.GetType(sized) is IArrayTypeSymbol array
            ? Strategies.DefaultValue.Of(array.ElementType, context)
            : Unbound(sized.Type, context);
        return fill.StartsWith("new ", StringComparison.Ordinal)
            ? $"Array.from({{ length: {size} }}, () => {fill})"
            : $"new Array({size}).fill({fill})";
    }

    /// <summary>The fill with no model to ask: an array of arrays (<c>new int[2][]</c>) holds nulls,
    /// and anything else is what the element type's name says.</summary>
    private static string Unbound(ArrayTypeSyntax type, ConversionContext context)
    {
        if (type.RankSpecifiers.Count > 1) return "null";
        var fill = TypeDeclarationExtensions.DefaultFor(type.ElementType);
        if (fill.Contains("$eq.")) context.UsedHelpers.Add(Eq.Import);
        return fill;
    }

    public int Priority => 0;
}
