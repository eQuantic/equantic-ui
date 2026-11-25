using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A resx accessor read once, into a static, is a warning (EQ3107, #1776): it keeps the language of
/// whoever reached it first, the first request on the server and the first page in the browser, and
/// only a second language shows it.
/// </summary>
public class CultureFrozenResourceAnalyzerTests
{
    /// <summary>The accessor class Visual Studio's ResXFileCodeGenerator writes, cut to one key.</summary>
    private const string Catalog = """
        internal class Strings
        {
            private static global::System.Resources.ResourceManager? resourceMan;
            internal static global::System.Resources.ResourceManager ResourceManager =>
                resourceMan ??= new global::System.Resources.ResourceManager("App.Strings", typeof(Strings).Assembly);
            internal static global::System.Globalization.CultureInfo? Culture { get; set; }
            internal static string Title => ResourceManager.GetString("Title", Culture)!;
            internal static string Subtitle => ResourceManager.GetString("Subtitle", Culture)!;
        }
        """;

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)TestReferences.Of(path));
        var compilation = CSharpCompilation.Create("App",
            [CSharpSyntaxTree.ParseText(source, path: "Probe.cs"), CSharpSyntaxTree.ParseText(Catalog, path: "Strings.Designer.cs")],
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("the probe is C# that compiles; only the analyzer may speak");
        return await compilation.WithAnalyzers([new CultureFrozenResourceAnalyzer()]).GetAnalyzerDiagnosticsAsync();
    }

    [Theory]
    [InlineData("static readonly string Heading = Strings.Title;", "the static 'Heading' is initialized")]
    [InlineData("static string Heading { get; } = Strings.Title;", "the static 'Heading' is initialized")]
    [InlineData("static readonly string Heading = $\"{Strings.Title}!\";", "the static 'Heading' is initialized")]
    [InlineData("static readonly string Heading; static Probe() { Heading = Strings.Title; }", "'Probe' runs its static constructor")]
    public async Task AnAccessorReadIntoAStatic_IsReported(string member, string when)
    {
        var diagnostics = await AnalyzeAsync($"public static class Probe {{ {member} }}");

        var warning = diagnostics.Should().ContainSingle().Which;
        warning.Id.Should().Be("EQ3107");
        warning.Severity.Should().Be(DiagnosticSeverity.Warning);
        warning.GetMessage().Should().StartWith($"'Strings.Title' is read once, when {when}, so it keeps the language");
        warning.GetMessage().Should().EndWith("in a getter ('=> Strings.Title') or a method.");
    }

    [Fact]
    public async Task EveryAccessorACollectionOfThemReads_IsReported()
    {
        var diagnostics = await AnalyzeAsync("""
            public static class Probe
            {
                static readonly string[] Lines = [Strings.Title, Strings.Subtitle];
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(' ')[0])
            .Should().Equal("'Strings.Title'", "'Strings.Subtitle'");
    }

    [Theory]
    [InlineData("public static class Probe { static string Heading => Strings.Title; }")]
    [InlineData("public static class Probe { static string Heading() => Strings.Title; }")]
    [InlineData("public static class Probe { static readonly System.Func<string> Heading = () => Strings.Title; }")]
    [InlineData("public sealed class Probe { readonly string _heading = Strings.Title; public string Heading => _heading; }")]
    [InlineData("public static class Probe { static string Heading() { string Read() => Strings.Title; return Read(); } }")]
    public async Task AnAccessorReadEachTimeItIsReached_IsLeftAlone(string source)
    {
        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Should().BeEmpty("a getter, a method, a lambda and an instance's field read the accessor when they run");
    }

    [Fact]
    public async Task AStaticPropertyOfAClassThatIsNoCatalog_IsLeftAlone()
    {
        var diagnostics = await AnalyzeAsync("""
            internal static class Labels
            {
                internal static string Title => "Title";
                internal static global::System.Resources.ResourceManager? ResourceManager => null;
            }

            public static class Probe
            {
                static readonly string Heading = Labels.Title;
            }
            """);

        diagnostics.Should().BeEmpty("a catalog has a ResourceManager AND a Culture, as the generator writes it");
    }
}
