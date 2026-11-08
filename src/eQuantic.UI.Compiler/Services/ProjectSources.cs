using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// What the C# compiler compiled for one project: its evaluated <c>Compile</c> items and the symbols its
/// <c>DefineConstants</c> defined (#828). eqc walked a project's directory and parsed every file with no
/// symbol, so a file the project removed became a module, a file it linked from outside was never read,
/// and <c>#if DEBUG</c> took the branch neither configuration builds. The SDK hands eqc what MSBuild
/// evaluated instead, for the app and for each project it references, and the one rule reads both.
/// </summary>
internal sealed class ProjectSources
{
    private ProjectSources(string projectFile, IReadOnlyList<string> files, IReadOnlyList<string> ownFiles,
        IReadOnlyList<string> symbols)
    {
        ProjectFile = projectFile;
        Files = files;
        OwnFiles = ownFiles;
        Symbols = symbols;
        ParseOptions = ParseDefaults.Options.WithPreprocessorSymbols(symbols);
        _files = new HashSet<string>(files, StringComparer.Ordinal);
    }

    private readonly HashSet<string> _files;

    /// <summary>The project file's full path.</summary>
    public string ProjectFile { get; }

    /// <summary>The C# files the project compiled, by their full paths, in the order MSBuild listed them:
    /// its generated global usings among them, and a file it links from outside its directory.</summary>
    public IReadOnlyList<string> Files { get; }

    /// <summary>The project's own source among <see cref="Files"/>: what its directory holds outside <c>obj</c>,
    /// and what it links from outside. A file its build generated under <c>obj</c> and C# a package hands it
    /// are compiled and never written as a module of the app, which is what the walk, that entered neither,
    /// always did.</summary>
    public IReadOnlyList<string> OwnFiles { get; }

    /// <summary>The preprocessor symbols the project defined (<c>DEBUG</c>, <c>NET10_0_OR_GREATER</c>…).</summary>
    public IReadOnlyList<string> Symbols { get; }

    /// <summary>What a file of the project is parsed with: eqc's language version and the project's symbols.</summary>
    public CSharpParseOptions ParseOptions { get; }

    /// <summary>Whether <paramref name="file"/> is one the project compiled.</summary>
    public bool Compiles(string file) => _files.Contains(Path.GetFullPath(file));

    /// <summary>The symbols a <c>DefineConstants</c> value defines: separated by <c>;</c> or <c>,</c>, as csc
    /// reads them, with the space around each dropped and an empty one none.</summary>
    public static IReadOnlyList<string> SymbolsOf(string? defineConstants) =>
        (defineConstants ?? "").Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal).ToList();

    /// <summary>The line that opens the app's section of a sources file.</summary>
    internal const string AppHeader = "app: ";

    /// <summary>The line that opens a referenced project's section.</summary>
    internal const string ProjectHeader = "project: ";

    /// <summary>The line after a header: the project's <c>DefineConstants</c>, as it evaluates them.</summary>
    internal const string SymbolsHeader = "symbols: ";

    /// <summary>What opens the line of a file the project compiles that is not its own source: one its build
    /// generated under <c>obj</c>, or one a package hands it.</summary>
    internal const string CompiledOnly = "compiled: ";

    /// <summary>
    /// The sections a <c>--sources</c> file holds, as the SDK writes it: for the app and for each project it
    /// references, a header naming the project file (<c>app: </c> or <c>project: </c>), then its symbols
    /// (<c>symbols: </c> and the <c>DefineConstants</c>, a line kept even when they are empty), then the full
    /// path of each C# file it compiled, one a line, after <c>compiled: </c> where it is not the project's own
    /// source. A file handed that does not hold is never read as fewer projects: it throws, and the build
    /// stops on it (EQ0001), as a projects list does. A listed file that is not on disk is passed over: a
    /// target that writes it before the C# compiler runs has not run when the compile target is run alone, as
    /// hot reload runs it.
    /// </summary>
    /// <returns>The app's sources, and the referenced projects' by their project files' full paths.</returns>
    public static (ProjectSources? App, IReadOnlyDictionary<string, ProjectSources> Projects) ReadAll(string? sourcesFile)
    {
        var projects = new Dictionary<string, ProjectSources>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(sourcesFile)) return (null, projects);
        if (!File.Exists(sourcesFile))
            throw new FileNotFoundException($"the list of sources is not there: '{sourcesFile}'", sourcesFile);

        ProjectSources? app = null;
        var lines = File.ReadAllLines(sourcesFile);
        for (var at = 0; at < lines.Length;)
        {
            var header = lines[at];
            if (header.Length == 0) { at++; continue; }
            var isApp = header.StartsWith(AppHeader, StringComparison.Ordinal);
            if (!isApp && !header.StartsWith(ProjectHeader, StringComparison.Ordinal))
                throw new InvalidDataException($"line {at + 1} opens no project's sources: '{header}'");
            var projectFile = Path.GetFullPath(header[(isApp ? AppHeader : ProjectHeader).Length..]);
            if (at + 1 >= lines.Length || !lines[at + 1].StartsWith(SymbolsHeader, StringComparison.Ordinal))
                throw new InvalidDataException($"line {at + 2} is not the symbols of '{projectFile}'");
            var symbols = SymbolsOf(lines[at + 1][SymbolsHeader.Length..]);

            var files = new List<string>();
            var own = new List<string>();
            for (at += 2; at < lines.Length && !lines[at].StartsWith(AppHeader, StringComparison.Ordinal)
                    && !lines[at].StartsWith(ProjectHeader, StringComparison.Ordinal); at++)
            {
                if (lines[at].Length == 0) continue;
                var compiledOnly = lines[at].StartsWith(CompiledOnly, StringComparison.Ordinal);
                var file = Path.GetFullPath(compiledOnly ? lines[at][CompiledOnly.Length..] : lines[at]);
                if (!File.Exists(file) || files.Contains(file, StringComparer.Ordinal)) continue;
                files.Add(file);
                if (!compiledOnly) own.Add(file);
            }

            var sources = new ProjectSources(projectFile, files, own, symbols);
            if (isApp)
            {
                if (app is not null) throw new InvalidDataException($"the list of sources names two apps: '{sourcesFile}'");
                app = sources;
            }
            else projects[projectFile] = sources;
        }
        return (app, projects);
    }
}
