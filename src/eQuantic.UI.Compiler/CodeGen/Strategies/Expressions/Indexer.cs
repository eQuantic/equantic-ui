using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Extensions;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// An INSTANCE INDEXER a twin carries (#427): <c>this[…]</c>'s getter is the method <c>item(…)</c>
/// and its setter <c>setItem(…, value)</c>, the extension indexer's <c>item</c> being the precedent,
/// and every element access the bound tree binds to it calls them. JavaScript has no indexer, and
/// <c>grid[3]</c> read a property named "3" that no twin had: undefined, with a green build. A
/// type declares one indexer and no member named <c>Item</c> or <c>SetItem</c> beside it (EQ1007), so
/// the two names are the indexer's.
/// </summary>
internal static class Indexer
{
    /// <summary>The getter's name on the twin.</summary>
    public const string Get = "item";

    /// <summary>The setter's name on the twin.</summary>
    public const string Set = "setItem";

    /// <summary>
    /// Whether an indexer is one this lowering carries: an instance indexer of a type whose twin eqc
    /// writes (declared in the source, or in a namespace it transpiles whole), a class's, a record's, a
    /// struct's or an interface's. A collection's (a list, a dictionary, an array, a string) keeps the
    /// subscript its runtime form answers, and an extension indexer its static <c>item</c>.
    /// </summary>
    public static bool IsLowered(IPropertySymbol? indexer)
    {
        if (indexer is not { IsIndexer: true, IsStatic: false } || indexer.ExtensionBlockHome() is not null) return false;
        var declaring = indexer.ContainingType;
        var ns = declaring.ContainingNamespace?.ToDisplayString() ?? "";
        return declaring.Locations.Any(location => location.IsInSource)
            || Services.RuntimeProvidedTypeScanner.IsTranspiledNamespace(ns);
    }

    /// <summary>The read: <c>receiver.item(keys)</c>.</summary>
    public static string Read(string receiver, IEnumerable<string> keys) => $"{receiver}.{Get}({string.Join(", ", keys)})";

    /// <summary>The write: <c>receiver.setItem(keys, value)</c>, which answers the value written, as
    /// C#'s assignment does.</summary>
    public static string Write(string receiver, IEnumerable<string> keys, string value) =>
        $"{receiver}.{Set}({string.Join(", ", keys.Append(value))})";
}
