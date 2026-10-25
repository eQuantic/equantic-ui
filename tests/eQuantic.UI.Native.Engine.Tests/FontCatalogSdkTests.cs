using System.Runtime.CompilerServices;
using System.Text.Json;
using eQuantic.UI.Tests;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The SDKs' font step, run as it ships (#1055): a consumer of each SDK with font files under its
/// <c>Assets</c> folder gets them embedded under the catalog's resource names, and gets the catalog,
/// written by the eqicon and the tasks of this build. A file the SDK cannot read stops the build with
/// EQ4005, naming it.
/// </summary>
public sealed class FontCatalogSdkTests : IDisposable
{
    private static readonly TimeSpan Deadline = TimeSpan.FromMinutes(3);
    private readonly string _dir = Directory.CreateTempSubdirectory("eq-font-sdk-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { /* a temp dir */ }
    }

    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    [Theory]
    [InlineData("eQuantic.UI.Sdk")]
    [InlineData("eQuantic.UI.Sdk.Native")]
    public void TheFontsUnderAssets_AreEmbedded_AndCatalogued(string sdk)
    {
        Font("Probe-Regular.ttf", TestFont.Build("Acme Probe"));
        Font("Probe-Italic.ttf", TestFont.Build("Acme Probe", italic: true));

        var (exit, output) = RunFontStep(sdk);

        exit.Should().Be(0, output);
        using var json = JsonDocument.Parse(output[output.IndexOf('{')..]);
        var embedded = json.RootElement.GetProperty("Items").GetProperty("EmbeddedResource").EnumerateArray()
            .Where(item => item.TryGetProperty("EQuanticFont", out var font) && font.GetString() == "true")
            .Select(item => item.GetProperty("LogicalName").GetString())
            .ToList();
        embedded.Should().BeEquivalentTo(["eQuantic.UI.Fonts/Probe-Regular.ttf", "eQuantic.UI.Fonts/Probe-Italic.ttf"]);

        var catalog = File.ReadAllText(Path.Combine(_dir, "obj", "Debug", "net10.0", "generated", "eQuantic.UI.Fonts", "Fonts.g.cs"));
        catalog.Should().Contain("[assembly: FontCatalog(typeof(Acme.Probe.Fonts))]")
            .And.Contain("public static FontFamily AcmeProbe { get; } = new(\"Acme Probe\",")
            .And.Contain("new FontFile(\"Acme.Probe\", \"eQuantic.UI.Fonts/Probe-Regular.ttf\", 400, 400),")
            .And.Contain("new FontFile(\"Acme.Probe\", \"eQuantic.UI.Fonts/Probe-Italic.ttf\", 400, 400, Italic: true));");
    }

    [Theory]
    [InlineData("eQuantic.UI.Sdk")]
    [InlineData("eQuantic.UI.Sdk.Native")]
    public void AFileThatIsNotAFont_StopsTheBuild_NamingIt(string sdk)
    {
        Font("Broken.ttf", "this is not a font"u8.ToArray());

        var (exit, output) = RunFontStep(sdk);

        exit.Should().NotBe(0);
        output.Should().Contain("error EQ4005").And.Contain("Broken.ttf is not a face the SDK can ship");
    }

    private void Font(string name, byte[] bytes)
    {
        Directory.CreateDirectory(Path.Combine(_dir, "Assets", "Fonts"));
        File.WriteAllBytes(Path.Combine(_dir, "Assets", "Fonts", name), bytes);
    }

    /// <summary>
    /// The SDK's font target on a consumer, with this build's eqicon and tasks: both sit beside the
    /// tests, as project references, and a path into src/bin is whatever was built there last. Run
    /// from the repository's root, so the global.json it pins picks the SDK.
    /// </summary>
    private (int Exit, string Output) RunFontStep(string sdk)
    {
        var sdkDir = Path.Combine(RepoRoot(), "src", sdk, "Sdk");
        var project = Path.Combine(_dir, "Acme.Probe.csproj");
        File.WriteAllText(project, $"""
            <Project>
                <Import Project="{Path.Combine(sdkDir, "Sdk.props")}" />
                <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <OutputType>Exe</OutputType>
                    <Configuration>Debug</Configuration>
                </PropertyGroup>
                <Import Project="{Path.Combine(sdkDir, "Sdk.targets")}" />
            </Project>
            """);
        var tools = AppContext.BaseDirectory;
        var (exit, output, error) = ChildProcess.Dotnet(
        [
            "msbuild", project, "-nologo", "-t:EQuanticFonts", "-getItem:EmbeddedResource",
            $"-p:_EqIconToolDir={tools}", $"-p:_EqTasksAssembly={Path.Combine(tools, "eQuantic.UI.Sdk.Tasks.dll")}",
        ], Deadline, RepoRoot());
        return (exit, output + error);
    }
}
