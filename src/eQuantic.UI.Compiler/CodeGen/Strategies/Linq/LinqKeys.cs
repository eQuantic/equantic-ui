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

    /// <summary>
    /// Whether a plain object, the shape a dictionary of such keys is on this side, holds a key of
    /// this type faithfully: its text names one value, and two equal keys write the same text. A
    /// string, a char, a bool, a number, a Guid (text on this side), a DateOnly and a TimeSpan (whose
    /// text keeps every tick) do, and so does an enum whose members each have a value of their own.
    /// A DateTime's text drops its ticks and a TimeOnly's its seconds, a decimal writes 1.0 and 1.00
    /// apart where .NET has one key, an enum with aliases has two names for one key, and every class
    /// instance is the same "[object Object]" where .NET keys each by identity.
    /// </summary>
    public static bool HeldAsText(ITypeSymbol key)
    {
        var type = key.UnwrapNullable() ?? key;
        if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType) return !HasAliases(enumType);
        return type.SpecialType is SpecialType.System_String or SpecialType.System_Char
                or SpecialType.System_Boolean or SpecialType.System_SByte or SpecialType.System_Byte
                or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32
                or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64
                or SpecialType.System_Single or SpecialType.System_Double
            || type.IsNamed("System.Guid") || type.IsNamed("System.DateOnly") || type.IsNamed("System.TimeSpan");
    }

    /// <summary>Two members of the enum with one value, which .NET reads as one key.</summary>
    private static bool HasAliases(INamedTypeSymbol enumType) =>
        enumType.GetMembers().OfType<IFieldSymbol>()
            .Where(field => field.HasConstantValue)
            .GroupBy(field => field.ConstantValue)
            .Any(values => values.Count() > 1);
}
