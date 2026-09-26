using eQuantic.UI.Compiler.CodeGen.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// The default interface members a class relies on (#414). JavaScript has no interfaces, so a
/// member an interface implements for the classes that do not declare it has to be written into
/// the twin of each class that takes it. Nothing wrote it: <c>ICodeLanguage.Rules</c> is a default
/// property, <c>PlainTextLanguage</c> relies on it, and its twin had no <c>rules</c>, so every
/// plain-text <c>CodeBlock</c> threw on <c>indentWidth</c> in the browser.
/// </summary>
internal static class DefaultInterfaceMembers
{
    /// <summary>A default member a class takes: the implementation that applies to it, its
    /// declaration where the compilation has one, and the interface member it answers. An interface
    /// compiled into a referenced assembly has no declaration, and its body cannot be written. A
    /// private helper answers no interface member: no class implements it, so its contract is null.</summary>
    internal readonly record struct Inherited(ISymbol Implementation, MemberDeclarationSyntax? Declaration, ISymbol? Contract);

    /// <summary>
    /// Every default member <paramref name="type"/> takes from an interface, in the order the
    /// interfaces and their members are declared. The implementation is the one the language picks
    /// for the class, so a derived interface's override of a base interface's member wins over the
    /// base's default. A member whose implementation the base class takes too is left to the base:
    /// its twin carries it, and the prototype chain hands it down. One the base answers with a LESS
    /// specific default (the class lists an interface that overrides it) is the class's own, written
    /// over the base's: skipping every interface the base implements dropped it, and the class
    /// answered with its base's default (found in review, #418).
    /// </summary>
    public static IReadOnlyList<Inherited> Of(INamedTypeSymbol type, Compilation compilation)
    {
        var inherited = new List<Inherited>();
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var contract in type.AllInterfaces)
        {
            foreach (var member in contract.GetMembers())
            {
                if (member.IsStatic || member is not (IPropertySymbol or IMethodSymbol { MethodKind: MethodKind.Ordinary }))
                    continue;
                if (type.FindImplementationForInterfaceMember(member) is not { } implementation
                    || implementation.ContainingType.TypeKind != TypeKind.Interface
                    || implementation.IsAbstract
                    || !seen.Add(implementation))
                    continue;
                if (type.BaseType?.FindImplementationForInterfaceMember(member) is { } based
                    && SymbolEqualityComparer.Default.Equals(based, implementation))
                    continue;
                inherited.Add(new Inherited(implementation, DeclarationOf(implementation), member));
            }
        }

