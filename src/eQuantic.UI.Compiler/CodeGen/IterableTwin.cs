using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.CodeGen.Extensions;

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
/// <para>
/// C# does not hold a type to that contract, so the build does (<see cref="Services.OverloadedMethods"/>,
/// Copilot's second review of #708). A member the twin holds once with another answers as that one only
/// where it hands back what that one answers (<see cref="AnswersAs"/>), and a type that implements
/// <c>IEnumerable&lt;T&gt;</c>, or <c>IEnumerator&lt;T&gt;</c>, over two types would walk one sequence, or
/// read one <c>Current</c>, for both (<see cref="Over"/>): each is refused, where the twin walked or read
/// the other's answer.
/// </para>
/// </summary>
internal static class IterableTwin
{
    /// <summary>The name of the member JavaScript's iteration reads.</summary>
    private const string Iterator = "[Symbol.iterator]";

    /// <summary>
    /// The method <paramref name="type"/>'s iteration goes through: what implements
    /// <c>IEnumerable&lt;T&gt;.GetEnumerator()</c> for it, or the non-generic <c>IEnumerable</c>'s where it
    /// implements no <c>IEnumerable&lt;T&gt;</c>; null for a type that is no sequence. A type that implements
    /// <c>IEnumerable&lt;T&gt;</c> over two types is refused (<see cref="Over"/>).
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

