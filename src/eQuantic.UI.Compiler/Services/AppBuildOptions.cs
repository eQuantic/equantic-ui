namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// What one build of an app is handed: where its sources are, where its modules go, and the files the
/// SDK wrote for it from what MSBuild resolved. The CLI fills it from its arguments and a test fills it
/// from a project it wrote to a temporary directory, so both run the same <see cref="AppBuild"/>.
/// </summary>
public sealed class AppBuildOptions
{
    /// <summary>The transpiled roots, the app's own FIRST: the SDK passes the project's directory and
    /// then the shared libraries' sources. Order matters: generated files are the first directory's,
    /// and the entry-point rule reads the same position.</summary>
    public required IReadOnlyList<string> SourceDirectories { get; init; }

    /// <summary>Where the bundled modules and the string catalogs are written.</summary>
    public required string OutputDirectory { get; init; }

    /// <summary>The embedded bun. Without it a build that has an entry point fails, as bundling is what
    /// produces a page's JavaScript.</summary>
    public string? BunPath { get; init; }

    /// <summary>A newline-delimited list of the assemblies MSBuild resolved for the project
    /// (<c>@(ReferencePathWithRefAssemblies)</c>), which makes the semantic model the one csc compiles
    /// against.</summary>
    public string? ReferencesFile { get; init; }

    /// <summary>Newline-delimited directories whose sources join the compilation for RESOLUTION only
    /// (an icon pack's constants, inlined where they are used). Nothing in them is transpiled.</summary>
    public string? ReferenceSourcesFile { get; init; }

    /// <summary>The source-generator output directory for the configuration being built. Null lets
    /// the build find it by sweeping <c>obj</c>, which takes every configuration's.</summary>
    public string? GeneratedDirectory { get; init; }

    /// <summary>
    /// The projects the app references with a <c>ProjectReference</c>, directly or through another:
    /// one a line, the assembly MSBuild resolved for it, a <c>|</c>, and its project file. The SDK
    /// writes it from <c>@(ReferencePathWithRefAssemblies)</c>, so the developer sets nothing. Each
    /// project's source is compiled in place of its assembly, and a type of it that browser code
    /// reaches becomes a module of the bundle (#624).
    /// </summary>
    public string? ProjectsFile { get; init; }

    /// <summary>
    /// The app's assembly name as its project evaluates it (<c>$(AssemblyName)</c>), which a project
    /// file may set apart from its own name. The app's compilation takes it, because it is the name
    /// another assembly knows the app by: a referenced project's <c>InternalsVisibleTo</c> opens its
    /// internal types to that name and to no other. Null takes the project file's name, which is
    /// what MSBuild itself defaults the property to.
    /// </summary>
    public string? AssemblyName { get; init; }

    /// <summary>
    /// How an assembly of the reference list is read; null reads each file anew, which is right for a
    /// process that builds once. A reference holds its assembly in native memory until it is
    /// finalized, so a host that runs many builds (a test suite, a design tool) hands a reader that
    /// keeps one reference per assembly.
    /// </summary>
    public Func<string, Microsoft.CodeAnalysis.MetadataReference>? ReferenceReader { get; init; }

    /// <summary>What each module's source map carries.</summary>
    public SourceMapMode SourceMaps { get; init; } = SourceMapMode.Full;

    /// <summary>The options that are followed by a value.</summary>
    private static readonly HashSet<string> ValueOptions = new(StringComparer.Ordinal)
    {
        "--refs", "--ref-sources", "--generated", "--projects", "--assembly-name", "--bun", "--source-maps",
    };

    /// <summary>
    /// The arguments a command line holds: the ones it was given, or, when its one argument is
    /// <c>@&lt;file&gt;</c>, the ones that file holds, one a line. The SDK hands eqc a file and never
    /// writes a value of the project into the command, because a shell reads the command first:
    /// <c>Cash$Flow</c> reached eqc as <c>Cash</c>, and a backtick ran as a command (#836). A line is
    /// an argument whatever characters it holds, and an empty line is none.
    /// </summary>
    /// <exception cref="IOException">The file the command line names cannot be read.</exception>
    public static IReadOnlyList<string> ReadArguments(IReadOnlyList<string> commandLine) =>
        commandLine is [var only] && only.StartsWith('@')
            ? File.ReadAllLines(only[1..]).Where(line => line.Length > 0).ToList()
            : commandLine;

