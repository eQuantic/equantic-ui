using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
/// iterator method filled, or one the app wrote, by its <c>MoveNext</c> and <c>Current</c>. That is what
/// LINQ, a spread, <c>string.Join</c> and a <c>foreach</c> over the interface reach in .NET. A
/// <c>foreach</c> over the class itself binds the type's own public <c>GetEnumerator()</c> where it has
/// one, which may answer another sequence, and calls it (<see cref="ForEachSource"/>). A derived type
/// inherits the iterator, and its override of the method answers.
/// </para>
/// <para>
/// The two <c>GetEnumerator</c>s are two members. An explicit <c>IEnumerable&lt;T&gt;.GetEnumerator()</c>
/// holds the interface's name and the public one beside it a name of its own
/// (<see cref="TwinMethodName.ExplicitBeside"/>): merged into one, the public one answered for LINQ too,
/// where C# lets the two walk different sequences (Copilot's review of #708). The non-generic member beside
/// the generic interface's is not written: <c>IEnumerable&lt;T&gt;</c> derives from <c>IEnumerable</c>, so
/// by its contract the two answer alike, and written after the generic one it replaced it and called
/// itself. Nor is an explicit <c>Current</c> beside the type's own public one, since the twin holds one
/// property per name: the public one answers for both.
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
    /// What a <c>foreach</c> walks where the <c>GetEnumerator()</c> it binds is a method of the app's other
    /// than the one the collection's <c>[Symbol.iterator]</c> calls: <c>$eq.linq.iterate</c> over that
    /// method's enumerator, called by the name it holds. A <c>foreach</c> over a class with a public
    /// <c>GetEnumerator()</c> beside an explicit <c>IEnumerable&lt;T&gt;.GetEnumerator()</c> walks the public
    /// one, as C# binds it. Null for every other loop, which walks the collection's own iteration.
    /// </summary>
    public static JsExpr? ForEachSource(ForEachStatementInfo info, ITypeSymbol? collection, JsExpr source, HashSet<string> usedHelpers)
    {
        if (info.GetEnumeratorMethod is not { MethodKind: MethodKind.Ordinary, IsStatic: false, IsExtensionMethod: false } bound
            || !bound.Locations.Any(location => location.IsInSource)
            || collection is INamedTypeSymbol named && EnumeratorOf(named) is { } iterated
               && SymbolEqualityComparer.Default.Equals(iterated.OriginalDefinition, bound.OriginalDefinition))
            return null;
        usedHelpers.Add(Eq.Import);
        return JsExpr.Call(JsExpr.Identifier(Eq.LinqIterate), JsExpr.Call(JsExpr.Member(source, TwinMethodName.Of(bound))));
    }

    /// <summary>
    /// Whether the twin leaves <paramref name="member"/> out, an explicit implementation of a member of the
    /// enumeration interfaces: the non-generic <c>IEnumerable</c>'s or <c>IEnumerator</c>'s beside the
    /// generic interface's member of its name, which another member answers, and a <c>Current</c> beside
    /// the type's own public one. Each holds one name on the twin with the member that stays, and answers
    /// alike by the interfaces' contract: written after it, the explicit one replaced it and read itself.
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
            var generic = contract.ContainingType.OriginalDefinition.SpecialType switch
            {
                SpecialType.System_Collections_IEnumerable => SpecialType.System_Collections_Generic_IEnumerable_T,
                SpecialType.System_Collections_IEnumerator => SpecialType.System_Collections_Generic_IEnumerator_T,
                SpecialType.System_Collections_Generic_IEnumerator_T => SpecialType.System_Collections_Generic_IEnumerator_T,
                _ => SpecialType.None,
            };
            if (generic == SpecialType.None) continue;
            if (member is IPropertySymbol && OwnProperty(member.ContainingType, contract.Name) is { } own
                && !SymbolEqualityComparer.Default.Equals(own.OriginalDefinition, member.OriginalDefinition))
                return true;
            if (contract.ContainingType.OriginalDefinition.SpecialType == generic) continue;
            if (member.ContainingType.AllInterfaces.FirstOrDefault(face => face.OriginalDefinition.SpecialType == generic)
                    ?.GetMembers(contract.Name).FirstOrDefault() is { } counterpart
                && member.ContainingType.FindImplementationForInterfaceMember(counterpart) is { } answer
                && !SymbolEqualityComparer.Default.Equals(answer.OriginalDefinition, member.OriginalDefinition))
                return true;
        }
        return false;
    }

    /// <summary>The type's own public instance property of <paramref name="name"/>, declared on it or a
    /// base, or null.</summary>
    private static IPropertySymbol? OwnProperty(INamedTypeSymbol type, string name)
    {
        for (var at = type; at is not null; at = at.BaseType)
            foreach (var property in at.GetMembers(name).OfType<IPropertySymbol>())
                if (property is { IsStatic: false, IsIndexer: false, DeclaredAccessibility: Accessibility.Public })
                    return property;
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
