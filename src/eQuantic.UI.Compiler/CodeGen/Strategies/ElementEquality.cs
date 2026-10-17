using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// How .NET's <c>EqualityComparer&lt;T&gt;.Default</c> finds two values of a type equal, written as the
/// runtime's <c>KeyEquality</c> (<c>utils/key-equality.ts</c>): null for IDENTITY (SameValueZero: a
/// number with NaN equal to NaN, a string, a char, a bool, a long, an enum, a Guid, an array, a
/// delegate), <c>true</c> for VALUE (<c>$eq.equals</c>: a record, a struct, a decimal, a date, and a
/// tuple, an anonymous type or a pair whose members all compare so), <c>'own'</c> where the type does not
/// decide (<c>object</c>, an interface a tuple implements, a type parameter), <c>'item'</c> for a class and an
/// interface no tuple implements, whose value is found by reference or by its own <c>Equals</c> and never by its members (a
/// <c>List&lt;int&gt;</c> is an array on the web, and two lists of the same items were one key), the typed
/// <c>Equals</c> of the app's own for a type that answers <c>IEquatable&lt;T&gt;</c> over itself
/// (<c>$eq.collections.equatable</c>), and a comparison GENERATED from the member
/// types for a tuple, an anonymous type or a pair with a member <c>$eq.equals</c> would compare otherwise.
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

    /// <summary>
    /// JavaScript asking whether two values of <paramref name="type"/> are equal as
    /// <c>EqualityComparer&lt;T&gt;.Default.Equals</c> answers it: <c>$eq.equals</c> where that answers
    /// what .NET answers, an array and a class by reference or the class's own <c>Equals</c>, a type
    /// that does not decide by what the value turns out to be, and a tuple holding an array by the
    /// comparison generated from its element types. A record's member and a tuple's <c>Equals</c> are
    /// compared through it: <c>$eq.equals</c> walks an array element by element, so two records holding
    /// two arrays of the same items were equal where .NET compares the arrays by reference (#554).
    /// A type the model could not give keeps <c>$eq.equals</c>, what every member was compared by.
    /// <paramref name="hoist"/> names a generated comparison once, where a caller can hold it, so it is
    /// not rebuilt on every call.
    /// </summary>
    internal static string Compare(ITypeSymbol? type, string left, string right, Func<string, string>? hoist = null) =>
        $"{Comparer(type, hoist)}({left}, {right})";

    /// <summary>
    /// The function <see cref="Compare"/> calls for a value of <paramref name="type"/>, named on its own
    /// where a caller hands the comparison over rather than calling it (<c>$eq.equalsInstance</c>'s).
    /// <paramref name="equatable"/> false leaves <c>IEquatable&lt;T&gt;</c> out, for what a value's own
    /// <c>Equals(object)</c> answers, which never asks it: <c>object.Equals(a, b)</c> and the call through
    /// <c>object</c>.
    /// </summary>
    internal static string Comparer(ITypeSymbol? type, Func<string, string>? hoist = null, bool equatable = true)
    {
        if (type is null) return Eq.Equals;
        var (equality, valueSafe) = Describe(type, equatable);
        if (valueSafe) return Eq.Equals;
        return equality switch
        {
            // An array, and a class: its own Equals where a twin carries one, and identity otherwise,
            // never its members, which `sameKey` walks for a value that may be a tuple.
            null or "'item'" => Eq.SameItem,
            "'own'" => Eq.SameKey,
            _ => hoist?.Invoke(equality) ?? equality,
        };
    }

    /// <summary>
    /// The key of an interface's typed <c>Equals</c> contract a call or a twin names, or null for any other
    /// member: <c>IEquatable&lt;T&gt;.Equals</c> and an <c>Equals</c> an interface of the app's own declares
    /// (<see cref="TwinMethodName.TypedEquals"/>), as the interface's definition and the parameter's type,
    /// <c>System.IEquatable&lt;T&gt;|int</c>, and <c>*</c> for a type parameter, which the value's own class
    /// settles (generic code). The twin's <c>static $equatable</c> maps each key to the method answering it,
    /// which holds a name of its own there, so a call reaches the implementation of the contract it binds:
    /// <c>IEquatable&lt;int&gt; c; c.Equals(1)</c> asked nothing a <c>C : IEquatable&lt;int&gt;</c> had named, and
    /// answered by what <c>c</c> is (Copilot's third review of #947).
    /// </summary>
    internal static string? ContractKey(IMethodSymbol? contract)
    {
        if (contract is not { Name: "Equals", IsStatic: false, Parameters.Length: 1, ContainingType.TypeKind: TypeKind.Interface }
            || !(IsEquatableEquals(contract) || TwinMethodName.TypedEquals(contract.OriginalDefinition)))
            return null;
        return $"{contract.ContainingType.OriginalDefinition.ToDisplayString()}|{KeyOf(contract.Parameters[0].Type)}";
    }

    /// <summary>A contract's parameter as its key spells it: <c>*</c> for a type parameter, the type otherwise.</summary>
    private static string KeyOf(ITypeSymbol type) =>
        type is ITypeParameterSymbol ? "*" : type.OriginalDefinition.ToDisplayString();

    /// <summary>
    /// What a class's or a struct's twin names in <c>static $equatable</c>: each interface typed <c>Equals</c>
    /// contract it answers with a method of the app's own (<see cref="ContractKey"/>), to the name that method
    /// holds on the twin, its own <c>IEquatable&lt;T&gt;</c> under <c>|@</c> too, which generic code asks. Empty
    /// for a type that answers none.
    /// </summary>
    internal static IReadOnlyList<(string Contract, string Method)> Contracts(INamedTypeSymbol type)
    {
        var answered = new List<(string Contract, string Method)>();
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct)) return answered;
        foreach (var face in type.AllInterfaces)
        {
            foreach (var contract in face.GetMembers("Equals").OfType<IMethodSymbol>())
            {
                if (ContractKey(contract) is not { } key
                    || type.FindImplementationForInterfaceMember(contract) is not IMethodSymbol implementation
                    || !TwinMethodName.TypedEquals(implementation))
                    continue;
                var name = TwinMethodName.Of(implementation);
                answered.Add((key, name));
                if (IsEquatableEquals(contract) && SymbolEqualityComparer.Default.Equals(face.TypeArguments[0], type))
                    answered.Add(($"{face.OriginalDefinition.ToDisplayString()}|@", name));
            }
        }
        return answered;
    }

    /// <summary>The object literal a twin's <c>static $equatable</c> holds (<see cref="Contracts"/>), or null for a
    /// type that answers no interface's typed <c>Equals</c>.</summary>
    internal static string? ContractMap(INamedTypeSymbol type)
    {
        var answered = Contracts(type);
        return answered.Count == 0
            ? null
            : "{ " + string.Join(", ", answered.Select(entry =>
                $"{JsStringLiteral.Quote(entry.Contract)}: {JsStringLiteral.Quote(entry.Method)}")) + " }";
    }

    /// <summary>The typed <c>Equals</c> of the app's own that answers the type's <c>IEquatable&lt;T&gt;</c> over
    /// itself, or null: what a record's synthesized <c>Equals(object)</c> asks.</summary>
    internal static IMethodSymbol? SelfMethod(INamedTypeSymbol type)
    {
        var face = type.AllInterfaces.FirstOrDefault(implemented =>
            implemented.OriginalDefinition.ToDisplayString() == "System.IEquatable<T>"
            && SymbolEqualityComparer.Default.Equals(implemented.TypeArguments[0], type));
        return face?.GetMembers("Equals").OfType<IMethodSymbol>().FirstOrDefault() is { } contract
            && type.FindImplementationForInterfaceMember(contract) is IMethodSymbol implementation
            && TwinMethodName.TypedEquals(implementation)
            ? implementation
            : null;
    }

    /// <summary>
    /// The contract of <c>IEquatable&lt;T&gt;</c> over the type itself, where a method of the app's own answers it:
    /// what <c>EqualityComparer&lt;T&gt;.Default</c> asks before <c>Equals(object)</c>, or null.
    /// </summary>
    internal static string? SelfContract(ITypeSymbol? type) =>
        type is INamedTypeSymbol named
        && Contracts(named).FirstOrDefault(entry => entry.Contract.EndsWith("|@", StringComparison.Ordinal)) is { Method: not null } self
            ? self.Contract[..^1] + KeyOf(named)
            : null;

    /// <summary>
    /// The JavaScript of the function a tuple's or an anonymous type's own <c>Equals(object)</c> compares
    /// by, which its call and its method group both use, or null where <c>$eq.equals</c> answers it: a
    /// tuple's whose elements all compare by value. An anonymous type's is always the comparison generated
    /// from its members, which takes only an anonymous object of the same members, in the same order, for
    /// one of its type, as .NET checks the type before it reads a member: <c>$eq.equals</c> matches two
    /// plain objects by their keys in any order, and a record by its members (#743).
    /// </summary>
    internal static string? OwnEquals(INamedTypeSymbol type)
    {
        if (type.IsAnonymousType)
            return MemberEquality(type, MembersOf(type).Select(member => Describe(member.Type).Equality ?? "false").ToList());
        var (equality, valueSafe) = Describe(type);
        return valueSafe ? null : equality;
    }

    /// <summary>
    /// Whether <paramref name="method"/> is <c>System.IEquatable&lt;T&gt;.Equals(T)</c>, the member a call through
    /// the interface or through a type parameter it constrains binds: generic code's equality, which
    /// <c>EqualityComparer&lt;T&gt;.Default</c> asks too.
    /// </summary>
    internal static bool IsEquatableEquals(IMethodSymbol? method) =>
        method is { Name: "Equals", IsStatic: false, Parameters.Length: 1 }
        && method.ContainingType is { TypeKind: TypeKind.Interface, Arity: 1 } face
        && face.OriginalDefinition.ToDisplayString() == "System.IEquatable<T>";

    /// <summary>An anonymous type's members, in the order C# declares them and its object literal is written.</summary>
    private static IReadOnlyList<IPropertySymbol> MembersOf(INamedTypeSymbol anonymous) =>
        anonymous.GetMembers().OfType<IPropertySymbol>().ToList();

    /// <summary>An anonymous type's comparison, each member under the name it crosses as, by the equality
    /// <paramref name="parts"/> gives it, in the members' order.</summary>
    private static string MemberEquality(INamedTypeSymbol anonymous, IReadOnlyList<string> parts) =>
        $"{Eq.MemberEquality}({{ {string.Join(", ", MembersOf(anonymous).Zip(parts, (member, part) => $"{member.Name.ToCamelCase()}: {part}"))} }})";

    /// <summary>Whether a class, or a base of it below <c>object</c>, overrides <c>Equals(object)</c>,
    /// which <c>EqualityComparer&lt;T&gt;.Default</c> then asks.</summary>
    private static bool OverridesEquals(INamedTypeSymbol type)
    {
        for (var at = type; at is not null && at.SpecialType != SpecialType.System_Object; at = at.BaseType)
        {
            if (at.GetMembers("Equals").OfType<IMethodSymbol>()
                .Any(method => method.IsOverride && method.Parameters is [{ Type.SpecialType: SpecialType.System_Object }]))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Whether a value of this interface may be a value tuple, which is an array on the web and is
    /// compared by its elements: one of the interfaces <c>ValueTuple</c> implements, read from
    /// <c>ValueTuple</c> in the interface's own assembly. A collection's interface, or an app's, is
    /// none of them.
    /// </summary>
    private static bool MayHoldATuple(INamedTypeSymbol face) =>
        face.ContainingAssembly?.GetTypeByMetadataName("System.ValueTuple`2") is { } tuple
        && tuple.AllInterfaces.Any(implemented =>
            SymbolEqualityComparer.Default.Equals(implemented.OriginalDefinition, face.OriginalDefinition));

    /// <summary>The equality, and whether <c>$eq.equals</c> answers what it answers for a value of the
    /// type: what lets a tuple of such members be compared by <c>$eq.equals</c> whole. <paramref name="equatable"/>
    /// false leaves <c>IEquatable&lt;T&gt;</c> out (<see cref="Comparer"/>).</summary>
    private static (string? Equality, bool ValueSafe) Describe(ITypeSymbol? type, bool equatable = true)
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
                return Composite(MembersOf(anonymous).Select(member => member.Type), parts => MemberEquality(anonymous, parts));
            case INamedTypeSymbol { Name: "KeyValuePair", TypeArguments.Length: 2 } pair
                when pair.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic":
                return Composite(pair.TypeArguments, parts => $"{Eq.PairComparer}({string.Join(", ", parts)})");
        }

        // IEquatable<T> of the app's own, which EqualityComparer<T>.Default asks before Equals(object): a typed
        // Equals is no `equals` of the twin, so a set of the type found nothing by it (#947's second review).
        if (equatable && SelfContract(value) is { } self) return ($"{Eq.Equatable}({JsStringLiteral.Quote(self)})", false);
        if (LinqKeys.ComparesByValue(value)) return ("true", true);
        return value switch
        {
            { SpecialType: SpecialType.System_Object or SpecialType.System_ValueType or SpecialType.System_Enum } => ("'own'", false),
            // An interface no tuple implements holds a value found by reference or its own Equals: a
            // list behind IReadOnlyList<int> compared by its elements found another list equal (#554).
            INamedTypeSymbol { TypeKind: TypeKind.Interface } face when !MayHoldATuple(face) => ("'item'", false),
            { TypeKind: TypeKind.Interface or TypeKind.TypeParameter or TypeKind.Dynamic } => ("'own'", false),
            // A class of the app's or a library's, never a special one: `string` overrides Equals too,
            // and is a primitive on this side, which SameValueZero compares by value already. One that
            // overrides Equals is asked it, its twin's or a reference tuple's members; one that does not
            // is found by reference: a List<int> is an array here, which the members' comparison found
            // equal to another list of the same items, in a set or a Contains alike.
            INamedTypeSymbol { TypeKind: TypeKind.Class, SpecialType: SpecialType.None } named =>
                (OverridesEquals(named) ? "'own'" : "'item'", false),
            // Everything else by identity, a delegate included, which `$eq.equals` compares the same way.
            _ => (null, true),
        };
    }

    /// <summary>A tuple's, an anonymous type's or a pair's equality from its members': <c>$eq.equals</c>
    /// whole when every member compares as it compares them, and the generated comparison otherwise.</summary>
    private static (string? Equality, bool ValueSafe) Composite(IEnumerable<ITypeSymbol> members,
        Func<IReadOnlyList<string>, string> generated)
    {
        var parts = members.Select(member => Describe(member)).ToList();
        if (parts.All(part => part.ValueSafe)) return ("true", true);
        return (generated(parts.Select(part => part.Equality ?? "false").ToList()), false);
    }
}
