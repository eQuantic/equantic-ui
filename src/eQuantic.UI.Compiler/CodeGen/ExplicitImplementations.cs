using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Extensions;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// An explicit interface implementation is written under the name of the member it implements, the name
/// every call through the interface reaches it by, and a JavaScript class holds one member per name
/// (#1139). C# reaches an explicit implementation only through its interface, so two of one name live
/// side by side there, and on the twin the later replaced the earlier in silence: <c>IFoo.Run()</c> and
/// <c>IBar.Run()</c> both answered as <c>IBar</c>'s, and <c>string IBar.Run() =&gt; ((IFoo)this).Run();</c>,
/// written last, called itself until the stack overflowed.
/// <para>
/// A call through an interface binds the INTERFACE's member, never the type that implements it, so the
/// call cannot be routed to a name derived from one implementation: every type that implements the
/// interface would have to answer that name. What the twin can do is hold a name once.
/// </para>
/// <para>
/// So an explicit implementation that hands back what the member holding its name answers
/// (<see cref="HandsOff"/>), the type's own member of that name or another interface's member the type
/// implements, is not written (<see cref="LeavesOut"/>): that member answers every call through each
/// interface, as they answer alike in .NET. Every other one is written (<see cref="Written"/>), and what
/// its name meets on the twin, another one written under it or a member of the type or of a base it
/// declares, is refused (EQ1007, <see cref="Services.OverloadedMethods"/>). A method of the type's own
/// beside one moves aside to a name of its own instead (<see cref="TwinMethodName.ExplicitBeside"/>,
/// #708). The enumeration interfaces' members the twin holds once by their contract are
/// <see cref="IterableTwin.LeavesOut"/>'s.
/// </para>
/// </summary>
internal static class ExplicitImplementations
{
    /// <summary>Whether the twin leaves <paramref name="member"/> out: an enumeration interface's member
    /// another one answers for (<see cref="IterableTwin.LeavesOut"/>), or an explicit implementation that
    /// hands back what the member holding its name answers.</summary>
    public static bool LeavesOut(ISymbol member) =>
        IterableTwin.LeavesOut(member)
        || (IsExplicit(member) && member.ContainingType is { } type
            && Settle(type, member.MemberTwinName(), member.IsStatic).LeftOut.Contains(member.OriginalDefinition));

    /// <summary>
    /// The explicit implementations <paramref name="type"/>'s twin writes under <paramref name="name"/>, on
    /// its prototype or, for <paramref name="isStatic"/>, on the class, in the order they are declared:
    /// each of its own that hands nothing back to the member holding the name. Two of them, or one beside
    /// another member the twin holds under the name, cannot both be held.
    /// </summary>
    public static IReadOnlyList<ISymbol> Written(INamedTypeSymbol type, string name, bool isStatic) =>
        Settle(type, name, isStatic).Written;

    /// <summary>Whether <paramref name="member"/> is an explicit implementation of an interface's method or
    /// property, an indexer's being <see cref="Strategies.Expressions.Indexer"/>'s.</summary>
    public static bool IsExplicit(ISymbol member) => member switch
    {
        IMethodSymbol { MethodKind: MethodKind.ExplicitInterfaceImplementation } => true,
        IPropertySymbol { IsIndexer: false, ExplicitInterfaceImplementations.IsEmpty: false } => true,
        _ => false,
    };

    /// <summary>What each type settled, by name and side: every call site asks it of the method it binds
    /// (<see cref="TwinMethodName.ExplicitBeside"/>), and settling binds the bodies of the explicit ones.</summary>
    private static readonly ConditionalWeakTable<INamedTypeSymbol, ConcurrentDictionary<(string Name, bool IsStatic), Settled>> Cache = new();

    /// <summary>The explicit implementations of one name and side a type writes, and the ones it leaves out.</summary>
    private sealed record Settled(IReadOnlyList<ISymbol> Written, HashSet<ISymbol> LeftOut);

