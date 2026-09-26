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
    /// <summary>A default member a class takes: the implementation that applies to it, and its
    /// declaration where the compilation has one. An interface compiled into a referenced assembly
    /// has none, and its body cannot be written.</summary>
    internal readonly record struct Inherited(ISymbol Implementation, MemberDeclarationSyntax? Declaration);

    /// <summary>
    /// Every default member <paramref name="type"/> takes from an interface, in the order the
    /// interfaces and their members are declared. The implementation is the one the language picks
    /// for the class, so a derived interface's override of a base interface's member wins over the
    /// base's default. A member some base class already takes is left to that base: its twin
    /// carries the member, and the prototype chain hands it down.
    /// </summary>
    public static IReadOnlyList<Inherited> Of(INamedTypeSymbol type)
    {
        var inherited = new List<Inherited>();
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var contract in type.AllInterfaces)
        {
            if (type.BaseType is { } baseType
                && baseType.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default))
                continue;
            foreach (var member in contract.GetMembers())
            {
                if (member.IsStatic || member is not (IPropertySymbol or IMethodSymbol { MethodKind: MethodKind.Ordinary }))
                    continue;
                if (type.FindImplementationForInterfaceMember(member) is not { } implementation
                    || implementation.ContainingType.TypeKind != TypeKind.Interface
                    || implementation.IsAbstract
                    || !seen.Add(implementation))
                    continue;
                inherited.Add(new Inherited(implementation, DeclarationOf(implementation)));
            }
        }

        // A default may call a PRIVATE member of its interface, which no class implements and the
        // language hands to none: it travels with the defaults that need it.
        var owners = inherited.Select(member => member.Implementation.ContainingType)
            .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default).ToList();
        foreach (var owner in owners)
        {
            foreach (var helper in owner.GetMembers())
            {
                if (helper.IsStatic || helper.IsAbstract || helper.DeclaredAccessibility != Accessibility.Private
                    || helper is not (IPropertySymbol or IMethodSymbol { MethodKind: MethodKind.Ordinary })
                    || !seen.Add(helper))
                    continue;
                inherited.Add(new Inherited(helper, DeclarationOf(helper)));
            }
        }
        return inherited;
    }

    private static MemberDeclarationSyntax? DeclarationOf(ISymbol member) =>
        member.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as MemberDeclarationSyntax;

    /// <summary>
    /// Whether the runtime carries <paramref name="contract"/>'s defaults (the runtime's
    /// <c>interface-defaults.ts</c>): the vocabulary's interfaces, which an app compiles against as
    /// METADATA, so the twin of an app's theme, language or completion provider delegates to them.
    /// <c>VocabularyInterfaceDefaultsTests</c> fails when a default of theirs has no copy there.
    /// </summary>
    public static bool RuntimeCarries(INamedTypeSymbol contract) =>
        Services.RuntimeProvidedTypeScanner.IsRuntimeProvidedNamespace(contract.ContainingNamespace?.ToDisplayString() ?? "");

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
    /// The warning for a default whose body cannot be written and that the runtime does not carry:
    /// its interface is compiled into a referenced assembly outside the vocabulary, so the
    /// transpiler has its signature and not its code. A warning, as EQ1006 is one: the twin lacks a
    /// member C# has, which only matters when client code calls it.
    /// </summary>
    public static string Unreadable(INamedTypeSymbol type, ISymbol implementation) =>
        $"{type.Name} relies on the default {implementation.ContainingType.Name}.{implementation.Name}, "
        + $"whose body is compiled into {implementation.ContainingAssembly?.Name ?? "a referenced assembly"} "
        + $"and cannot be transpiled, so the browser's {type.Name} has no {implementation.Name}. "
        + $"Declare {implementation.Name} in {type.Name} if client code uses it.";
}