    /// <summary>
    /// The constructions of the generic interface <paramref name="kind"/> (<c>IEnumerable&lt;T&gt;</c> or
    /// <c>IEnumerator&lt;T&gt;</c>) that <paramref name="type"/> implements, each once. The twin has one
    /// iteration, which LINQ, a spread, <c>string.Join</c> and a <c>foreach</c> over each of them walk, and
    /// one <c>current</c>, so a type with two would answer one of them with the other's sequence.
    /// </summary>
    public static IReadOnlyList<INamedTypeSymbol> Over(INamedTypeSymbol type, SpecialType kind) =>
        type.AllInterfaces.Where(face => face.OriginalDefinition.SpecialType == kind)
            .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default).ToList();

    /// <summary>
    /// The members whose answers <paramref name="type"/>'s twin holds once, each with the one it holds: the
    /// implementation of the non-generic <c>IEnumerable.GetEnumerator()</c> beside the generic one's, which
    /// the twin's iteration calls (<see cref="EnumeratorOf"/>), and each implementation of
    /// <c>IEnumerator.Current</c> and <c>IEnumerator&lt;T&gt;.Current</c> beside the <c>current</c> the twin
    /// holds, the type's own public <c>Current</c> where it has one. Only the pairs of two members.
    /// </summary>
    public static IEnumerable<(ISymbol Member, ISymbol Held)> HeldOnce(INamedTypeSymbol type)
    {
        if (Over(type, SpecialType.System_Collections_Generic_IEnumerable_T) is [var sequence]
            && Implementation(type, sequence, "GetEnumerator") is { } walked
            && Implementation(type, Plain(type, SpecialType.System_Collections_IEnumerable), "GetEnumerator") is { } plain
            && !Same(plain, walked))
            yield return (plain, walked);
        if (Over(type, SpecialType.System_Collections_Generic_IEnumerator_T) is not [var enumerator]) yield break;
        var generic = Implementation(type, enumerator, "Current");
        if ((OwnProperty(type, "Current") ?? generic) is not { } held) yield break;
        foreach (var answer in new[] { generic, Implementation(type, Plain(type, SpecialType.System_Collections_IEnumerator), "Current") })
            if (answer is not null && !Same(answer, held))
                yield return (answer, held);
    }

    /// <summary>
    /// Whether <paramref name="member"/> answers as <paramref name="held"/>, the member the twin holds in
    /// its place (<see cref="HeldOnce"/>): its body hands back what a member of the same object answers
    /// (<see cref="ExplicitImplementations.HandsOff"/>), and that member is the one <paramref name="held"/>
    /// reaches the same way, each followed through every such hand-off
    /// (<c>IEnumerator IEnumerable.GetEnumerator() =&gt; GetEnumerator();</c>,
    /// <c>object IEnumerator.Current =&gt; Current;</c>, a cast to the interface included). Anything else
    /// may answer otherwise, which C# allows: an iterator of its own walked the generic one's sequence in
    /// the browser, and a <c>Current</c> of its own read the public one's.
    /// </summary>
    public static bool AnswersAs(ISymbol member, ISymbol held, Compilation compilation) =>
        ExplicitImplementations.Reached(member, compilation) is { } reached
        && Same(reached, ExplicitImplementations.Reached(held, compilation) ?? held);

    /// <summary>The member <paramref name="type"/> implements <paramref name="face"/>'s member of
    /// <paramref name="name"/> with, or null.</summary>
    private static ISymbol? Implementation(INamedTypeSymbol type, INamedTypeSymbol? face, string name) =>
        face?.GetMembers(name).FirstOrDefault() is { } contract ? type.FindImplementationForInterfaceMember(contract) : null;

    /// <summary>The non-generic interface of <paramref name="kind"/> that <paramref name="type"/> implements, or null.</summary>
    private static INamedTypeSymbol? Plain(INamedTypeSymbol type, SpecialType kind) =>
        type.AllInterfaces.FirstOrDefault(face => face.SpecialType == kind);

    private static bool Same(ISymbol one, ISymbol other) =>
        SymbolEqualityComparer.Default.Equals(one.OriginalDefinition, other.OriginalDefinition);

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
    /// sequence, one whose <c>GetEnumerator()</c> stays on the server, or one that inherits its iteration
    /// from a base's twin.
    /// </summary>
    public static JsClassMember? IteratorOf(INamedTypeSymbol type, HashSet<string> usedHelpers)
    {
        if (EnumeratorOf(type) is not { } through
            || through.IsHostOnly()
            || !SymbolEqualityComparer.Default.Equals(through.ContainingType.OriginalDefinition, type.OriginalDefinition))
            return null;
        usedHelpers.Add(Eq.Import);
        return JsClassMember.Method("", Iterator, "", "", "",
            JsStatement.Block([JsStatement.Return(JsExpr.Call(JsExpr.Identifier(Eq.LinqIterate),
                JsExpr.Call(JsExpr.ThisMember(TwinMethodName.Of(through)))))]));
    }

    /// <summary>
    /// Refuses a <c>foreach</c> or <c>await foreach</c> whose bound enumeration protocol reaches a
    /// <c>[ServerOnly]</c> method or type: <c>GetEnumerator</c>/<c>GetAsyncEnumerator</c> (instance,
    /// interface or extension), <c>MoveNext</c>/<c>MoveNextAsync</c>, <c>Current</c> or
    /// <c>Dispose</c>/<c>DisposeAsync</c>.
    /// </summary>
    public static void FenceForEach(CommonForEachStatementSyntax statement, ITypeSymbol? collection,
        ConversionContext context)
    {
        if (context.SemanticHelper.ForEachInfo(statement) is not { GetEnumeratorMethod: { } bound } info)
        {
            FenceEnumeration(collection, statement.Expression, context);
            return;
        }
        var before = context.Diagnostics.Count;
        if ((bound.ReducedFrom ?? bound) is { IsExtensionMethod: true } extension)
        {
            extension.ReportIfHostOnly(statement.Expression, context);
            extension.ContainingType.ReportIfKeptOut(statement.Expression, context);
        }
        else if (bound.ExtensionBlockHome() is { } home)
        {
            bound.ReportIfHostOnly(statement.Expression, context);
            home.ReportIfKeptOut(statement.Expression, context);
        }
        else
        {
            bound.ReportIfHostOnly(statement.Expression, context, collection);
        }
        if (context.Diagnostics.Count > before) return;
        var enumerator = bound.ReturnType;
        info.MoveNextMethod?.ReportIfHostOnly(statement.Expression, context, enumerator);
        ((ISymbol?)info.CurrentProperty?.GetMethod ?? info.CurrentProperty)?.ReportIfHostOnly(statement.Expression, context, enumerator);
        info.DisposeMethod?.ReportIfHostOnly(statement.Expression, context, enumerator);
    }

    /// <summary>
    /// Refuses an implicit iteration (a collection expression spread <c>[.. xs]</c> or a LINQ source) over a
    /// sequence whose <c>GetEnumerator()</c> or enumerator protocol (<c>MoveNext</c>, <c>Current</c>,
    /// <c>Dispose</c>) is <c>[ServerOnly]</c>.
    /// </summary>
    public static void FenceEnumeration(ITypeSymbol? sequence, SyntaxNode at, ConversionContext context)
    {
        if (sequence.UnwrapNullable() is not INamedTypeSymbol named) return;
        var enumerator = named.TypeKind == TypeKind.Interface
            ? named.GetMembers("GetEnumerator").OfType<IMethodSymbol>().FirstOrDefault()
              ?? named.AllInterfaces.SelectMany(face => face.GetMembers("GetEnumerator")).OfType<IMethodSymbol>().FirstOrDefault()
            : EnumeratorOf(named) ?? named.GetMembers("GetEnumerator").OfType<IMethodSymbol>()
                .FirstOrDefault(method => method is { IsStatic: false, Parameters.Length: 0 });
        if (enumerator is null) return;
        var before = context.Diagnostics.Count;
        enumerator.ReportIfHostOnly(at, context, named);
        if (context.Diagnostics.Count > before || enumerator.ReturnType.UnwrapNullable() is not INamedTypeSymbol cursor) return;
        ProtocolMethod(cursor, "MoveNext")?.ReportIfHostOnly(at, context, cursor);
        var current = ProtocolProperty(cursor, "Current");
        ((ISymbol?)current?.GetMethod ?? current)?.ReportIfHostOnly(at, context, cursor);
        ProtocolMethod(cursor, "Dispose")?.ReportIfHostOnly(at, context, cursor);
    }

    private static IMethodSymbol? ProtocolMethod(INamedTypeSymbol type, string name)
    {
        for (var at = type; at is not null; at = at.BaseType)
            if (at.GetMembers(name).OfType<IMethodSymbol>()
                    .FirstOrDefault(method => method is { IsStatic: false, Parameters.Length: 0, DeclaredAccessibility: Accessibility.Public })
                is { } own)
                return own;
        foreach (var face in type.AllInterfaces)
            if (face.GetMembers(name).OfType<IMethodSymbol>().FirstOrDefault(method => method.Parameters.Length == 0) is { } contract)
                return type.TypeKind == TypeKind.Interface
                    ? contract
                    : type.FindImplementationForInterfaceMember(contract) as IMethodSymbol ?? contract;
        return null;
    }

    private static IPropertySymbol? ProtocolProperty(INamedTypeSymbol type, string name)
    {
        if (OwnProperty(type, name) is { } own) return own;
        foreach (var face in type.AllInterfaces)
            if (face.GetMembers(name).OfType<IPropertySymbol>().FirstOrDefault() is { } contract)
                return type.TypeKind == TypeKind.Interface
                    ? contract
                    : type.FindImplementationForInterfaceMember(contract) as IPropertySymbol ?? contract;
        return null;
    }
}