    /// <summary>
    /// What <paramref name="type"/> does with its explicit implementations of one name and side: the ones
    /// it writes, and the ones it leaves out because they hand back what the member holding the name
    /// answers. One that hands off to an explicit implementation with an answer of its own leaves it the
    /// name, and so does one that hands off to a member of a base whose twin holds it under the name. A
    /// property of the type's own or a base's always holds its name. One that hands off to a method of the
    /// type's own leaves it the name when that method holds it, which it does unless it hides an inherited
    /// member, is a typed <c>Equals</c>, or stands beside an explicit implementation the type writes, which
    /// takes the name (#708): so those are settled last, once every other one is.
    /// </summary>
    private static Settled Settle(INamedTypeSymbol type, string name, bool isStatic) =>
        Cache.GetValue(type.OriginalDefinition, _ => new()).GetOrAdd((name, isStatic), _ =>
        {
            var group = type.GetMembers()
                .Where(member => IsExplicit(member) && member.IsStatic == isStatic && member.MemberTwinName() == name
                    && !IterableTwin.LeavesOut(member))
                .OrderBy(member => member.Locations.FirstOrDefault()?.SourceSpan.Start ?? 0)
                .Select(member => (Member: member, Reached: Reached(member)))
                .Select(entry => (entry.Member, Reached: entry.Reached is { } reached && CanHold(reached, entry.Member, name) ? reached : null))
                .ToList();
            var leftOut = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            var toOwn = group.Where(entry => entry.Reached is not null && IsOwnMethod(entry.Reached, type)).ToList();
            foreach (var (member, reached) in group.Except(toOwn))
                if (reached is not null && Holds(reached, type, name, besideWritten: false))
                    leftOut.Add(member.OriginalDefinition);
            var besideWritten = group.Except(toOwn).Any(entry => !leftOut.Contains(entry.Member.OriginalDefinition));
            foreach (var (member, reached) in toOwn)
                if (Holds(reached!, type, name, besideWritten))
                    leftOut.Add(member.OriginalDefinition);
            return new Settled(group.Select(entry => entry.Member).Where(member => !leftOut.Contains(member.OriginalDefinition)).ToList(), leftOut);
        });

    /// <summary>Whether <paramref name="reached"/> is a method whose name <paramref name="type"/> settles: one
    /// it declares that fills no base's slot.</summary>
    private static bool IsOwnMethod(ISymbol reached, INamedTypeSymbol type) =>
        reached is IMethodSymbol method && !IsExplicit(method)
        && SymbolEqualityComparer.Default.Equals(TwinMethodName.Slot(method).ContainingType.OriginalDefinition, type.OriginalDefinition);

    /// <summary>Whether <paramref name="reached"/>, the member an explicit implementation's hand-offs end at,
    /// could hold its name: a member of the same kind and side, written under the same name, that a twin
    /// carries (not one kept on the server, and not an interface's default).</summary>
    private static bool CanHold(ISymbol reached, ISymbol member, string name) =>
        !SymbolEqualityComparer.Default.Equals(reached.OriginalDefinition, member.OriginalDefinition)
        && reached.Kind == member.Kind && reached.IsStatic == member.IsStatic
        && reached.MemberTwinName() == name
        && reached.ContainingType.TypeKind is TypeKind.Class or TypeKind.Struct
        && !reached.IsHostOnly();

    /// <summary>Whether <paramref name="reached"/> holds <paramref name="name"/> on its type's twin, beside an
    /// explicit implementation <paramref name="type"/> writes or not.</summary>
    private static bool Holds(ISymbol reached, INamedTypeSymbol type, string name, bool besideWritten)
    {
        if (IsExplicit(reached)) return HandsOff(reached) is null && !IterableTwin.LeavesOut(reached);
        if (reached is not IMethodSymbol method) return true;
        // A method of a base, or an override, whose slot a base holds: that type's twin settles its name.
        if (!IsOwnMethod(method, type)) return TwinMethodName.Of(method) == name;
        // The type's own method: its name is settled here, beside its explicit siblings (TwinMethodName.Of).
        var slot = TwinMethodName.Slot(method);
        return !besideWritten && !TwinMethodName.TypedEquals(slot) && TwinMethodName.Hidden(slot) is null;
    }

