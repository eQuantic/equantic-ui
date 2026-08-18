using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The scaffolded app, checked WITHOUT scaffolding it.
///
/// <para>
/// An SDK injects some usings into every file of a consumer's project (Sdk.props). A template that
/// also declares one of THOSE by hand is a duplicate — `CS1537: the using alias appeared
/// previously` — and the ONLY thing that ever noticed was the release gate, on a tag, after a full
/// build on three operating systems. That is a very slow way to learn that
/// `dotnet new equantic-app` no longer compiles.
/// </para>
/// <para>
/// WHICH usings those are is a question for the SDK, not a list kept here: the web SDK injects the
/// factory surface and the two aliases, the native one injects neither, and so a native screen
/// declares them itself and is right to. Reading the props is what keeps the guard honest the day
/// that changes — a list written down here failed the native template for doing the only thing that
/// compiles there.
/// </para>
/// </summary>
public class TemplateSourceTests
{
    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    private static string[] TemplateSources() =>
        Directory.GetFiles(Path.Combine(RepoRoot(), "src", "eQuantic.UI.Templates"), "*.cs",
                SearchOption.AllDirectories)
            // obj/ holds the STAMPED copy the pack produces — the same files, one build behind.
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToArray();

    /// <summary>
    /// The three a file could redeclare, each paired with the <c>Sdk.props</c> line that would be
    /// injecting it. The props are ASKED, per template, so this stays a description of what is true
    /// rather than a memory of what was.
    /// </summary>
    private static readonly (string Pattern, string Name, string Injection)[] ImplicitUsings =
    [
        (@"^\s*using\s+StatefulComponent\s*=", "StatefulComponent alias",
            @"Using\s+Include=""eQuantic\.UI\.Primitives\.StatefulComponent"""),
        (@"^\s*using\s+StatelessComponent\s*=", "StatelessComponent alias",
            @"Using\s+Include=""eQuantic\.UI\.Primitives\.StatelessComponent"""),
        (@"^\s*using\s+static\s+eQuantic\.UI\.Components\.UI\s*;", "the UI factory surface",
            @"Using\s+Include=""eQuantic\.UI\.Components\.UI"""),
    ];

    /// <summary>
    /// The SDK a project scaffolded from this template runs on. Only the native project template
    /// gets the native SDK; everything else here — the web app template and the item templates —
    /// is web.
    /// </summary>
    private static string SdkPropsFor(string path) =>
        Path.Combine(RepoRoot(), "src",
            path.Contains($"{Path.DirectorySeparatorChar}equantic-native{Path.DirectorySeparatorChar}")
                ? "eQuantic.UI.Sdk.Native"
                : "eQuantic.UI.Sdk",
            "Sdk", "Sdk.props");

    [Fact]
    public void No_template_source_redeclares_what_ITS_SDK_already_injects()
    {
        var offenders = new List<string>();
        foreach (var path in TemplateSources())
        {
            var text = File.ReadAllText(path);
            var props = File.ReadAllText(SdkPropsFor(path));
            foreach (var (pattern, name, injection) in ImplicitUsings)
            {
                // An SDK that does not inject it leaves the file to declare it, and the file must.
                if (!Regex.IsMatch(props, injection)) continue;

                if (Regex.IsMatch(text, pattern, RegexOptions.Multiline))
                    offenders.Add($"{Path.GetFileName(path)} declares {name}");
            }
        }

        offenders.Should().BeEmpty("an SDK that injects these makes a second declaration CS1537 "
            + "in the first project a newcomer scaffolds");
    }

    [Fact]
    public void The_templates_are_actually_there_to_check()
    {
        // A test that silently checks nothing is worse than no test: if the layout moves, this fails
        // instead of passing over an empty set.
        TemplateSources().Should().HaveCountGreaterThan(3);
    }

    private static string WebTemplateRoot() =>
        Path.Combine(RepoRoot(), "src", "eQuantic.UI.Templates", "templates", "equantic-app");

    /// <summary>What Sdk.props puts in every file of a consumer's project, without an import.</summary>
    private const string SdkUsings = """
        global using global::System;
        global using global::System.Collections.Generic;
        global using global::System.Linq;
        global using global::System.Threading.Tasks;
        global using global::eQuantic.UI.Components;
        global using global::eQuantic.UI.Primitives;
        global using static global::eQuantic.UI.Components.UI;
        global using StatefulComponent = global::eQuantic.UI.Primitives.StatefulComponent;
        global using StatelessComponent = global::eQuantic.UI.Primitives.StatelessComponent;
        """;

