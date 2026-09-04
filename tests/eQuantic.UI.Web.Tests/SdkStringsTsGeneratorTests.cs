using System.Runtime.CompilerServices;
using FluentAssertions;
using eQuantic.UI.Web.Build;

namespace eQuantic.UI.Web.Tests;

/// <summary>Pins sdk-strings.generated.ts byte-for-byte to SdkResources.resx
/// (EQ_UPDATE_SDK_STRINGS_TS=1 regenerates).</summary>
public class SdkStringsTsGeneratorTests
{
    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    [Fact]
    public void GeneratedSdkStrings_MatchCommittedFile()
    {
        var root = RepoRoot();
        var resx = Path.Combine(root, "src", "eQuantic.UI.Components", "SdkResources.resx");
        var path = Path.Combine(root, "src", "eQuantic.UI.Runtime", "src", "shared", "sdk-strings.generated.ts");

        var generated = SdkStringsTsGenerator.Generate(resx);

        if (Environment.GetEnvironmentVariable("EQ_UPDATE_SDK_STRINGS_TS") == "1")
        {
            File.WriteAllText(path, generated);
            return;
        }

        File.Exists(path).Should().BeTrue($"generate once with EQ_UPDATE_SDK_STRINGS_TS=1 ({path})");
        File.ReadAllText(path).Should().Be(generated,
            "sdk-strings.generated.ts must be regenerated (EQ_UPDATE_SDK_STRINGS_TS=1) when "
            + "SdkResources.resx changes");
    }
}
