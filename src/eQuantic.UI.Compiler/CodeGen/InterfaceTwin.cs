using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Whether an interface declared in source gets a twin, a module holding <c>export interface</c> that a type parameter's
/// constraint can name (#1878). One answer, read from the declaration's syntax alone, by the parser that writes the
/// module, the dependency resolver that says it exists and the constraint namers that import it, so the three never
/// disagree, with or without a model.
/// <para>
/// The twin can be written when the interface declares no type parameter and stands in no generic type, each member is
/// an instance property or method with no body, no member is static, an event, an indexer or a type, it extends no
/// interface, it has one declaration, and neither it nor a type it stands in is marked <c>[ServerOnly]</c> or
/// <c>[RuntimeProvided]</c>. Each fence is a shape the twin would have to say something TypeScript's <c>interface</c>
/// cannot (a static member, a body), or a base whose twin this rule would have to find in another file, and leaves a
/// constrained type parameter unconstrained, as every constraint was before #1854.
/// </para>
/// </summary>
internal static class InterfaceTwin
{
    /// <summary>Whether <paramref name="declaration"/> gets a twin.</summary>
    public static bool CanBeWritten(InterfaceDeclarationSyntax declaration) =>
        declaration.TypeParameterList is null
        && !declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
        && declaration.BaseList is null
        && !PlainClassModule.IsServerOnlyDeclaration(declaration)
        && !declaration.AttributeLists.SelectMany(list => list.Attributes).Any(attribute => attribute.IsNamed("RuntimeProvided"))
        && PlainClassModule.OwnersCross(declaration)
        && !InAGenericType(declaration)
        && declaration.Members.All(IsWritableMember);

    /// <summary>Whether <paramref name="symbol"/>, an interface, gets a twin: declared in source, once, by a declaration
    /// that <see cref="CanBeWritten(InterfaceDeclarationSyntax)"/>.</summary>
    public static bool CanBeWritten(INamedTypeSymbol symbol) =>
        symbol.TypeKind == TypeKind.Interface
        && symbol.ReadDeclarations() is [var only]
        && only.GetSyntax() is InterfaceDeclarationSyntax declaration
        && CanBeWritten(declaration);

    /// <summary>The name a reference writes for <paramref name="symbol"/>'s twin, its owners' and its own (#584), or null
    /// when it has none. A type annotation writes an interface as <c>any</c>, so a constraint names its twin by this.</summary>
    public static string? NameOf(INamedTypeSymbol symbol) =>
        CanBeWritten(symbol) && symbol.ReadDeclarations()[0].GetSyntax() is InterfaceDeclarationSyntax declaration
            ? declaration.TwinTypeName()
            : null;

    private static bool InAGenericType(SyntaxNode declaration)
    {
        for (var owner = declaration.Parent; owner is not null; owner = owner.Parent)
            if (owner is TypeDeclarationSyntax { TypeParameterList: not null }) return true;
        return false;
    }

    private static bool IsWritableMember(MemberDeclarationSyntax member) => member switch
    {
        PropertyDeclarationSyntax property =>
            !property.Modifiers.Any(SyntaxKind.StaticKeyword)
            && property.ExpressionBody is null
            && property.Initializer is null
            && property.AccessorList is { } accessors
            && accessors.Accessors.All(accessor => accessor.Body is null && accessor.ExpressionBody is null),
        MethodDeclarationSyntax method =>
            !method.Modifiers.Any(SyntaxKind.StaticKeyword)
            && method.Body is null
            && method.ExpressionBody is null,
        _ => false,
    };
}
