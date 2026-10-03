using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Generators;

/// <summary>
/// Where a projection stops following a value: at a LEAF, which crosses as it is. A scalar (a number, a
/// boolean, a char, an enum, a date or a time, a GUID, a string, a URI), and the two collection shapes
/// the boundary rebuilds whole (<see cref="BoundaryShape"/>): a sequence of leaves, which the browser
/// holds as an array, and a dictionary of them, which it holds as the runtime's class. Everything else,
/// a struct included, is read member by member, and only those reads cross, each coerced in the browser
/// by its own type.
/// <para>
/// A struct is never written whole. What the server's serializer writes of one is not what it holds:
/// it drops public fields, writes a <c>byte[]</c> as base64, and writes a computed property the
/// browser's spec does not type, so a long crossed as a string. Nor is a set, a pair or any other
/// collection: written whole it is an array or an object, never the Set or the pair the browser's code
/// reads. Reads down to scalars have none of those gaps, and a use that needs the value itself fails
/// the build where the analysis stops.
/// </para>
/// </summary>
internal static class ProjectionLeaves
{
    /// <summary>The value types that cross as a value.</summary>
    private static readonly HashSet<string> Scalars = new()
    {
        "System.TimeSpan", "System.DateTimeOffset", "System.DateOnly", "System.TimeOnly", "System.Guid",
    };

    public static bool IsLeaf(ITypeSymbol type)
    {
        if (type is ITypeParameterSymbol) return false;
        if (type.SpecialType == SpecialType.System_String || type.TypeKind == TypeKind.Enum) return true;
        // A boolean, a char, a number, a decimal and a DateTime. Named, not ranged: the enumeration puts
        // System.Array and the collection interfaces between the numbers and DateTime.
        if (type.SpecialType is (>= SpecialType.System_Boolean and <= SpecialType.System_Double)
            or SpecialType.System_IntPtr or SpecialType.System_UIntPtr or SpecialType.System_DateTime) return true;
        var name = type.ToDisplayString();
        if (name == "System.Uri" || Scalars.Contains(name)) return true;
        // A byte[] is written as base64 text, which no spec turns back into bytes.
        if (type is IArrayTypeSymbol array)
            return array.ElementType.SpecialType != SpecialType.System_Byte && IsLeaf(array.ElementType);
        if (type is not INamedTypeSymbol named) return false;

        if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            return IsLeaf(named.TypeArguments[0]);
        if (BoundaryShape.IsSequence(named)) return IsLeaf(named.TypeArguments[0]);
        return BoundaryShape.DictionaryName(named) is not null
            && IsLeaf(named.TypeArguments[0]) && IsLeaf(named.TypeArguments[1]);
    }
}
