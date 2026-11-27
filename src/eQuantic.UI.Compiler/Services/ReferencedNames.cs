using System.Text.RegularExpressions;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// What ONE module names of the projects the app references: the layer every emitter asks, beside the
/// app's scan, whether a name is a sibling module. It joins what those projects declare
/// (<see cref="ReferencedModules"/>) with what the module's own symbols name
/// (<see cref="NamedSourceTypes"/>), so a referenced project's name is importable only where a symbol
/// named that project's type. A name a file merely mentions, bound to a package's type or to nothing,
/// never imports a referenced project's module of that name. Which type a written import means is the
/// writers' to say (<see cref="CodeGen.WrittenTwins"/>, #830).
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

    /// <summary>A module's import or re-export statement, which names a sibling and uses none.</summary>
    private static readonly Regex ImportStatement = new(
        """^\s*(?:import|export)\b[^;'"]*?\bfrom\s*["'][^"']*["'];?""",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    /// <summary>
    /// The referenced projects' types the written module imports: each sibling import of
    /// <paramref name="typeScript"/>, with the type it means and the file that declares it. Read from the
    /// module's own text, so it is what the bundler will ask for, whichever emitter wrote the import.
    /// <para>
    /// Which type a <c>./Order</c> means is said by whoever wrote <c>Order</c> (<paramref name="written"/>,
    /// #830). Where every use of the name in the module is a write on record, the import means the types
    /// those writes were of and nothing else: the app's own <c>Order</c> beside a cast of a null to a
    /// referenced project's <c>Order</c>, which writes nothing, reaches the app's alone. Where a use is not
    /// on record, some writer wrote the name and said nothing, and the import means every type of the
    /// name a symbol of the module names (<see cref="NamedSourceTypes"/>), as it did before: a writer that
    /// records nothing costs precision, never a type the module writes.
    /// </para>
    /// </summary>
    public IReadOnlyList<ReferencedType> ImportedBy(string typeScript, CodeGen.WrittenTwins? written = null)
    {
        if (_modules is null || _named is null || string.IsNullOrEmpty(typeScript)) return [];
        var code = CodeOf(typeScript);
        var imported = new List<ReferencedType>();
        foreach (Match import in SiblingImport.Matches(typeScript))
        {
            foreach (var declaration in Meant(import.Groups[1].Value, code, written))
                if (_modules.ProjectOf(declaration.File) is not null)
                    imported.Add(declaration);
        }
        return imported;
    }

    /// <summary>The sibling imports of <paramref name="typeScript"/> whose every use is not a write on record
    /// (<see cref="ComponentCompiler"/>'s <c>ImportsOnTheWalk</c>), in the order the module imports them.</summary>
    internal static IReadOnlyList<string> OnTheWalk(string typeScript, CodeGen.WrittenTwins? written)
    {
        if (string.IsNullOrEmpty(typeScript)) return [];
        var code = CodeOf(typeScript);
        return SiblingImport.Matches(typeScript).Select(import => import.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Where(name => !EveryUseIsOnRecord(name, code, written))
            .ToList();
    }

    /// <summary>The module's text without its import and re-export statements, which use no name.</summary>
    internal static string CodeOf(string typeScript) => ImportStatement.Replace(typeScript, "");

    /// <summary>Whether every use of <paramref name="name"/> in <paramref name="code"/> is a write
    /// <paramref name="written"/> holds (#830).</summary>
    internal static bool EveryUseIsOnRecord(string name, string code, CodeGen.WrittenTwins? written) =>
        written?.Under(name) is { Count: > 0 } writes && writes.Count == CodeGen.WrittenTwins.Uses(code, name);

    private IEnumerable<ReferencedType> Meant(string name, string code, CodeGen.WrittenTwins? written)
    {
        if (!EveryUseIsOnRecord(name, code, written)) return _named!.Declarations(name);
        return written!.Under(name).Distinct<Microsoft.CodeAnalysis.INamedTypeSymbol>(Microsoft.CodeAnalysis.SymbolEqualityComparer.Default)
            .SelectMany(type => type.ReadDeclarations()
                .Where(reference => !string.IsNullOrEmpty(reference.SyntaxTree.FilePath))
                .Select(reference => new ReferencedType(name, NamedSourceTypes.ClrName(type), reference.SyntaxTree.FilePath)))
            .Distinct();
    }
}
