using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
        declaration.Parent is BaseTypeDeclarationSyntax owner
            ? owner.TwinTypeName() + "$" + declaration.Identifier.ValueText
            : declaration.Identifier.ValueText;

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
        _ => type.ToString(),
    };
}
