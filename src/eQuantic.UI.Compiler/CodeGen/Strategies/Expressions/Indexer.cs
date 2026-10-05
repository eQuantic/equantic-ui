using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Extensions;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// An INSTANCE INDEXER a twin carries (#427): <c>this[…]</c>'s getter is the method <c>item(…)</c>
/// and its setter <c>setItem(…, value)</c>, the extension indexer's <c>item</c> being the precedent,
/// and every element access the bound tree binds to it calls them, through the <see cref="Place"/>
/// every writer takes. JavaScript has no indexer, and <c>grid[3]</c> read a property named "3" that
/// no twin had: undefined, with a green build. A type declares one indexer and no member named
/// <c>Item</c> or <c>SetItem</c> beside it (EQ1007), so the two names are the indexer's.
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

    /// <summary>The indexer this lowering carries that the bound tree binds an element access to, or
    /// null: an access written with its receiver, a null-conditional's binding or an object
    /// initializer's entry, and a from-the-end key over a type that counts its elements, which C#
    /// binds to its <c>this[int]</c> (<c>ring[^1]</c> is <c>ring[ring.Count - 1]</c>).</summary>
    public static IPropertySymbol? LoweredAt(SyntaxNode access, ConversionContext context) =>
        context.SemanticHelper.GetOperation(access) switch
        {
            IPropertyReferenceOperation { Property: var property } when IsLowered(property) => property,
            IImplicitIndexerReferenceOperation { IndexerSymbol: IPropertySymbol property } when IsLowered(property) => property,
            _ => null,
        };
}