        // A default may call a PRIVATE member of its interface, which no class implements and the
        // language hands to none: it travels with the defaults that call it, directly or through
        // another helper, and only with those. Copying every helper put members nothing calls into
        // every twin, and one could collide with a name the class uses (found in review, #418).
        var pending = new Queue<MemberDeclarationSyntax>(inherited.Select(member => member.Declaration).OfType<MemberDeclarationSyntax>());
        while (pending.Count > 0)
        {
            var declaration = pending.Dequeue();
            if (!compilation.ContainsSyntaxTree(declaration.SyntaxTree)) continue;
            var model = compilation.GetSemanticModel(declaration.SyntaxTree);
            foreach (var name in RunTimeNames(declaration, model))
            {
                if (model.GetSymbolInfo(name).Symbol is not { IsStatic: false, IsAbstract: false } helper
                    || helper.DeclaredAccessibility != Accessibility.Private
                    || helper.ContainingType?.TypeKind != TypeKind.Interface
                    || helper is not (IPropertySymbol or IMethodSymbol { MethodKind: MethodKind.Ordinary })
                    || !seen.Add(helper))
                    continue;
                var helperDeclaration = DeclarationOf(helper);
                inherited.Add(new Inherited(helper, helperDeclaration, Contract: null));
                if (helperDeclaration is not null) pending.Enqueue(helperDeclaration);
            }
        }
        return inherited;
    }

    /// <summary>
    /// The names a declaration reaches when it RUNS: every name in it, except one inside
    /// <c>nameof(…)</c>, which the compiler turns into text, and one in an attribute, which is metadata
    /// (found in review, #418). <c>Show() => nameof(Hidden)</c> copied the private <c>Hidden()</c> into
    /// the twin and took its name from a field of the class, and a static named only there read as
    /// one the default calls.
    /// </summary>
    private static IEnumerable<SimpleNameSyntax> RunTimeNames(MemberDeclarationSyntax declaration, SemanticModel model) =>
        declaration.DescendantNodes(node => node is not AttributeListSyntax)
            .OfType<SimpleNameSyntax>()
            .Where(name => !name.Ancestors().OfType<InvocationExpressionSyntax>().Any(invocation =>
                invocation.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" }
                && model.GetOperation(invocation) is Microsoft.CodeAnalysis.Operations.INameOfOperation));

    private static MemberDeclarationSyntax? DeclarationOf(ISymbol member) =>
        member.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as MemberDeclarationSyntax;

    /// <summary>
    /// Whether the runtime carries <paramref name="contract"/>'s defaults (the runtime's
    /// <c>interface-defaults.ts</c>): a public interface of one of the assemblies the runtime provides,
    /// which an app compiles against as METADATA, so the twin of an app's theme, language or
    /// completion provider delegates to it. <c>VocabularyInterfaceDefaultsTests</c> fails when a
    /// default of theirs has no copy there. The assembly is asked, not only the namespace: another
    /// assembly can declare an interface in the vocabulary's namespace, and its twin delegated to a
    /// copy the runtime does not have (found in review, #418).
    /// </summary>
    public static bool RuntimeCarries(INamedTypeSymbol contract) =>
        contract.DeclaredAccessibility == Accessibility.Public
        && contract.ContainingAssembly?.Name is { } assembly
        && Services.RuntimeProvidedTypeScanner.RuntimeAssemblies.Contains(assembly)
        && Services.RuntimeProvidedTypeScanner.IsRuntimeProvidedNamespace(contract.ContainingNamespace?.ToDisplayString() ?? "");

    /// <summary>
    /// The member a twin delegates a vocabulary default with: its name, the parameters of a method,
    /// and the call to the runtime's copy, which takes the instance first
    /// (<c>ICodeLanguage.rules(this)</c>, <c>IAppTheme.code(this, kind)</c>).
    /// </summary>
    public static (string Name, IReadOnlyList<string> Parameters, string Call) Delegation(ISymbol implementation)
    {
        var name = implementation.Name.ToCamelCase();
        IReadOnlyList<string> parameters = implementation is IMethodSymbol method
            ? method.Parameters.Select(parameter => parameter.Name.ToJsIdentifier()).ToList()
            : [];
        var call = $"{implementation.ContainingType.Name}.{name}(this{string.Concat(parameters.Select(parameter => ", " + parameter))})";
        return (name, parameters, call);
    }

    /// <summary>
    /// The static member of an interface a default's body reaches, if any. An interface erases, so a
    /// static of one has no JavaScript home, and the default would call a name nothing defines (found
    /// in review, #418). A constant is inlined where it is read, so it needs none.
    /// </summary>
    public static ISymbol? InterfaceStaticIn(MemberDeclarationSyntax declaration, SemanticModel model) =>
        RunTimeNames(declaration, model)
            .Select(name => model.GetSymbolInfo(name).Symbol)
            .FirstOrDefault(symbol => symbol is { IsStatic: true, ContainingType.TypeKind: TypeKind.Interface }
                and not ITypeSymbol and not IFieldSymbol { IsConst: true });

    /// <summary>The error for a default that reaches a static member of an interface.</summary>
    public static string Homeless(INamedTypeSymbol type, ISymbol implementation, ISymbol reached) =>
        $"{type.Name} relies on the default {implementation.ContainingType.Name}.{implementation.Name}, which uses "
        + $"{reached.ContainingType.Name}.{reached.Name}, a static member of an interface, and an interface has no "
        + $"JavaScript form to hold it, so the default cannot be written into {type.Name}'s twin. Declare "
        + $"{implementation.Name} in {type.Name}, or keep {type.Name} out of client code.";

    /// <summary>
    /// The error for a default indexer: a twin has no form for an indexer, a class's own included
    /// (#427), so the default would reach the browser's class as nothing (found in review, #418).
    /// </summary>
    public static string NoIndexer(INamedTypeSymbol type, ISymbol implementation) =>
        $"{type.Name} relies on the default indexer of {implementation.ContainingType.Name}, and a twin has no form "
        + $"for an indexer yet (#427), so the browser's {type.Name} would have none. Keep {type.Name} out of client code.";

    /// <summary>
    /// The error for a default whose body cannot be written and that the runtime does not carry:
    /// its interface is compiled into a referenced assembly outside the vocabulary, so the
    /// transpiler has its signature and not its code, and the twin would answer the member with
    /// undefined. An error, since the SDK's own defaults reach every app through the runtime and
    /// what is left is an interface the developer can act on: measured, the documentation site, the
    /// dashboard sample and the app template raise it nowhere.
    /// </summary>
    public static string Unreadable(INamedTypeSymbol type, ISymbol implementation) =>
        $"{type.Name} relies on the default {implementation.ContainingType.Name}.{implementation.Name}, "
        + $"whose body is compiled into {implementation.ContainingAssembly?.Name ?? "a referenced assembly"} "
        + $"and cannot be transpiled, so the browser's {type.Name} would have no {implementation.Name}. "
        + $"Declare {implementation.Name} in {type.Name}, or keep {type.Name} out of client code.";
}
