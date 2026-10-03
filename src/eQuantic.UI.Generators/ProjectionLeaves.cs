using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Generators;

/// <summary>
/// Where a projection stops following a value: at a LEAF, a scalar that crosses as it is. A number, a
/// boolean, a char, an enum, a date or a time, a GUID, a string, a URI, and a collection or a dictionary
/// of those. Everything else, a struct included, is read member by member, and only those reads cross,
/// each coerced in the browser by its own type.
/// <para>
/// A struct is never written whole. What the server's serializer writes of one is not what it holds:
/// it drops public fields, writes a <c>byte[]</c> as base64, and writes a computed property the
/// browser's spec does not type, so a long crossed as a string. Reads down to scalars have none of
/// those gaps, and a use that needs the struct itself fails the build where the analysis stops.
/// </para>
/// </summary>
internal static class ProjectionLeaves
{
    /// <summary>The value types that cross as a value.</summary>
    private static readonly HashSet<string> Scalars = new()
    {
        "System.TimeSpan", "System.DateTimeOffset", "System.DateOnly", "System.TimeOnly", "System.Guid",
    };

    /// <summary>The byte containers the serializer writes as base64 text, which no spec turns back into bytes.</summary>
    private static readonly HashSet<string> Base64 = new()
    {
        "System.Memory<byte>", "System.ReadOnlyMemory<byte>",
    };

    public static bool IsLeaf(ITypeSymbol type) =>
        IsLeaf(type, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));

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
        if (Base64.Contains(name)) return false;
        if (type is IArrayTypeSymbol array)
            return array.ElementType.SpecialType != SpecialType.System_Byte && IsLeaf(array.ElementType, visiting);
        if (type is not INamedTypeSymbol named) return false;
        // A cycle answers no: a collection of itself is no scalar.
        if (!visiting.Add(named)) return false;

        if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            return IsLeaf(named.TypeArguments[0], visiting);
        // A dictionary's entry: a key and a value, each a leaf.
        if (named.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.KeyValuePair<TKey, TValue>")
            return IsLeaf(named.TypeArguments[0], visiting) && IsLeaf(named.TypeArguments[1], visiting);
        return Element(named) is { } element && IsLeaf(element, visiting);
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
