using System.Runtime.CompilerServices;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>Pins icons.generated.ts byte-for-byte to the C# IconRegistry (EQ_UPDATE_ICONS_TS=1 regenerates).</summary>
public class IconTsGeneratorTests
{
    private static string GeneratedFilePath([CallerFilePath] string sourcePath = "")
    {
        var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));
        return Path.Combine(repoRoot, "src", "eQuantic.UI.Runtime", "src", "shared", "icons.generated.ts");
    }

    [Fact]
    public void GeneratedIcons_MatchCommittedFile()
    {
        var generated = IconTsGenerator.Generate();
        var path = GeneratedFilePath();

        if (Environment.GetEnvironmentVariable("EQ_UPDATE_ICONS_TS") == "1")
        {
            File.WriteAllText(path, generated);
            return;
        }

        File.Exists(path).Should().BeTrue($"generate once with EQ_UPDATE_ICONS_TS=1 ({path})");
        File.ReadAllText(path).Should().Be(generated,
            "icons.generated.ts must be regenerated (EQ_UPDATE_ICONS_TS=1) when IconRegistry changes");
    }
}
