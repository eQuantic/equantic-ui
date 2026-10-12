using System.Text.RegularExpressions;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// What ONE module names of the projects the app references: the layer every emitter asks, beside the
/// app's scan, whether a name is a sibling module. It joins what those projects declare
/// (<see cref="ReferencedModules"/>) with what the module's own symbols name
/// (<see cref="NamedSourceTypes"/>), so a referenced project's name is importable only where a symbol
/// named that project's type. A name a file merely mentions, bound to a package's type or to nothing,
/// never imports a referenced project's module of that name.
/// </summary>
internal sealed class ReferencedNames
{
    /// <summary>A module of a build with no referenced project, or one the build has no model for:
    /// it names none.</summary>
    public static readonly ReferencedNames None = new(null, null);

    private static readonly Regex SiblingImport = new(
        """^\s*(?:import|export)\b[^;'"]*?\bfrom\s*["']\./([^"']+)["']""",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private readonly ReferencedModules? _modules;
    private readonly NamedSourceTypes? _named;

    public ReferencedNames(ReferencedModules? modules, NamedSourceTypes? named)
    {
        _modules = modules;
        _named = named;
    }

    /// <summary>Whether <paramref name="name"/> is a module of a referenced project that a symbol of
    /// this module names.</summary>
    public bool IsModule(string name) =>
        _modules is not null && _named is not null && _modules.IsModule(name, _named.DeclaredIn(name));

    /// <summary>Whether <paramref name="name"/> is a referenced project's nested type the build
    /// writes no module for, which a module that names it reports where it names it (EQ2010).</summary>
    public bool IsKeptOut(string name) =>
        _modules is not null && _named is not null && _modules.IsKeptOut(name, _named.DeclaredIn(name));

    /// <summary>
    /// The referenced projects' types the written module imports: each sibling import of
    /// <paramref name="typeScript"/> a symbol of the module placed in a referenced project, with the
    /// file that declares it. Read from the module's own text, so it is what the bundler will ask
    /// for, whichever emitter wrote the import.
    /// </summary>
    public IReadOnlyList<ReferencedType> ImportedBy(string typeScript)
    {
        if (_modules is null || _named is null || string.IsNullOrEmpty(typeScript)) return [];
        var imported = new List<ReferencedType>();
        foreach (Match import in SiblingImport.Matches(typeScript))
        {
            foreach (var declaration in _named.Declarations(import.Groups[1].Value))
                if (_modules.ProjectOf(declaration.File) is not null)
                    imported.Add(declaration);
        }
        return imported;
    }
}
