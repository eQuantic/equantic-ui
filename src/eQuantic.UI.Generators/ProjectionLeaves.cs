using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Generators;

/// <summary>
/// Where a projection stops following a value: at a LEAF, plain data that crosses as it is. A number,
/// a boolean, an enum, a date or a time, a string, a URI, a struct made of leaves, and a collection of
/// leaves. Anything else is an object whose members the browser may read one at a time, and only those
/// cross.
/// </summary>
internal static class ProjectionLeaves
{
    public static bool IsLeaf(ITypeSymbol type) =>
        IsLeaf(type, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));

    private static bool IsLeaf(ITypeSymbol type, HashSet<ITypeSymbol> visiting)
    {
        if (type is ITypeParameterSymbol) return false;
        if (type.SpecialType == SpecialType.System_String || type.TypeKind == TypeKind.Enum) return true;
        if (type.ToDisplayString() == "System.Uri") return true;
        if (type is IArrayTypeSymbol array) return IsLeaf(array.ElementType, visiting);
        if (type is not INamedTypeSymbol named) return false;
        // A cycle answers yes, so the members around it decide.
        if (!visiting.Add(named)) return true;

        var definition = named.OriginalDefinition.ToDisplayString();
        if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            return IsLeaf(named.TypeArguments[0], visiting);
        // Its two fields are private, so the field rule below would not see what it holds.
        if (definition == "System.Collections.Generic.KeyValuePair<TKey, TValue>")
            return IsLeaf(named.TypeArguments[0], visiting) && IsLeaf(named.TypeArguments[1], visiting);

        if (Element(named) is { } element) return IsLeaf(element, visiting);
        // A struct is copied by value, so it is data when what it holds is: its fields, every one in
        // source and the public ones of a struct from metadata, which keeps its own private.
        return named.IsValueType
            && named.GetMembers().OfType<IFieldSymbol>().Where(f => !f.IsStatic).All(f => IsLeaf(f.Type, visiting));
    }

    /// <summary>The element a collection enumerates, when it enumerates exactly one kind.</summary>
    private static ITypeSymbol? Element(INamedTypeSymbol type)
    {
        var enumerables = type.AllInterfaces
            .Append(type)
            .Where(i => i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
            .Select(i => i.TypeArguments[0])
            .Distinct<ITypeSymbol>(SymbolEqualityComparer.Default)
            .ToList();
        return enumerables.Count == 1 ? enumerables[0] : null;
    }
}
