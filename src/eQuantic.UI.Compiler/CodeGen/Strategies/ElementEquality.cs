using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// How .NET's <c>EqualityComparer&lt;T&gt;.Default</c> finds two values of a type equal, written as the
/// runtime's <c>KeyEquality</c> (<c>utils/key-equality.ts</c>): null for IDENTITY (SameValueZero: a
/// number with NaN equal to NaN, a string, a char, a bool, a long, an enum, a Guid, an array, a
/// delegate), <c>true</c> for VALUE (<c>$eq.equals</c>: a record, a struct, a decimal, a date, and a
/// tuple, an anonymous type or a pair whose members all compare so), <c>'own'</c> where the type does not
/// decide (<c>object</c>, an interface, a type parameter, a class a subclass may override <c>Equals</c>
/// in), and a comparison GENERATED from the member types for a tuple, an anonymous type or a pair with a
/// member <c>$eq.equals</c> would compare otherwise.
/// <para>
/// ONE decision for every search the comparer decides in .NET: a dictionary's keys and its
/// <c>ContainsValue</c>, a set's elements, a list's and an array's <c>IndexOf</c>, <c>LastIndexOf</c>,
/// <c>Contains</c> and <c>Remove</c>, and LINQ's <c>Contains</c>. The list read its comparison from
/// <c>IsStructuralValueType</c> and the dictionary from the key type's table, and the two disagreed
/// about one value (#425).
/// </para>
/// <para>
/// A tuple is the reason for the generated form: its <c>Equals</c> compares each element with that
/// element type's own comparer, so an array element by REFERENCE, and <c>$eq.equals</c> walks an array
/// element by element: <c>list.Contains((new[] { 1 }, 1))</c> answered true over a list holding another
/// array of 1. A tuple is an array here, so only its static type can say which of its members are
/// arrays.
/// </para>
/// </summary>
internal static class ElementEquality
{
    /// <summary>The runtime's <c>KeyEquality</c> for a value of <paramref name="type"/>, as the
    /// JavaScript that writes it; null for identity, which every runtime helper takes by default.</summary>
    internal static string? Of(ITypeSymbol? type) => Describe(type).Equality;

    /// <summary>The equality, and whether <c>$eq.equals</c> answers what it answers for a value of the
    /// type: what lets a tuple of such members be compared by <c>$eq.equals</c> whole.</summary>
    private static (string? Equality, bool ValueSafe) Describe(ITypeSymbol? type)
    {
        var value = type.UnwrapNullable() ?? type;
        switch (value)
        {
            case null:
                return (null, false);
            // An array is found by reference, which `$eq.equals` would walk element by element.
            case IArrayTypeSymbol:
                return (null, false);
            case INamedTypeSymbol { IsTupleType: true } tuple:
                return Composite(tuple.TupleElements.Select(element => element.Type),
                    parts => $"{Eq.TupleEquality}({string.Join(", ", parts)})");
            case INamedTypeSymbol { IsAnonymousType: true } anonymous:
            {
                var members = anonymous.GetMembers().OfType<IPropertySymbol>().ToList();
                return Composite(members.Select(member => member.Type),
                    parts => $"{Eq.MemberEquality}({{ {string.Join(", ", members.Zip(parts, (member, part) => $"{member.Name.ToCamelCase()}: {part}"))} }})");
            }
            case INamedTypeSymbol { Name: "KeyValuePair", TypeArguments.Length: 2 } pair
                when pair.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic":
                return Composite(pair.TypeArguments, parts => $"{Eq.PairComparer}({string.Join(", ", parts)})");
        }

        if (LinqKeys.ComparesByValue(value)) return ("true", true);
        return value switch
        {
            { SpecialType: SpecialType.System_Object or SpecialType.System_ValueType or SpecialType.System_Enum } => ("'own'", false),
            { TypeKind: TypeKind.Interface or TypeKind.TypeParameter or TypeKind.Dynamic } => ("'own'", false),
            // A class of the app's or a library's, never a special one: `string` overrides Equals too,
            // and is a primitive on this side, which SameValueZero compares by value already. One with no
            // Equals of its own is found by identity, which `$eq.equals` would walk member by member.
            { TypeKind: TypeKind.Class, SpecialType: SpecialType.None } => ("'own'", false),
            // Everything else by identity, a delegate included, which `$eq.equals` compares the same way.
            _ => (null, true),
        };
    }

    /// <summary>A tuple's, an anonymous type's or a pair's equality from its members': <c>$eq.equals</c>
    /// whole when every member compares as it compares them, and the generated comparison otherwise.</summary>
    private static (string? Equality, bool ValueSafe) Composite(IEnumerable<ITypeSymbol> members,
        Func<IReadOnlyList<string>, string> generated)
    {
        var parts = members.Select(Describe).ToList();
        if (parts.All(part => part.ValueSafe)) return ("true", true);
        return (generated(parts.Select(part => part.Equality ?? "false").ToList()), false);
    }
}