    private static readonly string[] WebShellNames = ["blank", "topnav", "dashboard"];

    public static TheoryData<string> WebShells()
    {
        var data = new TheoryData<string>();
        foreach (var shell in WebShellNames) data.Add(shell);
        return data;
    }

    /// <summary>
    /// Every <c>--shell</c> the web template offers, COMPILED — with the factory surface the
    /// GENERATOR writes for the app's own components, because a shell page calls
    /// <c>AppShell(…)</c> and <c>StatTile(…)</c> and nothing else would resolve them.
    /// <para>
    /// A template is source nobody builds until it lands on someone else's machine. The release
    /// gate does scaffold and build it — on a tag, on three operating systems, after a full pack —
    /// so without this a wrong overload is found a week later by a stranger.
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(WebShells))]
    public void Every_web_shell_compiles(string shell)
    {
        var root = WebTemplateRoot();
        var files = Directory.GetFiles(Path.Combine(root, ".shells", shell), "*.cs",
                SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(root, "Components"), "*.cs"))
            .Concat(Directory.GetFiles(Path.Combine(root, "Resources"), "*.cs"))
            .ToList();

        files.Should().NotBeEmpty($"--shell {shell} must actually have sources");

        var trees = files
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path))
            .Append(CSharpSyntaxTree.ParseText(SdkUsings))
            .ToList();

        var compilation = CSharpCompilation.Create($"WebShell_{shell}", trees, PlatformReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        // The app's own factories are GENERATED at build time — run the real generator, or every
        // `AppShell("/", …)` in a page reads as an undefined name.
        var driver = CSharpGeneratorDriver.Create(new eQuantic.UI.Generators.AppFactorySurfaceGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var withFactories, out _);
        _ = driver;

        var errors = withFactories.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => $"{Path.GetFileName(diagnostic.Location.SourceTree?.FilePath)}"
                + $"{diagnostic.Location.GetLineSpan().StartLinePosition}: {diagnostic.Id} {diagnostic.GetMessage()}")
            .ToList();

        errors.Should().BeEmpty($"`dotnet new equantic-app --shell {shell}` has to compile");
    }

    /// <summary>The choices the manifest offers and the folders on disk are the same set — a shape
    /// listed with no sources scaffolds an empty project, and sources with no choice are dead.</summary>
    [Fact]
    public void The_web_manifest_offers_exactly_the_shapes_on_disk()
    {
        var root = WebTemplateRoot();
        var manifest = File.ReadAllText(Path.Combine(root, ".template.config", "template.json"));

        var onDisk = Directory.GetDirectories(Path.Combine(root, ".shells"))
            .Select(Path.GetFileName)
            .Where(name => !name!.StartsWith('_'))
            .ToList();

        onDisk.Should().BeEquivalentTo(WebShellNames);
        foreach (var shape in onDisk)
            manifest.Should().Contain($"\"choice\": \"{shape}\"");
    }

    /// <summary>
    /// Both templates, both directions: a shape on disk that the manifest never offers is
    /// unreachable, and a choice the manifest offers with no folder behind it scaffolds an empty
    /// project. Neither fails anywhere else — the release gate asks the TEMPLATE what it offers, so
    /// a shell missing from the manifest is a shell the gate never walks and nobody hears about.
    /// </summary>
    [Theory]
    [InlineData("equantic-app")]
    [InlineData("equantic-native")]
    public void Every_shape_a_template_offers_is_on_disk_and_the_other_way_round(string template)
    {
        var root = Path.Combine(RepoRoot(), "src", "eQuantic.UI.Templates", "templates", template);
        var manifest = File.ReadAllText(Path.Combine(root, ".template.config", "template.json"));

        // `_`-prefixed folders are shared parts a shape includes, not shapes themselves.
        var onDisk = Directory.GetDirectories(Path.Combine(root, ".shells"))
            .Select(Path.GetFileName)
            .Where(name => !name!.StartsWith('_'))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var offered = Regex.Matches(manifest, @"""choice"":\s*""([^""]+)""")
            .Select(match => match.Groups[1].Value)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        onDisk.Should().NotBeEmpty($"{template} has to have shapes at all");
        offered.Should().BeEquivalentTo(onDisk,
            $"`dotnet new {template} --shell` offers exactly what is there to scaffold");
    }

    private static IEnumerable<MetadataReference> PlatformReferences() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.Length > 0)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));
}
