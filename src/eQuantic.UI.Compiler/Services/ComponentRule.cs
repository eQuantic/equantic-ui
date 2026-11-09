using eQuantic.UI.Compiler.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// Whether a class is a component, the one rule the parser and the dependency scan read (#1170). Each kept
/// its own: where a base did not bind, the parser took a non-private instance <c>Build</c> or
/// <c>Render</c> for a component, and the scan any <c>Build</c>, <c>Render</c> or <c>CreateState</c>, private
/// and static included, so a class written as a plain class was filed as a component.
/// </summary>
internal static class ComponentRule
{
    /// <summary>
    /// With the model, the chain decides wherever it binds: a builder whose last step is <c>Build</c> derives
    /// from nothing a component does, and was written as one (#1164). The name of a method is the answer
    /// only where a base on the chain does not bind, and the name of the base as well where there is no
    /// model at all.
    /// </summary>
    /// <param name="declaration">The class.</param>
    /// <param name="symbol">Its type, or null where nothing can answer for it, which then reads the first base
    /// the class writes by its simple name, an alias read through its directive.</param>
    internal static bool Is(ClassDeclarationSyntax declaration, INamedTypeSymbol? symbol)
    {
        var writtenBase = symbol is null ? declaration.BaseList?.Types.FirstOrDefault()?.Type.TwinTypeName(null) : null;
        var hasEntryPoint = declaration.Members.OfType<MethodDeclarationSyntax>()
            .Any(method => IsBuildEntryPoint(method, "Render", "Build"));
        return symbol is not null
            ? symbol.IsUiComponent() || hasEntryPoint && !symbol.BasesBind()
            : hasEntryPoint || writtenBase is "StatefulComponent" or "StatelessComponent" or "ComponentState"
                or "HtmlElement" or "UiComponent" or "Flex" or "Container" or "Stack";
    }

    /// <summary>
    /// Whether this method IS the component's entry point, rather than merely sharing its name.
    /// <para>
    /// Matching on the name alone made any method called <c>Render</c> the build method — so a page
    /// with a <c>private static VisualNode Render(DocBlock block, IAppTheme theme)</c> helper had its
    /// Build body REPLACED by the helper's, and the helper itself dropped. The emitted
    /// <c>build(_context)</c> then referenced the helper's parameters, which do not exist in that
    /// scope, and the page died at hydration on "block is not defined" — while SSR rendered
    /// correct HTML and answered 200, so a smoke test saw a perfect page.
    /// </para>
    /// <para>
    /// The entry point overrides a public abstract, so it is never static and never private. A
    /// helper is one or the other, which is exactly what tells them apart without a semantic model.
    /// </para>
    /// </summary>
    internal static bool IsBuildEntryPoint(MethodDeclarationSyntax method, params string[] names) =>
        names.Contains(method.Identifier.ValueText)
        && !method.Modifiers.Any(SyntaxKind.StaticKeyword)
        && !method.Modifiers.Any(SyntaxKind.PrivateKeyword);
}
