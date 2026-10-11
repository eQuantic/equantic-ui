using System.Linq;
using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// A <c>Count</c> read, as the browser's form of its receiver answers it: a dictionary's and a set's
/// <c>size</c>, the runtime queue's, stack's, linked list's and sorted set's <c>count</c>, the helper
/// that reads an array, a Set or a twin for a face more than one of them answers to (<c>ICollection</c>,
/// <c>IReadOnlyList</c>), the own <c>count</c> of a type the app or a library it references declares,
/// and an array's <c>length</c> for .NET's own types, which the browser holds as one (a list, a
/// lookup), and for a receiver the model cannot type.
/// <para>
/// ONE table, for a member access and a property pattern alike. Each kept its own, and a pattern
/// counted a set by <c>length</c> while the member read beside it said <c>size</c>, so
/// <c>{ Roles.Count: > 0 }</c> was false in the browser alone (#516).
/// </para>
/// </summary>
internal static class CountSpelling
{
    public static JsExpr Read(JsExpr receiver, ITypeSymbol? type, ConversionContext context)
    {
        if (type.IsDictionary()) return JsExpr.Member(receiver, "size");
        if (BoundaryShape.CollectionClass(type) is { } kind) return JsExpr.Member(receiver, kind == "set" ? "size" : "count");

        // A receiver typed only as a collection may be a Set at run time, whose count is `size`.
        // `.length` on one is undefined, and `undefined > 0` is false, so the header checkbox
        // simply never noticed a selection. One typed as a list's face may be a twin of the app's own,
        // whose count is its `count` (#586): an IReadOnlyList<T> over one counted undefined.
        if (type.HasOpenCollectionShape() || type.IsListFace())
        {
            context.UsedHelpers.Add(Eq.Import);
            return JsExpr.Call(JsExpr.Identifier(Eq.Count), receiver);
        }

        // A type of the app's or of a library it references has a `Count` of its own: its property
        // emits as `get count()`, and EqJson writes it as `count`. Asked of the source alone, a
        // library's domain model read its `Count` as `.length`, undefined (#517).
        if (type is not null and not IArrayTypeSymbol && !BoundaryShape.IsPlatform(type))
            return JsExpr.Member(receiver, "count");

        return JsExpr.Member(receiver, "length");
    }
}
