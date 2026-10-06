using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for Index from end expressions (hat operator).
/// Handles:
/// - array[^1] -> array[array.length - 1]
/// - array[^2] -> array[array.length - 2]
/// <para>
/// An indexer a twin carries is not here: <c>ring[^1]</c> over a type that counts its elements is its
/// <c>item</c> at the count the bound tree names (<see cref="Place"/>), and a type that declares
/// <c>this[Index]</c> takes the index itself. This read <c>ring.length</c>, which no twin has, and
/// wrote the bare index into <c>setItem</c>.
/// </para>
/// <para>
/// A <c>^n</c> that is no array's, list's or string's index is a System.Index VALUE (<c>Index i =
/// ^1</c>, a <c>this[Index]</c> key), which has no JavaScript translation: it is refused (EQ1004), where
/// it was written as a call to <c>__INDEX_FROM_END__</c>, which nothing defines.
/// </para>
/// </summary>
public class IndexFromEndStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        // Handle ^n expressions
        if (node is PrefixUnaryExpressionSyntax prefix &&
            prefix.IsKind(SyntaxKind.IndexExpression))
        {
            return true;
        }

        // Handle element access with ^n index: array[^1]. A dictionary keyed by Index is not one:
        // `d[^1]` looks the key up (DictionaryEntry), where this counted back from a length a map
        // does not have. Nor is an indexer a twin carries.
        if (node is ElementAccessExpressionSyntax elementAccess
            && DictionaryEntry.Of(elementAccess, context) is null
            && Indexer.LoweredAt(elementAccess, context) is null)
        {
            var arg = elementAccess.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            if (arg is PrefixUnaryExpressionSyntax indexExpr &&
                indexExpr.IsKind(SyntaxKind.IndexExpression))
            {
                return true;
            }
        }

        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        // Handle array[^n] expression
        if (node is ElementAccessExpressionSyntax elementAccess)
        {
            var array = context.Converter.ConvertExpression(elementAccess.Expression);
            var arg = elementAccess.ArgumentList.Arguments.FirstOrDefault()?.Expression;

            if (arg is PrefixUnaryExpressionSyntax indexExpr &&
                indexExpr.IsKind(SyntaxKind.IndexExpression))
            {
                var offset = context.Converter.ConvertExpression(indexExpr.Operand);
                return $"{array}[{array}.length - {offset}]";
            }
        }

        // A standalone ^n is an Index value.
        return context.Unhandled(node, "index-from-end");
    }

    public int Priority => 20; // Higher than ElementAccessStrategy
}
