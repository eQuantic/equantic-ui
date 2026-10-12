using eQuantic.UI.Compiler.CodeGen.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// The catalogue id of every resource class of a build, decided ONCE over the app and the projects it
/// references.
/// <para>
/// A read of <c>Strings.Title</c> is written as the catalogue's id and the key, and the catalogs are
/// keyed by the same pair. The id is the class's simple name while that is unique, and
/// <see cref="ResourceClasses.IdFor"/> judged "unique" among the resource classes of the compilation
/// a file is compiled in. With a compilation per project that view differs by file: an app's
/// <c>Strings</c> and a shared library's <c>Strings</c> were each the only one in sight, both took
/// the id <c>Strings</c>, and one's <c>Title</c> overwrote the other's in every catalog (#624).
/// </para>
/// <para>
/// So the answer is the build's, and every file reads the same one. It is found by the class's
/// assembly and full name and never by its symbol: a file is compiled in a compilation of its own
/// (its tree replaces the project's), so the symbol one file holds does not compare equal to the one
/// another holds. An app with no referenced catalogue keeps the ids it had.
/// </para>
/// </summary>
internal sealed class ResourceCatalogIds
{
    private readonly Compilation _app;
    private Dictionary<(string Assembly, string FullName), string>? _ids;

    /// <param name="app">The app's compilation, which references each project's as a compilation.</param>
    public ResourceCatalogIds(Compilation app) => _app = app;

    /// <summary>
    /// The catalogue id of <paramref name="type"/>: its simple name while no other resource class of
    /// the build has it, its full name once two do, and its assembly's with it where two projects
    /// declare one full name. A class the build has no source for is a library's, read by its own
    /// build, and keeps its simple name.
    /// </summary>
    public string IdFor(INamedTypeSymbol type)
    {
        _ids ??= Decide();
        return _ids.TryGetValue(Key(type), out var id) ? id : type.Name;
    }

    private static (string Assembly, string FullName) Key(INamedTypeSymbol type) =>
        (type.ContainingAssembly?.Name ?? "", type.ToDisplayString());

    private Dictionary<(string Assembly, string FullName), string> Decide()
    {
        var declared = new List<INamedTypeSymbol>();
        foreach (var compilation in _app.ReferencedCompilations().Prepend(_app))
        {
            foreach (var tree in compilation.SyntaxTrees)
            {
                // Read from the declarations as written first: a project's every type is not bound
                // to learn which few are catalogues, and a resource class always declares a member
                // named ResourceManager.
                SemanticModel? model = null;
                foreach (var candidate in tree.GetRoot().DescendantNodes(node => node is not MemberDeclarationSyntax
                             || node is BaseNamespaceDeclarationSyntax or TypeDeclarationSyntax)
                             .OfType<ClassDeclarationSyntax>())
                {
                    if (!candidate.Members.OfType<PropertyDeclarationSyntax>()
                            .Any(property => property.Identifier.Text == "ResourceManager"))
                        continue;
                    model ??= compilation.GetSemanticModel(tree);
                    if (model.GetDeclaredSymbol(candidate) is INamedTypeSymbol type
                        && ResourceClasses.IsResourceClass(type)
                        && !declared.Contains(type, SymbolEqualityComparer.Default))
                        declared.Add(type);
                }
            }
        }

        var ids = new Dictionary<(string, string), string>();
        foreach (var sameName in declared.GroupBy(type => type.Name, StringComparer.Ordinal))
        {
            var types = sameName.ToList();
            foreach (var type in types)
            {
                var fullName = type.ToDisplayString();
                ids[Key(type)] = types.Count == 1
                    ? type.Name
                    : types.Count(other => other.ToDisplayString() == fullName) == 1
                        ? fullName
                        : $"{type.ContainingAssembly?.Name}:{fullName}";
            }
        }
        return ids;
    }
}