    /// <summary>The member <paramref name="member"/> reaches through its hand-offs (<see cref="HandsOff"/>),
    /// followed while each hands off to one not reached yet, or null where it hands nothing off.</summary>
    public static ISymbol? Reached(ISymbol member, Compilation? compilation = null)
    {
        if (HandsOff(member, compilation) is not { } next) return null;
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default) { member.OriginalDefinition };
        while (seen.Add(next.OriginalDefinition) && HandsOff(next, compilation) is { } further) next = further;
        return next;
    }

    /// <summary>
    /// The member whose answer <paramref name="member"/>'s body hands back as it is: a method's one
    /// statement or its expression, or a property's getter, calling a method of the same object
    /// (<c>this</c>, written or not, cast to an interface or not), or of its type for a static, with each of
    /// its own parameters in its place and nothing else, or reading a property of it where it takes none;
    /// and a property's setter, where it has one, writing that property with the value it is given. What
    /// is handed back is converted only as a reference or a box. A member of an interface is the one the
    /// type implements it with. Null for any other body, and for a member with no source.
    /// </summary>
    public static ISymbol? HandsOff(ISymbol member, Compilation? compilation = null)
    {
        if (member.DeclaringSyntaxReferences is not [var reference]) return null;
        var declaration = reference.GetSyntax();
        if (ModelOf(member, compilation, declaration.SyntaxTree) is not { } model) return null;
        var target = declaration switch
        {
            MethodDeclarationSyntax method => Answer(member, Single(method.Body, method.ExpressionBody), model),
            PropertyDeclarationSyntax property => Read(member, property, model),
            _ => null,
        };
        return target is null ? null : Implementation(member, target);
    }

    /// <summary>The one expression a body is: an arrow's, a block's one <c>return</c>'s, or its one
    /// statement's.</summary>
    private static ExpressionSyntax? Single(BlockSyntax? block, ArrowExpressionClauseSyntax? arrow) =>
        arrow?.Expression ?? block switch
        {
            { Statements: [ReturnStatementSyntax { Expression: { } value }] } => value,
            { Statements: [ExpressionStatementSyntax { Expression: var value }] } => value,
            _ => null,
        };

    /// <summary>What <paramref name="expression"/> hands back of the same object: a method it calls with
    /// <paramref name="member"/>'s own parameters in place, or a property it reads where the member takes
    /// none.</summary>
    private static ISymbol? Answer(ISymbol member, ExpressionSyntax? expression, SemanticModel model) =>
        expression is null
            ? null
            : Unconverted(model.GetOperation(expression)) switch
            {
                IInvocationOperation call when OnTheSameObject(member, call.Instance, call.TargetMethod)
                    && PassesThrough(member, call) => call.TargetMethod,
                IPropertyReferenceOperation { Arguments.Length: 0 } read when OnTheSameObject(member, read.Instance, read.Property)
                    && Parameters(member).IsEmpty => read.Property,
                _ => null,
            };

    /// <summary>What a property's getter hands back (<see cref="Answer"/>), where its setter, if it has one,
    /// writes the property it reads with the value it is given.</summary>
    private static ISymbol? Read(ISymbol member, PropertyDeclarationSyntax property, SemanticModel model)
    {
        var getter = property.AccessorList?.Accessors.FirstOrDefault(accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration));
        var read = property.ExpressionBody?.Expression ?? (getter is null ? null : Single(getter.Body, getter.ExpressionBody));
        if (Answer(member, read, model) is not { } target) return null;
        var setter = property.AccessorList?.Accessors.FirstOrDefault(accessor =>
            accessor.IsKind(SyntaxKind.SetAccessorDeclaration) || accessor.IsKind(SyntaxKind.InitAccessorDeclaration));
        if (setter is null) return target;
        return target is IPropertySymbol
            && Single(setter.Body, setter.ExpressionBody) is { } write
            && model.GetOperation(write) is ISimpleAssignmentOperation
                { Target: IPropertyReferenceOperation { Arguments.Length: 0 } written, Value: var value }
            && OnTheSameObject(member, written.Instance, written.Property)
            && SymbolEqualityComparer.Default.Equals(Implementation(member, written.Property)?.OriginalDefinition,
                Implementation(member, target)?.OriginalDefinition)
            && Unconverted(value) is IParameterReferenceOperation { Parameter.IsImplicitlyDeclared: true }
            ? target
            : null;
    }

    /// <summary>
    /// Whether a member is reached on the object <paramref name="member"/> answers for, as a call through
    /// its interface would reach it: through <c>this</c>, written or not, cast or not, for an instance
    /// member, and through no object, on its type or a base, for a static one. A call through <c>base</c>
    /// skips the type's override, which a call through the interface would reach.
    /// </summary>
    private static bool OnTheSameObject(ISymbol member, IOperation? receiver, ISymbol target) =>
        member.IsStatic
            ? target.IsStatic && receiver is null && InChain(member.ContainingType, target.ContainingType)
            : !target.IsStatic
              && Unconverted(receiver) is IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance } self
              && self.Syntax is not BaseExpressionSyntax;

    /// <summary>Whether <paramref name="holder"/> is <paramref name="type"/> or one of its bases.</summary>
    private static bool InChain(INamedTypeSymbol type, INamedTypeSymbol holder)
    {
        for (INamedTypeSymbol? at = type; at is not null; at = at.BaseType)
            if (SymbolEqualityComparer.Default.Equals(at.OriginalDefinition, holder.OriginalDefinition)) return true;
        return false;
    }

    /// <summary>Whether <paramref name="call"/> passes each of <paramref name="member"/>'s parameters, and
    /// only them, in the place of the parameter it binds, as written and by the same kind of reference,
    /// and its type parameters, in order.</summary>
    private static bool PassesThrough(ISymbol member, IInvocationOperation call)
    {
        var parameters = Parameters(member);
        var typeParameters = member is IMethodSymbol method ? method.TypeParameters : [];
        if (call.Arguments.Length != parameters.Length
            || !call.TargetMethod.TypeArguments.SequenceEqual<ITypeSymbol>(typeParameters, SymbolEqualityComparer.Default))
            return false;
        foreach (var argument in call.Arguments)
        {
            if (argument.ArgumentKind != ArgumentKind.Explicit || argument.Parameter is not { } bound
                || Unconverted(argument.Value) is not IParameterReferenceOperation { Parameter: var passed }
                || !SymbolEqualityComparer.Default.Equals(passed, parameters[bound.Ordinal])
                || passed.RefKind != bound.RefKind)
                return false;
        }
        return true;
    }

    /// <summary>The parameters a member takes: a method's, and none for a property.</summary>
    private static ImmutableArray<IParameterSymbol> Parameters(ISymbol member) =>
        member is IMethodSymbol method ? method.Parameters : [];

    /// <summary>The member <paramref name="member"/>'s type answers <paramref name="target"/> with: the
    /// implementation of an interface's member, and any other member itself.</summary>
    private static ISymbol? Implementation(ISymbol member, ISymbol target) =>
        target.ContainingType.TypeKind == TypeKind.Interface
            ? member.ContainingType.FindImplementationForInterfaceMember(target)
            : target;

    /// <summary>An operation under the conversions that keep its value: an identity, a reference's and a
    /// box.</summary>
    private static IOperation? Unconverted(IOperation? operation)
    {
        while (operation is IConversionOperation converted
               && converted.GetConversion() is { IsIdentity: true } or { IsReference: true } or { IsBoxing: true })
            operation = converted.Operand;
        return operation;
    }

    /// <summary>The model of the tree <paramref name="member"/> is declared in: from the compilation its
    /// assembly is the source of, or from the one a caller holds.</summary>
    private static SemanticModel? ModelOf(ISymbol member, Compilation? compilation, SyntaxTree tree) =>
        (member.ContainingAssembly as ISourceAssemblySymbol)?.Compilation.ModelOf(tree) ?? compilation?.ModelOf(tree);
}