    /// <summary>
    /// The options the arguments hold, as eqc reads them: the source roots, separated by a
    /// semicolon, and the output folder first, then each option followed by its value. The CLI's own
    /// <c>--watch</c> is left to it.
    /// </summary>
    /// <exception cref="ArgumentException">There are not the two folders first, an option has no
    /// value after it, or <c>--source-maps</c> names no mode.</exception>
    public static AppBuildOptions FromArguments(IReadOnlyList<string> arguments)
    {
        if (arguments.Count < 2 || arguments[0].StartsWith("--", StringComparison.Ordinal)
            || arguments[1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException("eqc is handed the source roots and the output folder first.");

        // Read in order, an option and then its value: a value that reads like an option (a folder
        // named `--refs`) is the value it stands as. A flag of the CLI's own, `--watch`, is passed by.
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var at = 2; at < arguments.Count; at++)
        {
            if (!ValueOptions.Contains(arguments[at])) continue;
            if (at + 1 >= arguments.Count) throw new ArgumentException($"{arguments[at]} is handed no value.");
            values[arguments[at]] = arguments[at + 1];
            at++;
        }
        string? Value(string option) => values.GetValueOrDefault(option);

        // --source-maps full|external|none (#352): what each module's map carries. `full` links a
        // map with the C# inside it, `external` writes one without the C# and without the link,
        // `none` writes none. The SDK passes full in Debug and none everywhere else; a direct run
        // keeps full.
        var sourceMaps = Value("--source-maps")?.Trim() ?? "full";
        return new AppBuildOptions
        {
            // TrimEntries: a root written on several lines of a project file carries the line
            // breaks and the indentation around it, and an untrimmed root is a folder that is not
            // there, whose components are then never transpiled.
            SourceDirectories = arguments[0].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            OutputDirectory = arguments[1].Trim(),
            // --refs <file>: the assemblies MSBuild resolved for the project
            // (@(ReferencePathWithRefAssemblies)), one a line, so the semantic model resolves every
            // reference as csc did. Without it, base types stay unresolved and member calls degrade
            // to naive camel-casing (.Add to .add).
            ReferencesFile = Value("--refs"),
            // --ref-sources <file>: folders whose C# joins the compilation to resolve, and is never
            // transpiled. An icon pack's `static readonly IconGlyph` is inlined where it is used,
            // which needs its initializer, and metadata does not carry one.
            ReferenceSourcesFile = Value("--ref-sources"),
            // --generated <dir>: the source generators' output for the configuration being built,
            // which the SDK knows and eqc cannot: a project built in both configurations has a tree
            // under each, and sweeping obj would take every generated type twice.
            GeneratedDirectory = Value("--generated")?.Trim(),
            // --projects <file>: the projects the app references with a ProjectReference, directly
            // or through another, each as two lines, the assembly MSBuild resolved for it and its
            // project file. Their source is compiled in place of their assemblies (#624).
            ProjectsFile = Value("--projects")?.Trim(),
            // --assembly-name <name>: the app's assembly name as its project evaluates it, which a
            // project file may set apart from its own name, and which a referenced project's
            // InternalsVisibleTo names.
            AssemblyName = Value("--assembly-name")?.Trim(),
            BunPath = Value("--bun"),
            SourceMaps = sourceMaps.ToLowerInvariant() switch
            {
                "full" => SourceMapMode.Full,
                "external" => SourceMapMode.External,
                "none" => SourceMapMode.None,
                _ => throw new ArgumentException($"--source-maps takes full, external or none, not '{sourceMaps}'."),
            },
        };
    }
}
