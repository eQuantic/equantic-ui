using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Extensions;

/// <summary>
/// The contracts a <c>[ServerOnly]</c> method fulfils for a type that crosses: the interface members it
/// implements, for the type that declares it and for every type that takes it from a base, and the
/// methods it overrides, up to the root of the chain. No twin holds such a method
/// (<see cref="Parser.ComponentParser.StaysOnTheServer"/>), so a call bound to one of its contracts may
/// reach, in the browser, a member the twin does not have: <c>((IShape)circle).Area()</c> threw a
/// TypeError, and <c>((Named)derived).Name()</c> ran the base's method where .NET ran the server's. A
/// call bound to the method itself carries the attribute and is refused by it. A call through the
/// contract binds to a member that carries none, and is refused by this (EQ2010, asked by
/// <see cref="HostOnlySymbolExtensions.ReportIfHostOnly"/>).
/// <para>
/// A call reaches the method only where its receiver can be a type that takes it, so the question is
/// asked of the call's receiver too. A call bound to another override that is not <c>[ServerOnly]</c>
/// dispatches at or below that override, and a call made on a sibling of the method's type, through
/// it or bare inside it, never reaches the method at all: compared by the root of the chain alone,
/// <c>new Plain().Name()</c> and <c>new Mute().Name()</c> were refused beside a <c>Derived</c> that
/// keeps its own <c>Name</c> on the server. A receiver whose type is not known (a type parameter) may
/// be any of them.
/// </para>
/// <para>
/// It is a question about the BUILD. A project the app references calls its own contract, which only
/// the app fulfils, and a file of that project is converted in its project's compilation, which sees
/// nothing of the app. So the contracts are read once over the app's compilation and every compilation
/// it references (<see cref="ConversionContext.ProjectCompilation"/>), and a member or a type is found
/// by what it IS, its assembly and its documentation id, never by its symbol: each file is converted in
/// a compilation of its own, its tree replacing the project's, whose symbols compare equal to no other
/// file's, as <see cref="Services.ResourceCatalogIds"/> found first.
/// </para>
/// </summary>
internal static class ServerOnlyContracts
{
    /// <summary>One way a contract is fulfilled on the server only: the <c>[ServerOnly]</c> method, by
    /// name, and every type a receiver may be declared as and still hold a type that takes it, keyed by
    /// the open type's key.</summary>
    private sealed record Fulfilment(string Method, IReadOnlyDictionary<string, IReadOnlyList<INamedTypeSymbol>> Receivers);

    /// <summary>Each build's contracts, by the member's key.</summary>
    private static readonly ConditionalWeakTable<Compilation, IReadOnlyDictionary<string, IReadOnlyList<Fulfilment>>> Read = new();

