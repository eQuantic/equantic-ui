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
    /// How an assembly of the reference list is read; null reads each file anew, which is right for a
    /// process that builds once. A reference holds its assembly in native memory until it is
    /// finalized, so a host that runs many builds (a test suite, a design tool) hands a reader that
    /// keeps one reference per assembly.
    /// </summary>
    public Func<string, Microsoft.CodeAnalysis.MetadataReference>? ReferenceReader { get; init; }

    /// <summary>What each module's source map carries.</summary>
    public SourceMapMode SourceMaps { get; init; } = SourceMapMode.Full;
}
