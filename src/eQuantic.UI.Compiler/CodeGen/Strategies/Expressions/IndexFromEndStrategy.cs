using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for Index from end expressions (hat operator) over an array, a list or a string, which are
/// a JavaScript array or string on this side: <c>a[^n]</c> is <c>a[a.length - n]</c>, read and written
/// as the <see cref="Place"/> it is, so its receiver is evaluated once and its offset is a part the
/// writer parenthesizes (#394). It was text that named the receiver twice, so <c>Get()[^1]</c> called
/// <c>Get</c> twice, and spliced the offset bare, so <c>a[^(i + 1)]</c> read <c>a[a.length - i + 1]</c>.
/// <para>
/// An indexer a twin carries is not here: <c>ring[^1]</c> over a type that counts its elements is its
/// <c>item</c> at the count the bound tree names (<see cref="Place"/>), and a type that declares
/// <c>this[Index]</c> takes the index itself. This read <c>ring.length</c>, which no twin has, and
/// wrote the bare index into <c>setItem</c>. Nor is a list face's (<c>IReadOnlyList&lt;T&gt;</c>,
/// <c>IList&lt;T&gt;</c>), which counts and reads whichever list it holds through the runtime (#586).
/// </para>
/// <para>
/// A <c>^n</c> that is no array's, list's or string's index is a System.Index VALUE (<c>Index i =
/// ^1</c>, a <c>this[Index]</c> key), which has no JavaScript translation: it is refused (EQ1004), where
/// it was written as a call to <c>__INDEX_FROM_END__</c>, which nothing defines.
/// </para>
/// </summary>
public class IndexFromEndStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context) =>
        node is PrefixUnaryExpressionSyntax prefix && prefix.IsKind(SyntaxKind.IndexExpression)
        || node is ElementAccessExpressionSyntax access && HatOf(access, context) is not null;

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        if (node is ElementAccessExpressionSyntax access && Place.Of(access, context) is { } place)
            return place.Read();

        // A standalone ^n is an Index value.
        return JsExpr.Opaque(context.Unhandled(node, "index-from-end"));
    }

    /// <summary>
    /// The <c>^n</c> of an element access this strategy lowers: an access with one argument counted from
    /// the end, whose receiver is no dictionary keyed by <c>Index</c> (<c>d[^1]</c> looks the key up,
    /// <see cref="DictionaryEntry"/>) and carries no indexer a twin or a list face lowers; null otherwise.
    /// </summary>
    internal static PrefixUnaryExpressionSyntax? HatOf(ElementAccessExpressionSyntax access, ConversionContext context) =>
        access.ArgumentList.Arguments is [{ Expression: PrefixUnaryExpressionSyntax hat }]
        && hat.IsKind(SyntaxKind.IndexExpression)
        && DictionaryEntry.Of(access, context) is null
        && Indexer.LoweredAt(access, context) is null
            ? hat
            : null;

    public int Priority => 20; // Higher than ElementAccessStrategy
}
