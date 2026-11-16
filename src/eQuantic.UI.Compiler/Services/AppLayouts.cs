using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// The layout the app declared, read from its source where the server reads it: the type argument of
/// <c>ui.UseLayout&lt;ConsoleLayout&gt;()</c>, which the browser loads by name as it loads a page, so its
/// module is an ENTRY POINT of the bundle wherever its file sits (#1129).
/// <para>
/// The developer writes the call once, in <c>Program.cs</c>, and the build reads the same line, as it
/// reads the app's service registrations (<see cref="ServiceRegistrations"/>, #1335): nothing on the
/// layout's type says it is one, and nothing has to. A marker on the type would be a second statement
/// of one fact, which drifts from the first the day the app changes its layout.
/// </para>
/// <para>
/// The call is bound, never matched by its name alone: <c>UseLayout</c> of the SDK's options and no
/// other method of that name. A build without the SDK's server among its references reads none, which
/// is a build that serves no document and loads no layout.
/// </para>
/// </summary>
internal static class AppLayouts
{
    private const string Options = "eQuantic.UI.Server.UIOptions";
    private const string Method = "UseLayout";

    /// <summary>
    /// Every layout a <c>UseLayout</c> call names in <paramref name="compilations"/>' files that
    /// <paramref name="isSourceOfTheBuild"/> claims: the app's own and its referenced projects', where a
    /// library may declare the layout in an extension method the app calls.
    /// </summary>
    public static IReadOnlyList<INamedTypeSymbol> Read(IEnumerable<Compilation> compilations, Func<string, bool> isSourceOfTheBuild)
    {
        var layouts = new List<INamedTypeSymbol>();
        var read = new HashSet<SyntaxTree>();
        foreach (var compilation in compilations)
        {
            foreach (var tree in compilation.SyntaxTrees)
            {
                if (!read.Add(tree) || string.IsNullOrEmpty(tree.FilePath) || !isSourceOfTheBuild(tree.FilePath)) continue;
                SemanticModel? model = null;
                foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    // The name first, which costs nothing: a model is asked only about a call that could be one.
                    if (NameOf(invocation) is not GenericNameSyntax { Identifier.ValueText: Method }) continue;
                    model ??= compilation.GetSemanticModel(tree);
                    if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol
                        {
                            Name: Method, TypeArguments: [INamedTypeSymbol layout],
                        } method
                        || method.ContainingType?.ToDisplayString() != Options)
                        continue;
                    if (!layouts.Contains(layout.OriginalDefinition, SymbolEqualityComparer.Default))
                        layouts.Add(layout.OriginalDefinition);
                }
            }
        }
        return layouts;
    }

    /// <summary>What a compiled module is matched to a layout by: its namespace and its twin's name,
    /// which is the module's (<see cref="CompilationResult.Namespace"/>, <see cref="CompilationResult.ComponentName"/>).</summary>
    public static (string Namespace, string Module) ModuleOf(INamedTypeSymbol layout) =>
        (layout.ContainingNamespace is { IsGlobalNamespace: false } space ? space.ToDisplayString() : string.Empty,
            layout.TwinTypeName());

    /// <summary>The type a layout declared in a file of a referenced project is compiled as: a referenced
    /// project's type is a module only when the build reaches it (#624), and nothing imports a layout. Its
    /// declarations are read through <see cref="SymbolSources"/>, the one path that notes a file read (#1281).</summary>
    public static IEnumerable<ReferencedType> Declarations(INamedTypeSymbol layout) =>
        layout.ReadDeclarations()
            .Where(reference => !string.IsNullOrEmpty(reference.SyntaxTree.FilePath))
            .Select(reference => new ReferencedType(layout.TwinReference(), NamedSourceTypes.ClrName(layout),
                reference.SyntaxTree.FilePath));

    /// <summary>The method a call names, as written: <c>UseLayout&lt;T&gt;</c> in <c>ui.UseLayout&lt;T&gt;()</c>.</summary>
    private static SimpleNameSyntax? NameOf(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name,
        SimpleNameSyntax name => name,
        _ => null,
    };
}
