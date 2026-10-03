using System.Linq;
using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// A <c>Count</c> read, as the browser's form of its receiver answers it: a dictionary's and a set's
/// <c>size</c>, the runtime queue's, stack's, linked list's and sorted set's <c>count</c>, the helper
/// that reads either an array or a Set for a face both answer to (<c>ICollection</c>), a user type's
/// own <c>count</c>, and an array's <c>length</c> for every sequence and for a receiver the model
/// cannot type.
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
        // simply never noticed a selection.
        if (type.HasOpenCollectionShape())
        {
            context.UsedHelpers.Add(Eq.Import);
            return JsExpr.Call(JsExpr.Identifier(Eq.Count), receiver);
        }

        // A USER type with its own `Count` is not a collection: its property emits as `get count()`,
        // and rewriting the read to `.length` returned undefined.
        if (type is not null && type.Locations.Any(location => location.IsInSource))
            return JsExpr.Member(receiver, "count");

        return JsExpr.Member(receiver, "length");
    }
}
