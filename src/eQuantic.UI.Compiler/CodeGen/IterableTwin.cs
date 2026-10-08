using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// The twin of a type that implements <c>IEnumerable&lt;T&gt;</c> is enumerated as the type's own
/// <c>GetEnumerator()</c> says (#612). A <c>foreach</c>, a spread, <c>string.Join</c> and every LINQ
/// operator reach a sequence in the browser through JavaScript's iteration, <c>[Symbol.iterator]</c>,
/// which nothing wrote, so each threw over a class of the app's. And the twin held the type's two
/// <c>GetEnumerator</c>s under one name: the explicit <c>IEnumerable.GetEnumerator()</c> was written
/// after the generic one, over it, and called itself.
/// <para>
/// So the twin of the type that declares the method its iteration goes through carries a
/// <c>[Symbol.iterator]</c> that calls it, by the name it holds (<see cref="TwinMethodName"/>), and walks
/// what it returns (<c>$eq.linq.iterate</c>): an enumerator an iterator method filled, or one the app
/// wrote, by its <c>MoveNext</c> and <c>Current</c>. The method is the one C#'s <c>foreach</c> binds: the
/// type's own public <c>GetEnumerator()</c>, which a struct enumerator rides on as <c>List&lt;T&gt;</c>'s
/// does, or else its implementation of <c>IEnumerable&lt;T&gt;.GetEnumerator()</c> (or of the non-generic
/// one, for a type that implements no <c>IEnumerable&lt;T&gt;</c>). A derived type inherits it, and its
/// override of the method answers.
/// </para>
/// <para>
/// An explicit implementation of a member of the enumeration interfaces (<c>GetEnumerator()</c>,
/// <c>Current</c>) beside the member that answers that name for the type is not written at all: the twin
/// holds one member per name, and the two answer alike, by the contract of the generic interfaces, which
/// derive from the non-generic ones, and by the pattern every .NET collection keeps for its own public
/// one. Written after it, the explicit one replaced it and called itself: the non-generic
/// <c>IEnumerable.GetEnumerator()</c> after the generic one, and <c>IEnumerable&lt;T&gt;.GetEnumerator()</c>
/// after a public <c>GetEnumerator()</c> that returns a struct, whose loop then ran out of stack.
/// </para>
/// </summary>
internal static class IterableTwin
{
    /// <summary>The name of the member JavaScript's iteration reads.</summary>
    private const string Iterator = "[Symbol.iterator]";

    /// <summary>
    /// The method <paramref name="type"/>'s iteration goes through, as C#'s <c>foreach</c> binds it
    /// (<see cref="Answering"/>); null for a type that implements no <c>IEnumerable</c>.
    /// </summary>
    public static IMethodSymbol? EnumeratorOf(INamedTypeSymbol type) =>
        type.AllInterfaces.Any(face => face.SpecialType == SpecialType.System_Collections_IEnumerable)
            ? Answering(type, "GetEnumerator", sequence: true) as IMethodSymbol
            : null;

    /// <summary>
    /// Whether the twin leaves <paramref name="member"/> out: an explicit implementation of a member of
    /// <c>IEnumerable</c>, <c>IEnumerable&lt;T&gt;</c>, <c>IEnumerator</c> or <c>IEnumerator&lt;T&gt;</c>
    /// (<c>GetEnumerator()</c>, <c>Current</c>) in a type where another member answers that name
    /// (<see cref="Answering"/>). The two hold one name on the twin and answer alike: written after the
    /// other, the explicit one replaced it and called itself.
    /// </summary>
    public static bool LeavesOut(ISymbol member)
    {
        var implemented = member switch
        {
            IMethodSymbol method => method.ExplicitInterfaceImplementations.Cast<ISymbol>(),
            IPropertySymbol property => property.ExplicitInterfaceImplementations.Cast<ISymbol>(),
            _ => [],
        };
        foreach (var contract in implemented)
        {
            bool? sequence = contract.ContainingType.OriginalDefinition.SpecialType switch
            {
                SpecialType.System_Collections_IEnumerable or SpecialType.System_Collections_Generic_IEnumerable_T => true,
                SpecialType.System_Collections_IEnumerator or SpecialType.System_Collections_Generic_IEnumerator_T => false,
                _ => null,
            };
            if (sequence is { } kind && Answering(member.ContainingType, contract.Name, kind) is { } answer
                && !SymbolEqualityComparer.Default.Equals(answer.OriginalDefinition, member.OriginalDefinition))
                return true;
        }
        return false;
    }

    /// <summary>
    /// The member that answers <paramref name="name"/> (<c>GetEnumerator</c>, <c>Current</c>) for
    /// <paramref name="type"/>'s enumeration, as C# binds it: the type's own public instance member of
    /// that name, which <c>foreach</c> binds first, or else what implements the generic interface's member
    /// for it (<c>IEnumerable&lt;T&gt;</c>, or <c>IEnumerator&lt;T&gt;</c> where <paramref name="sequence"/>
    /// is false), or else the non-generic one's.
    /// </summary>
    private static ISymbol? Answering(INamedTypeSymbol type, string name, bool sequence)
    {
        for (var at = type; at is not null; at = at.BaseType)
            foreach (var own in at.GetMembers(name))
                if (own is { IsStatic: false, DeclaredAccessibility: Accessibility.Public }
                    and (IMethodSymbol { MethodKind: MethodKind.Ordinary, Parameters.IsEmpty: true, Arity: 0 }
                        or IPropertySymbol { IsIndexer: false }))
                    return own;
        var faces = sequence
            ? (Generic: SpecialType.System_Collections_Generic_IEnumerable_T, Plain: SpecialType.System_Collections_IEnumerable)
            : (Generic: SpecialType.System_Collections_Generic_IEnumerator_T, Plain: SpecialType.System_Collections_IEnumerator);
        foreach (var special in new[] { faces.Generic, faces.Plain })
        {
            var face = type.AllInterfaces.FirstOrDefault(candidate => candidate.OriginalDefinition.SpecialType == special);
            if (face?.GetMembers(name).FirstOrDefault() is { } contract
                && type.FindImplementationForInterfaceMember(contract) is { } answer)
                return answer;
        }
        return null;
    }

    /// <summary>
    /// <c>[Symbol.iterator]() { return $eq.linq.iterate(this.getEnumerator()); }</c> for the twin of the type
    /// that declares the method its iteration goes through, and null for any other: a type that is no
    /// sequence, or one that inherits its iteration from a base's twin.
    /// </summary>
    public static JsClassMember? IteratorOf(INamedTypeSymbol type, HashSet<string> usedHelpers)
    {
        if (EnumeratorOf(type) is not { } through
            || !SymbolEqualityComparer.Default.Equals(through.ContainingType.OriginalDefinition, type.OriginalDefinition))
            return null;
        usedHelpers.Add(Eq.Import);
        return JsClassMember.Method("", Iterator, "", "", "",
            JsStatement.Block([JsStatement.Return(JsExpr.Call(JsExpr.Identifier(Eq.LinqIterate),
                JsExpr.Call(JsExpr.ThisMember(TwinMethodName.Of(through)))))]));
    }
}
