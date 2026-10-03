using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// The name a type written in a base list is emitted under, after <c>extends</c> and in the import
/// beside it: its twin's, which is the class's own simple name. Read from the SYMBOL when the model
/// binds it, so a namespace, an alias or <c>global::</c> in the spelling never reaches the module,
/// and from the rightmost simple name the syntax writes when there is no model. Generic arguments
/// are erased either way. The base was copied as written, so
/// <c>class Tag : eQuantic.UI.Web.HtmlElement</c> extended a name nothing defines (#479).
/// </summary>
internal static class TypeSyntaxExtensions
{
    internal static string TwinTypeName(this TypeSyntax type, SemanticModel? model) =>
        model?.GetSymbolInfo(type).Symbol is INamedTypeSymbol symbol ? symbol.Name : SimpleName(type);

    private static string SimpleName(TypeSyntax type) => type switch
    {
        QualifiedNameSyntax qualified => SimpleName(qualified.Right),
        AliasQualifiedNameSyntax alias => SimpleName(alias.Name),
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        _ => type.ToString(),
    };
}
