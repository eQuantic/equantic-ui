using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// How LINQ's keyed operators compare a KEY and hold one, decided by the key's type, which the
/// compiler knows and a value alone cannot say.
/// </summary>
internal static class LinqKeys
{
    /// <summary>
    /// Whether two keys of this type are equal by VALUE where <c>===</c> compares references: a
    /// record, a struct, a tuple, an anonymous type, a decimal or one of the dates, each an object on
    /// this side, which <c>$eq.equals</c> compares as .NET's default equality does. A string, a
    /// number or an enum's name is a value to <c>===</c> already, and a class keys by identity in
    /// .NET as <c>===</c> does.
    /// </summary>
    public static bool ComparesByValue(ITypeSymbol? key)
    {
        var type = key.UnwrapNullable() ?? key;
        return type is not null
            && (type.IsStructuralValueType()
                || type.IsAnonymousType
                || type.SpecialType is SpecialType.System_Decimal or SpecialType.System_DateTime
                || type.IsNamed("System.TimeSpan") || type.IsNamed("System.DateOnly")
                || type.IsNamed("System.TimeOnly") || type.IsNamed("System.DateTimeOffset"));
    }

    /// <summary>
    /// The JavaScript that asks whether a group's key is the key sought, by the key type's equality.
    /// </summary>
    public static string Matches(ITypeSymbol? key, string groupKey, string sought) =>
        ComparesByValue(key) ? $"{Eq.Equals}({groupKey}, {sought})" : $"{groupKey} === {sought}";

    /// <summary>Two members of the enum with one value, which .NET reads as one key and this side,
    /// where an enum is its member's name, as two.</summary>
    public static bool HasAliases(INamedTypeSymbol enumType) =>
        enumType.GetMembers().OfType<IFieldSymbol>()
            .Where(field => field.HasConstantValue)
            .GroupBy(field => field.ConstantValue)
            .Any(values => values.Count() > 1);
}
