using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Services;

/// <summary>
/// A transpiled module's map carries the C# only in <see cref="SourceMapMode.Full"/>, and never names
/// the build machine (#352). The map sits in a web root, and a Release publish shipped it: <c>sources</c>
/// held each file's absolute path and <c>sourcesContent</c> the whole file, a <c>[ServerAction]</c>'s
/// body included. The SDK passes Full in Debug and None everywhere else.
/// </summary>
public class SourceMapModeTests
{
    private static readonly string Project = Path.Combine(Path.GetTempPath(), "eq-maps", "App");
    private static readonly string Maps = Path.Combine(Project, "obj", "eQuantic", "ts");
    private static readonly string Screen = Path.Combine(Project, "Screens", "Home.cs");

    private const string Source = """
        using eQuantic.UI.Primitives;

        namespace Demo;

        public sealed class Home : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) => new Text("only-in-the-source", TypeRole.BodyM);
        }
        """;

    private static string? MapOf(SourceMapMode mode, string sourcePath) =>
        new ComponentCompiler { SourceMaps = mode, SourceRoot = Project, SourceMapDirectory = Maps }
            .CompileSource(Source, sourcePath).Single().SourceMap;

    [Fact]
    public void Full_CarriesTheCSharp_AndNamesTheFileInsideTheProject()
    {
        using var map = JsonDocument.Parse(MapOf(SourceMapMode.Full, Screen)!);

        map.RootElement.GetProperty("sources")[0].GetString().Should().Be("Screens/Home.cs");
        // A source resolves relative to the map, which is written into the intermediate folder: the
        // root leads back to the project, so a debugger shows Screens/Home.cs and nothing of the machine.
        map.RootElement.GetProperty("sourceRoot").GetString().Should().Be("../../../");
        map.RootElement.GetProperty("sourcesContent")[0].GetString().Should().Contain("only-in-the-source");
    }

    [Fact]
    public void External_CarriesNoCSharp()
    {
        var json = MapOf(SourceMapMode.External, Screen)!;
        using var map = JsonDocument.Parse(json);

        map.RootElement.TryGetProperty("sourcesContent", out _).Should().BeFalse();
        json.Should().NotContain("only-in-the-source");
        map.RootElement.GetProperty("sources")[0].GetString().Should().Be("Screens/Home.cs");
    }

    [Fact]
    public void None_WritesNoMap() => MapOf(SourceMapMode.None, Screen).Should().BeNull();

    /// <summary>A package's sources in the NuGet cache would climb out of the project with <c>../</c>,
    /// naming the machine's layout after all.</summary>
    [Fact]
    public void AFileOutsideTheProject_IsNamedByItsFileNameAlone()
    {
        var outside = Path.Combine(Path.GetTempPath(), "eq-maps", "packages", "some.library", "Shared.cs");
        using var map = JsonDocument.Parse(MapOf(SourceMapMode.Full, outside)!);

        map.RootElement.GetProperty("sources")[0].GetString().Should().Be("Shared.cs");
    }

    /// <summary>
    /// Every control character a C# file can hold, in a comment, where the lexer takes anything but a
    /// line break: a form feed and a vertical tab are C# whitespace besides. The map escaped five
    /// characters by hand and wrote the rest raw, which JSON refuses (#525): <c>'0x0C' is invalid within
    /// a JSON string</c>, and a map that is not JSON is one the bundler and the browser drop in silence.
    /// </summary>
    private static readonly string ControlSource =
        "using eQuantic.UI.Primitives;\n\nnamespace Demo;\n\n// "
        + string.Concat(Enumerable.Range(0, 0x20).Where(c => c is not ('\n' or '\r')).Select(c => (char)c))
        + "\npublic sealed class Home : StatelessComponent\n{\n"
        + "    public override VisualNode Build(ComponentContext context) => new Text(\"\f\v\", TypeRole.BodyM);\n}\n";

    [Fact]
    public void ASourceHoldingEveryControlCharacter_MapsToJsonThatReadsBackAsTheSource()
    {
        var result = new ComponentCompiler { SourceMaps = SourceMapMode.Full, SourceRoot = Project, SourceMapDirectory = Maps }
            .CompileSource(ControlSource, Screen).Single();
        result.Success.Should().BeTrue(string.Join("\n", result.Errors.Select(error => error.Message)));

        using var map = JsonDocument.Parse(result.SourceMap!);

        map.RootElement.GetProperty("sourcesContent")[0].GetString().Should().Be(ControlSource);
    }

    /// <summary>A file name may hold what a JSON string may not, and the map wrote its <c>sources</c>
    /// raw (#525).</summary>
    [Fact]
    public void AFileNameHoldingAQuote_IsNamedAsItIs()
    {
        var quoted = Path.Combine(Project, "Screens", "Say\"Hi.cs");
        using var map = JsonDocument.Parse(MapOf(SourceMapMode.Full, quoted)!);

        map.RootElement.GetProperty("sources")[0].GetString().Should().Be("Screens/Say\"Hi.cs");
    }
}