    /// <summary>
    /// The <c>[ServerOnly]</c> methods of types that cross which a call bound to
    /// <paramref name="contract"/>, made on <paramref name="receiver"/>, may reach, by name, in order;
    /// none for a member that is no contract, and for one no such method fulfils.
    /// </summary>
    /// <param name="contract">The method a call, or a method group, binds.</param>
    /// <param name="receiver">The type the call is made on, asked only of a contract such a method
    /// fulfils; null where it is not known, which may be any.</param>
    /// <param name="build">The compilation the build reads every project from.</param>
    internal static IReadOnlyList<string> FulfilledOnTheServer(IMethodSymbol contract, Func<ITypeSymbol?> receiver,
        Compilation build)
    {
        // Only an interface member and a method that can be overridden dispatch to another body.
        if (!contract.IsVirtual && !contract.IsAbstract && !contract.IsOverride) return [];
        if (Key(contract) is not { } key || !Read.GetValue(build, Of).TryGetValue(key, out var fulfilments)) return [];
        var named = receiver().UnwrapNullable() as INamedTypeSymbol;
        var open = named is { TypeKind: not TypeKind.Error } ? Key(named) : null;
        return fulfilments.Where(fulfilment => open is null || CanHold(fulfilment.Receivers, open, named!))
            .Select(fulfilment => fulfilment.Method).Distinct().Order(StringComparer.Ordinal).ToList();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<Fulfilment>> Of(Compilation build)
    {
        var compilations = build.ReferencedCompilations().Prepend(build).ToList();
        var serverOnly = compilations.SelectMany(ServerOnlyMethods).ToList();
        var fulfilled = new Dictionary<string, List<Fulfilment>>(StringComparer.Ordinal);
        var receivers = new Dictionary<INamedTypeSymbol, IReadOnlyDictionary<string, IReadOnlyList<INamedTypeSymbol>>>(
            SymbolEqualityComparer.Default);

        void Add(IMethodSymbol contract, INamedTypeSymbol type, IMethodSymbol method)
        {
            if (Key(contract) is not { } key) return;
            if (!receivers.TryGetValue(type, out var through)) receivers[type] = through = Receivers(type);
            if (!fulfilled.TryGetValue(key, out var fulfilments)) fulfilled[key] = fulfilments = [];
            fulfilments.Add(new Fulfilment(NameOf(method), through));
        }

        // What each overrides, up to the root, for the type that declares it: a receiver of a type below
        // it binds a call to the method itself, or to an override below it. A type kept out takes no
        // part, and none derived from it crosses.
        foreach (var method in serverOnly.Where(method => !method.ContainingType.IsKeptOut()))
            for (var overridden = method.OverriddenMethod; overridden is not null;
                 overridden = overridden.OriginalDefinition.OverriddenMethod)
                Add(overridden, method.ContainingType, method);

        // What it implements, for each type that crosses and takes it: the type that declares it, and a
        // type derived from that one that implements an interface through it.
        if (serverOnly.Count > 0)
        {
            var methods = serverOnly.ToHashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            var declaring = serverOnly.Select(method => method.ContainingType.OriginalDefinition)
                .ToHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var type in compilations.SelectMany(compilation => TypesIn(compilation.Assembly.GlobalNamespace)))
            {
                if (type.IsKeptOut() || !Takes(type, declaring)) continue;
                foreach (var member in type.AllInterfaces.SelectMany(@interface => @interface.GetMembers()).OfType<IMethodSymbol>())
                {
                    if (member.MethodKind is not (MethodKind.Ordinary or MethodKind.UserDefinedOperator or MethodKind.Conversion))
                        continue;
                    if (type.FindImplementationForInterfaceMember(member) is IMethodSymbol implementation
                        && methods.Contains(implementation.OriginalDefinition))
                        Add(member, type, implementation.OriginalDefinition);
                }
            }
        }

        return fulfilled.ToDictionary(entry => entry.Key, entry => (IReadOnlyList<Fulfilment>)entry.Value,
            StringComparer.Ordinal);
    }

