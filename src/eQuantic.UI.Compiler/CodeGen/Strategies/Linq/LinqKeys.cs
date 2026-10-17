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
    /// The JavaScript that asks whether a group's key is the key sought, as the key type's default comparer
    /// answers it (<see cref="ElementEquality.Compare"/>), and <c>===</c> only where that answers the same:
    /// a type compared by identity that is no floating-point number, whose NaN .NET finds equal to itself,
    /// and a key the model could not type. By <c>===</c>, two equal records were two groups, each NaN was a
    /// group of its own and a key of a class that overrides <c>Equals</c> was found by reference. A tuple or
    /// an anonymous object holding an array compares that member by reference, as .NET's does, where
    /// <c>$eq.equals</c> walked the array and two keys of different arrays were one group.
    /// </summary>
    public static string Matches(ITypeSymbol? key, string groupKey, string sought, ConversionContext context)
    {
        if (key is null || ByIdentity(key)) return $"{groupKey} === {sought}";
        context.UsedHelpers.Add(Eq.Import);
        return ElementEquality.Compare(key, groupKey, sought);
    }

    /// <summary>Whether <c>===</c> answers what the type's default comparer answers: a type compared by
    /// identity (<see cref="ElementEquality.Of"/>) that is no floating-point number.</summary>
    private static bool ByIdentity(ITypeSymbol key) =>
        ElementEquality.Of(key) is null
        && (key.UnwrapNullable() ?? key).SpecialType is not (SpecialType.System_Single or SpecialType.System_Double);
}
