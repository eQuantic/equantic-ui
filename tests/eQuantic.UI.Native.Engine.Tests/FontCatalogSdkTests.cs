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

    /// <summary>
    /// An intermediate path that is absolute, as .NET's artifacts layout or a shared obj folder makes
    /// it: the list and the catalog are where MSBuild put them, not under the project. Joined to the
    /// project unconditionally, eqicon was asked for a file nobody wrote.
    /// </summary>
    [Theory]
    [InlineData("eQuantic.UI.Sdk")]
    [InlineData("eQuantic.UI.Sdk.Native")]
    public void TheFontStep_HoldsWithAnAbsoluteIntermediatePath(string sdk)
    {
        Font("Probe-Regular.ttf", TestFont.Build("Acme Probe"));
        var obj = Path.Combine(_dir, "artifacts", "obj") + Path.DirectorySeparatorChar;

        var (exit, output) = RunFontStep(sdk, $"-p:IntermediateOutputPath={obj}");

        exit.Should().Be(0, output);
        File.ReadAllText(Path.Combine(obj, "generated", "eQuantic.UI.Fonts", "Fonts.g.cs"))
            .Should().Contain("public static FontFamily AcmeProbe { get; }");
    }

    /// <summary>
    /// A project in a folder whose name has a bar, which macOS and Linux allow: the step handed each file
    /// as one line, <c>path|resource</c>, the reader passed over every line that did not split in two, and
    /// the app got no catalog, so a theme that named its face did not compile.
    /// </summary>
    [SkippableTheory]
    [InlineData("eQuantic.UI.Sdk")]
    [InlineData("eQuantic.UI.Sdk.Native")]
    public void TheFontStep_HoldsInAFolderWhoseNameHasABar(string sdk)
    {
        Skip.If(OperatingSystem.IsWindows(), "a bar is not a character of a Windows path");
        var folder = Path.Combine(_dir, "brand|probe");
        Font("Probe-Regular.ttf", TestFont.Build("Acme Probe"), folder);

        var (exit, output) = RunFontStepIn(folder, sdk);

        exit.Should().Be(0, output);
        File.ReadAllText(Path.Combine(folder, "obj", "Debug", "net10.0", "generated", "eQuantic.UI.Fonts", "Fonts.g.cs"))
            .Should().Contain("public static FontFamily AcmeProbe { get; }");
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

    /// <summary>
    /// Android packs every file under <c>Assets/</c> into the package's assets by default, and the
    /// fonts leave that set, since the assembly carries them already. A text file beside them stays,
    /// which is what shows the defaults ran at all: without the workload there are no assets to ask.
    /// </summary>
    [SkippableFact]
    public void OnAndroid_AFontIsNotPackedASecondTime()
    {
        Font("Probe-Regular.ttf", TestFont.Build("Acme Probe"));
        File.WriteAllText(Path.Combine(_dir, "Assets", "Fonts", "notes.txt"), "beside the font");
        var project = Consumer("eQuantic.UI.Sdk.Native", "net10.0-android");

        var (exit, output, error) = ChildProcess.Dotnet(
            ["msbuild", project, "-nologo", "-getItem:AndroidAsset", "-getItem:EmbeddedResource"], Deadline, RepoRoot());

        exit.Should().Be(0, output + error);
        using var json = JsonDocument.Parse(output[output.IndexOf('{')..]);
        var items = json.RootElement.GetProperty("Items");
        var assets = items.TryGetProperty("AndroidAsset", out var found)
            ? found.EnumerateArray().Select(item => item.GetProperty("Identity").GetString()!).ToList()
            : [];
        Skip.If(assets.Count == 0, "the Android workload is not installed here, so no default asset is packed to ask about");
        assets.Should().Contain(asset => asset.EndsWith("notes.txt"), "the defaults ran")
            .And.NotContain(asset => asset.EndsWith(".ttf"), "the font is in the assembly already");
        items.GetProperty("EmbeddedResource").EnumerateArray()
            .Should().Contain(item => item.GetProperty("Identity").GetString()!.EndsWith("Probe-Regular.ttf"));
    }

    /// <summary>A font file under the Assets of the project in <paramref name="folder"/>, the test's own by default.</summary>
    private void Font(string name, byte[] bytes, string? folder = null)
    {
        var fonts = Directory.CreateDirectory(Path.Combine(folder ?? _dir, "Assets", "Fonts")).FullName;
        File.WriteAllBytes(Path.Combine(fonts, name), bytes);
    }

    /// <summary>
    /// The SDK's font target on a consumer, with this build's eqicon and tasks: both sit beside the
    /// tests, as project references, and a path into src/bin is whatever was built there last. Run
    /// from the repository's root, so the global.json it pins picks the SDK.
    /// </summary>
    private (int Exit, string Output) RunFontStep(string sdk, params string[] properties) => RunFontStepIn(_dir, sdk, properties);

    /// <summary>As <see cref="RunFontStep"/>, for the project in <paramref name="folder"/>.</summary>
    private (int Exit, string Output) RunFontStepIn(string folder, string sdk, params string[] properties)
    {
        var project = Consumer(sdk, "net10.0", folder);
        var tools = AppContext.BaseDirectory;
        var (exit, output, error) = ChildProcess.Dotnet(
        [
            "msbuild", project, "-nologo", "-t:EQuanticFonts", "-getItem:EmbeddedResource",
            $"-p:_EqIconToolDir={tools}", $"-p:_EqTasksAssembly={Path.Combine(tools, "eQuantic.UI.Sdk.Tasks.dll")}",
            .. properties,
        ], Deadline, RepoRoot());
        return (exit, output + error);
    }

    /// <summary>A consumer of <paramref name="sdk"/> in <paramref name="folder"/>, the test's own by default,
    /// for <paramref name="targetFramework"/>.</summary>
    private string Consumer(string sdk, string targetFramework, string? folder = null)
    {
        var sdkDir = Path.Combine(RepoRoot(), "src", sdk, "Sdk");
        var project = Path.Combine(folder ?? _dir, "Acme.Probe.csproj");
        File.WriteAllText(project, $"""
            <Project>
                <Import Project="{Path.Combine(sdkDir, "Sdk.props")}" />
                <PropertyGroup>
                    <TargetFramework>{targetFramework}</TargetFramework>
                    <OutputType>Exe</OutputType>
                    <Configuration>Debug</Configuration>
                    <ApplicationId>tech.equantic.probe</ApplicationId>
                    <ApplicationTitle>Probe</ApplicationTitle>
                </PropertyGroup>
                <Import Project="{Path.Combine(sdkDir, "Sdk.targets")}" />
            </Project>
            """);
        return project;
    }
}