    /// <summary>
    /// The methods, operators and conversions of a compilation's source that no twin holds, found by the
    /// rule the emitters leave them out by: a project's every member is not bound to learn which few they
    /// are, since the rule binds only a member that writes an attribute or is a partial method.
    /// </summary>
    private static IEnumerable<IMethodSymbol> ServerOnlyMethods(Compilation compilation)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var declaration in tree.GetRoot()
                         .DescendantNodes(node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax or TypeDeclarationSyntax)
                         .OfType<BaseMethodDeclarationSyntax>())
            {
                if (!Parser.ComponentParser.StaysOnTheServer(declaration, inAComponent: false, model)) continue;
                if (model.GetDeclaredSymbol(declaration) is IMethodSymbol method) yield return method;
            }
        }
    }

    /// <summary>Every type a namespace of source declares, a nested one included.</summary>
    private static IEnumerable<INamedTypeSymbol> TypesIn(INamespaceSymbol space) =>
        space.GetTypeMembers().SelectMany(WithNested).Concat(space.GetNamespaceMembers().SelectMany(TypesIn));

    private static IEnumerable<INamedTypeSymbol> WithNested(INamedTypeSymbol type) =>
        type.GetTypeMembers().SelectMany(WithNested).Prepend(type);

    /// <summary>Whether <paramref name="type"/> is, derives from or implements a type that declares a
    /// <c>[ServerOnly]</c> method: no other type can implement an interface through one.</summary>
    private static bool Takes(INamedTypeSymbol type, HashSet<INamedTypeSymbol> declaring)
    {
        for (var at = type; at is not null; at = at.BaseType)
            if (declaring.Contains(at.OriginalDefinition)) return true;
        return type.AllInterfaces.Any(@interface => declaring.Contains(@interface.OriginalDefinition));
    }

    /// <summary>Every type a receiver may be declared as and hold a <paramref name="type"/>, grouped by
    /// the open type's key.</summary>
    private static IReadOnlyDictionary<string, IReadOnlyList<INamedTypeSymbol>> Receivers(INamedTypeSymbol type)
    {
        var byOpen = new Dictionary<string, List<INamedTypeSymbol>>(StringComparer.Ordinal);
        void Record(INamedTypeSymbol candidate)
        {
            if (Key(candidate) is not { } open) return;
            if (!byOpen.TryGetValue(open, out var list)) byOpen[open] = list = [];
            list.Add(candidate);
        }
        for (var at = type; at is not null; at = at.BaseType) Record(at);
        foreach (var @interface in type.AllInterfaces) Record(@interface);
        return byOpen.ToDictionary(entry => entry.Key, entry => (IReadOnlyList<INamedTypeSymbol>)entry.Value,
            StringComparer.Ordinal);
    }

    private static bool CanHold(IReadOnlyDictionary<string, IReadOnlyList<INamedTypeSymbol>> receivers,
        string open, INamedTypeSymbol receiver) =>
        receivers.TryGetValue(open, out var candidates)
        && candidates.Any(candidate => GenericArgumentsMatch(candidate, receiver));

    private static bool GenericArgumentsMatch(INamedTypeSymbol implemented, INamedTypeSymbol receiver)
    {
        if (!receiver.IsGenericType) return true;
        var parameters = receiver.OriginalDefinition.TypeParameters;
        if (implemented.TypeArguments.Length != parameters.Length || receiver.TypeArguments.Length != parameters.Length)
            return true;
        for (var i = 0; i < parameters.Length; i++)
            if (!TypeArgumentMatches(implemented.TypeArguments[i], receiver.TypeArguments[i], parameters[i].Variance))
                return false;
        return true;
    }

    private static bool TypeArgumentMatches(ITypeSymbol implementedArg, ITypeSymbol receiverArg, VarianceKind variance)
    {
        if (implementedArg is ITypeParameterSymbol || receiverArg is ITypeParameterSymbol) return true;
        if (SameClosedType(implementedArg, receiverArg)) return true;
        return variance switch
        {
            VarianceKind.Out => IsReferenceAssignable(implementedArg, receiverArg),
            VarianceKind.In => IsReferenceAssignable(receiverArg, implementedArg),
            _ => false,
        };
    }

    private static bool IsReferenceAssignable(ITypeSymbol subtype, ITypeSymbol supertype)
    {
        if (!subtype.IsReferenceType || !supertype.IsReferenceType) return false;
        if (supertype.SpecialType == SpecialType.System_Object) return true;
        if (subtype is IArrayTypeSymbol subArray)
        {
            if (supertype is IArrayTypeSymbol superArray && subArray.Rank == superArray.Rank)
                return SameClosedType(subArray.ElementType, superArray.ElementType)
                       || IsReferenceAssignable(subArray.ElementType, superArray.ElementType);
            if (supertype.SpecialType == SpecialType.System_Array) return true;
            return supertype is INamedTypeSymbol superInterface && Key(superInterface) is { } ifaceKey
                   && subArray.AllInterfaces.Any(candidate =>
                       Key(candidate) == ifaceKey && GenericArgumentsMatch(candidate, superInterface));
        }
        if (subtype is not INamedTypeSymbol subNamed
            || supertype is not INamedTypeSymbol superNamed
            || Key(superNamed) is not { } targetKey)
            return false;
        for (var at = subNamed; at is not null; at = at.BaseType)
            if (Key(at) == targetKey && GenericArgumentsMatch(at, superNamed)) return true;
        return subNamed.AllInterfaces.Any(@interface =>
            Key(@interface) == targetKey && GenericArgumentsMatch(@interface, superNamed));
    }

    private static bool SameClosedType(ITypeSymbol left, ITypeSymbol right) =>
        ClosedKey(left) is { } key && key == ClosedKey(right);

    private static string? ClosedKey(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => ClosedKey(array.ElementType) is { } element ? $"{element}[]" : null,
        INamedTypeSymbol { IsGenericType: true } named when Key(named) is { } open =>
            $"{open}<{string.Join(",", named.TypeArguments.Select(argument => ClosedKey(argument) ?? "?"))}>",
        INamedTypeSymbol named => Key(named),
        _ => null,
    };

    /// <summary>What a member or a type IS in every compilation of one build: its definition's assembly
    /// and documentation id. Null for one that has no id.</summary>
    private static string? Key(ISymbol symbol)
    {
        var definition = symbol.OriginalDefinition;
        return definition.GetDocumentationCommentId() is { } id ? $"{definition.ContainingAssembly?.Name}:{id}" : null;
    }

    /// <summary>A method as a message names it: its type, and the name it has on its contract.</summary>
    private static string NameOf(IMethodSymbol method) =>
        $"{method.ContainingType.ToDisplayString()}.{method.ExplicitInterfaceImplementations.FirstOrDefault()?.Name ?? method.Name}";
}
