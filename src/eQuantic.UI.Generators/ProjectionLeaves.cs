using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Generators;

/// <summary>
/// Where a projection stops following a value: at a LEAF, plain data that crosses as it is. A number,
/// a boolean, an enum, a date or a time, a string, a URI, a struct made of leaves, and a collection of
/// leaves. Anything else is an object whose members the browser may read one at a time, and only those
/// cross.
/// <para>
/// A leaf is written whole, so it is judged by what writing it whole sends: the server's serializer
/// writes a struct's public properties, the computed ones included. A struct is a leaf only when every
/// one of those, and every field it shows, is a leaf too. Its fields alone would pass a struct with no
/// fields that hands out an object through a getter, and every struct a referenced assembly keeps its
/// fields private in.
/// </para>
/// </summary>
internal static class ProjectionLeaves
{
    public static bool IsLeaf(ITypeSymbol type) =>
        IsLeaf(type, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));

    /// <summary>The value types whose own members say nothing about what crosses: they cross as a value.</summary>
    private static readonly HashSet<string> Scalars = new()
    {
        "System.TimeSpan", "System.DateTimeOffset", "System.DateOnly", "System.TimeOnly", "System.Guid",
    };

    private static bool IsLeaf(ITypeSymbol type, HashSet<ITypeSymbol> visiting)
    {
        if (type is ITypeParameterSymbol) return false;
        if (type.SpecialType == SpecialType.System_String || type.TypeKind == TypeKind.Enum) return true;
        // A boolean, a char, a number, a decimal and a DateTime. Named, not ranged: the enumeration puts
        // System.Array and the collection interfaces between the numbers and DateTime.
        if (type.SpecialType is (>= SpecialType.System_Boolean and <= SpecialType.System_Double)
            or SpecialType.System_IntPtr or SpecialType.System_UIntPtr or SpecialType.System_DateTime) return true;
        var name = type.ToDisplayString();
        if (name == "System.Uri" || Scalars.Contains(name)) return true;
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
        // A struct is copied by value, so it is data when what writing it sends is: its public
        // properties, and every field it shows.
        return named.IsValueType
            && named.GetMembers().OfType<IPropertySymbol>()
                .Where(p => !p.IsStatic && !p.IsIndexer && p.GetMethod is not null
                    && p.DeclaredAccessibility == Accessibility.Public)
                .All(p => IsLeaf(p.Type, visiting))
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
