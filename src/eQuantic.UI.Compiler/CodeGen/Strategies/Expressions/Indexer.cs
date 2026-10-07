using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// An INSTANCE INDEXER a twin carries (#427): <c>this[…]</c>'s getter is the method <c>item(…)</c>
/// and its setter <c>setItem(…, value)</c>, the extension indexer's <c>item</c> being the precedent,
/// and every element access the bound tree binds to it calls them, through the <see cref="Place"/>
/// every writer takes. JavaScript has no indexer, and <c>grid[3]</c> read a property named "3" that
/// no twin had: undefined, with a green build. A type declares one indexer under those names and no
/// member of its instances named <c>item</c> or <c>setItem</c> beside it (EQ1007), so the two names
/// are the indexer's.
/// <para>
/// An access through an interface reaches <c>item</c> and <c>setItem</c> whatever the type behind it,
/// so those are the slot of an interface's own indexer and of an explicit implementation of one, and
/// of the indexer of a type that implements none explicitly. A type's own indexer beside an explicit
/// implementation takes names of its own (<see cref="NamesOf(IPropertySymbol)"/>), which only an
/// access through that type reaches, as C# reaches it there.
/// </para>
/// </summary>
internal static class Indexer
{
    /// <summary>The getter's name on the twin.</summary>
    public const string Get = "item";

    /// <summary>The setter's name on the twin.</summary>
    public const string Set = "setItem";

    /// <summary>
    /// The twin's names for an indexer's getter and setter: <c>item</c> and <c>setItem</c>, unless the
    /// indexer is a type's own beside an explicit implementation of an interface's indexer, which holds
    /// that slot (<c>int IGrid.this[int i]</c>); then they are named after the type that first declares
    /// the indexer, its override's alike (<c>Grid$item</c>, a name no C# member can take). Both were
    /// <c>item</c>, so the twin kept one of them, and EQ1007 refused the type.
    /// </summary>
    public static (string Get, string Set) NamesOf(IPropertySymbol indexer)
    {
        if (indexer.ContainingType.TypeKind == TypeKind.Interface || !indexer.ExplicitInterfaceImplementations.IsEmpty)
            return (Get, Set);
        var root = indexer;
        while (root.OverriddenProperty is { } overridden) root = overridden;
        var declaring = root.ContainingType.OriginalDefinition;
        return declaring.GetMembers().OfType<IPropertySymbol>()
            .Any(member => member.IsIndexer && !member.ExplicitInterfaceImplementations.IsEmpty)
            ? Own(declaring.Name)
            : (Get, Set);
    }

    /// <summary>The names of an indexer as its declaration has them: from its symbol where a model can
    /// be asked, and where none can, from the declaration beside it (an override's are then taken as
    /// its own declaration's, which only a base that holds an explicit implementation tells apart).</summary>
    public static (string Get, string Set) NamesOf(IndexerDeclarationSyntax indexer, SemanticModel? model)
    {
        if (model is not null && model.SyntaxTree == indexer.SyntaxTree
            && model.GetDeclaredSymbol(indexer) is IPropertySymbol symbol)
            return NamesOf(symbol);
        if (indexer.ExplicitInterfaceSpecifier is not null || indexer.Parent is not TypeDeclarationSyntax type
            || type is InterfaceDeclarationSyntax)
            return (Get, Set);
        return type.Members.OfType<IndexerDeclarationSyntax>().Any(member => member.ExplicitInterfaceSpecifier is not null)
            ? Own(type.Identifier.ValueText)
            : (Get, Set);
    }

    private static (string Get, string Set) Own(string type) => ($"{type}${Get}", $"{type}${Set}");

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
            || Services.RuntimeProvidedTypeScanner.IsTranspiledNamespace(ns)
            || declaring.TwinIsTranspiled();
    }

    /// <summary>
    /// Whether an indexer is a list FACE's (<c>IList&lt;T&gt;</c>, <c>IReadOnlyList&lt;T&gt;</c>,
    /// <c>IList</c>), which the interface reaches on whatever implements it when the access runs: an
    /// array's subscript and a twin's <c>item</c> alike, through the runtime (#586). It was a subscript,
    /// which an array answers and a twin does not, so a twin read through the face was undefined.
    /// </summary>
    public static bool IsListFace(IPropertySymbol? indexer) =>
        indexer is { IsIndexer: true, IsStatic: false } && indexer.ContainingType.IsListFace();

    /// <summary>The indexer this lowering carries that the bound tree binds an element access to, or
    /// null: an access written with its receiver, a null-conditional's binding or an object
    /// initializer's entry, and a from-the-end key over a type that counts its elements, which C#
    /// binds to its <c>this[int]</c> (<c>ring[^1]</c> is <c>ring[ring.Count - 1]</c>). A twin's own
    /// indexer, and a list face's, which the <see cref="Place"/> reads through the runtime.</summary>
    public static IPropertySymbol? LoweredAt(SyntaxNode access, ConversionContext context) =>
        context.SemanticHelper.GetOperation(access) switch
        {
            IPropertyReferenceOperation { Property: var property } when IsLowered(property) || IsListFace(property) => property,
            IImplicitIndexerReferenceOperation { IndexerSymbol: IPropertySymbol property }
                when IsLowered(property) || IsListFace(property) => property,
            _ => null,
        };
}
