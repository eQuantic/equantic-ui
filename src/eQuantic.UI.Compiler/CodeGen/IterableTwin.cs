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
/// So the twin of the type that declares the method its iteration goes through, its implementation of
/// <c>IEnumerable&lt;T&gt;.GetEnumerator()</c> (or of the non-generic one, for a type that implements no
/// <c>IEnumerable&lt;T&gt;</c>), carries a <c>[Symbol.iterator]</c> that calls it, by the name it holds
/// (<see cref="TwinMethodName"/>), and walks what it returns (<c>$eq.linq.iterate</c>): an enumerator an
/// iterator method filled, or one the app wrote, by its <c>MoveNext</c> and <c>Current</c>. A derived
/// type inherits it, and its override of the method answers. The non-generic
/// <c>IEnumerable.GetEnumerator()</c> beside the generic one is not written at all: IEnumerable&lt;T&gt;
/// derives from IEnumerable, so the two are one sequence by its contract, and the twin holds one; nor is
/// an enumerator's explicit <c>IEnumerator.Current</c> beside its generic one.
/// </para>
/// </summary>
internal static class IterableTwin
{
    /// <summary>The name of the member JavaScript's iteration reads.</summary>
    private const string Iterator = "[Symbol.iterator]";

    /// <summary>
    /// The method <paramref name="type"/>'s iteration goes through: what implements
    /// <c>IEnumerable&lt;T&gt;.GetEnumerator()</c> for it, or the non-generic <c>IEnumerable</c>'s where it
    /// implements no <c>IEnumerable&lt;T&gt;</c>; null for a type that is no sequence.
    /// </summary>
    public static IMethodSymbol? EnumeratorOf(INamedTypeSymbol type)
    {
        var generic = type.AllInterfaces.FirstOrDefault(face =>
            face.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);
        var plain = type.AllInterfaces.FirstOrDefault(face => face.SpecialType == SpecialType.System_Collections_IEnumerable);
        foreach (var face in new[] { generic, plain })
        {
            if (face?.GetMembers("GetEnumerator").OfType<IMethodSymbol>().FirstOrDefault() is { } contract
                && type.FindImplementationForInterfaceMember(contract) is IMethodSymbol answer)
                return answer;
        }
        return null;
    }

    /// <summary>
    /// Whether the twin leaves <paramref name="member"/> out: an explicit implementation of a member of the
    /// non-generic <c>IEnumerable</c> or <c>IEnumerator</c> (<c>GetEnumerator()</c>, <c>Current</c>) in a
    /// type whose generic interface's member of that name another member answers. The two hold one name on
    /// the twin, and the generic interface derives from the non-generic one, so by its contract they answer
    /// alike: written after the generic one, the explicit one replaced it and called itself.
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
            var generic = contract.ContainingType.SpecialType switch
            {
                SpecialType.System_Collections_IEnumerable => SpecialType.System_Collections_Generic_IEnumerable_T,
                SpecialType.System_Collections_IEnumerator => SpecialType.System_Collections_Generic_IEnumerator_T,
                _ => SpecialType.None,
            };
            if (generic == SpecialType.None) continue;
            if (member.ContainingType.AllInterfaces.FirstOrDefault(face => face.OriginalDefinition.SpecialType == generic)
                    ?.GetMembers(contract.Name).FirstOrDefault() is { } counterpart
                && member.ContainingType.FindImplementationForInterfaceMember(counterpart) is { } answer
                && !SymbolEqualityComparer.Default.Equals(answer.OriginalDefinition, member.OriginalDefinition))
                return true;
        }
        return false;
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
