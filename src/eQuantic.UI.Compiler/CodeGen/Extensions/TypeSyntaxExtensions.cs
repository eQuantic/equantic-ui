using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// The name a type written in a base list is emitted under, after <c>extends</c> and in the import
/// beside it: its twin's, which is the class's own simple name. Read from the SYMBOL when the model
/// binds it, so a namespace, an alias or <c>global::</c> in the spelling never reaches the module,
/// and from the syntax when there is no model: an alias the file declares through its directive,
/// then the rightmost simple name. Generic arguments are erased either way. The base was copied as
/// written, so <c>class Tag : eQuantic.UI.Web.HtmlElement</c> extended a name nothing defines (#479).
/// </summary>
internal static class TypeSyntaxExtensions
{
    internal static string TwinTypeName(this TypeSyntax type, SemanticModel? model) =>
        model?.GetSymbolInfo(type).Symbol is INamedTypeSymbol symbol ? symbol.TwinTypeName() : SimpleName(Unaliased(type));

    /// <summary>A declared type's twin name (<see cref="TypeSymbolExtensions.TwinTypeName(INamedTypeSymbol)"/>),
    /// read from the declarations that contain it, for a host with no model to ask.</summary>
    internal static string TwinTypeName(this BaseTypeDeclarationSyntax declaration) =>
        eQuantic.UI.TwinName.OfNested((declaration.Parent as BaseTypeDeclarationSyntax)?.TwinTypeName(),
            declaration.Identifier.ValueText);

    /// <summary>
    /// The type parameters a declared type's twin declares, each a legal JS identifier: what the class header of its
    /// module writes after the name, in TypeScript. The one list every kind of class reads, a component's, a plain
    /// class's, a static helper's, a record's and a struct's. Only the component path wrote it once, so a plain generic
    /// class named <c>T</c> in its members while its class declared none, which the runtime's <c>tsc</c> refuses
    /// (#1845).
    /// </summary>
    internal static IReadOnlyList<string> TwinTypeParameters(this TypeDeclarationSyntax declaration) =>
        declaration.TypeParameterList?.Parameters.Select(parameter => parameter.Identifier.ValueText.ToJsIdentifier()).ToList() ?? [];

    /// <summary>
    /// The type parameters as a class header DECLARES them, each with the constraints its <c>where</c> clause names that
    /// <paramref name="constraint"/> can name in TypeScript, joined: <c>T extends GestureRecognizer</c>. Written as
    /// names alone, a member call on a <c>T</c> constrained to a class was refused (<c>Property 'area' does not exist
    /// on type 'T'</c>), and so was an override answering <c>T</c> where its base answers the class (#1854). A
    /// constraint the writer has no name for (<c>class</c>, <c>new()</c>, an interface, a generic) leaves the parameter
    /// unconstrained, as it was. A nullable class constraint (<c>where T : ShapeBase?</c>) is named by its class, since
    /// the namer imports what it names and <c>ShapeBase | null</c> is no name to import. A member's annotation names the
    /// type by <see cref="TwinTypeParameters(TypeDeclarationSyntax)"/>, the names alone.
    /// </summary>
    internal static IReadOnlyList<string> TwinTypeParameters(this TypeDeclarationSyntax declaration, Func<TypeSyntax, string?> constraint) =>
        declaration.TypeParameterList?.Parameters.Select(parameter =>
        {
            var name = parameter.Identifier.ValueText.ToJsIdentifier();
            var bounds = declaration.ConstraintClauses
                .Where(clause => clause.Name.Identifier.ValueText == parameter.Identifier.ValueText)
                .SelectMany(clause => clause.Constraints.OfType<TypeConstraintSyntax>())
                .Select(bound => constraint(bound.Type is NullableTypeSyntax nullable ? nullable.ElementType : bound.Type))
                .OfType<string>()
                .ToList();
            return bounds.Count == 0 ? name : $"{name} extends {string.Join(" & ", bounds)}";
        }).ToList() ?? [];

    /// <summary>
    /// Without a model, an alias the file declares (<c>using UiBase = …StatelessComponent;</c>) is
    /// read through its directive, from the innermost namespace out to the file, the order C# looks
    /// in. Its own name is no twin's, and <c>extends UiBase</c> named a class no import brings. A
    /// global alias declared in another file is out of a single file's reach.
    /// </summary>
    private static TypeSyntax Unaliased(TypeSyntax type)
    {
        if (type is not IdentifierNameSyntax { Identifier.ValueText: var name }) return type;
        for (var scope = type.Parent; scope is not null; scope = scope.Parent)
        {
            var usings = scope switch
            {
                BaseNamespaceDeclarationSyntax space => space.Usings,
                CompilationUnitSyntax unit => unit.Usings,
                _ => default,
            };
            foreach (var directive in usings)
                if (directive.Alias?.Name.Identifier.ValueText == name && directive.NamespaceOrType is { } target)
                    return target;
        }
        return type;
    }

    private static string SimpleName(TypeSyntax type) => type switch
    {
        QualifiedNameSyntax qualified => SimpleName(qualified.Right),
        AliasQualifiedNameSyntax alias => SimpleName(alias.Name),
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        _ => type.ValueText(),
    };
}
