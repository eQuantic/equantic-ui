using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Extensions;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// How the browser orders a value of a type as .NET's <c>Comparer&lt;T&gt;.Default</c> does: an
/// <c>Ordering</c> of <c>utils/ordering.ts</c>, written as the JavaScript that names it, or null where
/// the values have no faithful order on that side.
/// <para>
/// ONE table, read wherever the browser orders by a type: <c>Max</c> and <c>Min</c>, a sorted
/// collection eqc builds, and one a hydration rebuilds. A value alone cannot say how it orders: a C#
/// string and a C# char are both strings in the browser, and order in the current culture and by code
/// unit. Without it, every sorted collection ordered by <c>&lt;</c>, so a <c>SortedSet&lt;string&gt;</c>
/// put <c>B</c> before <c>a</c>, and one of decimals compared their text, keeping <c>1.0</c> beside
/// <c>1.00</c> and <c>10</c> before <c>9</c>.
/// </para>
/// </summary>
internal static class ValueOrdering
{
    /// <summary>The ordering of a value of <paramref name="type"/>, a <c>Nullable&lt;T&gt;</c> read
    /// as its <c>T</c>, as the JavaScript that names it (<c>'text'</c>, or an enum's members); null
    /// where it has no faithful order in the browser.</summary>
    internal static string? Of(ITypeSymbol type)
    {
        var value = type.UnwrapNullable() ?? type;
        if (value is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumeration) return OfEnum(enumeration);
        return value.SpecialType switch
        {
            SpecialType.System_Double or SpecialType.System_Single => "'real'",
            SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Int16
                or SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_UInt16
                or SpecialType.System_UInt32 or SpecialType.System_UInt64 or SpecialType.System_Char
                or SpecialType.System_Boolean => "'value'",
            SpecialType.System_String => "'text'",
            _ when CarriesCompareTo(value) => "'comparable'",
            _ => null,
        };
    }

    /// <summary>
    /// An enum orders by its value, as .NET's comparer orders it. A flags enum is that value already
    /// in the browser, so it orders as a number. Any other crosses as its member's camelCase name,
    /// which ordered alphabetically, so its members are written out with their values.
    /// </summary>
    private static string OfEnum(INamedTypeSymbol enumeration)
    {
        if (enumeration.IsFlagsEnum()) return "'value'";
        return Types.EnumShape.KeyToValue(enumeration);
    }

    /// <summary>
    /// Whether a value of this type orders by a <c>compareTo</c> it carries on this side: a decimal and
    /// the dates, whose runtime types have one, and a type of the app's own that is comparable through
    /// a <c>CompareTo</c> it wrote, which its twin carries by that name. Nothing else has one to call:
    /// an enum and a Guid are comparable in the BCL but cross as a name and as text, a type that is not
    /// comparable makes .NET's default comparer throw, a comparison written as an explicit interface
    /// member has no name to be called by, and a type parameter may be answered by a number.
    /// </summary>
    private static bool CarriesCompareTo(ITypeSymbol type)
    {
        if (type.SpecialType is SpecialType.System_Decimal or SpecialType.System_DateTime
            || type.IsNamed("System.TimeSpan") || type.IsNamed("System.DateOnly")
            || type.IsNamed("System.TimeOnly") || type.IsNamed("System.DateTimeOffset"))
            return true;
        return type.AllInterfaces
            .Where(contract => contract.ContainingNamespace?.ToDisplayString() == "System"
                && contract.OriginalDefinition.MetadataName is "IComparable" or "IComparable`1")
            .SelectMany(contract => contract.GetMembers("CompareTo"))
            .Select(type.FindImplementationForInterfaceMember)
            .Any(found => found is IMethodSymbol { MethodKind: MethodKind.Ordinary } method
                && method.Locations.Any(location => location.IsInSource));
    }
}
