using System.Runtime.CompilerServices;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Pins the runtime's <c>design-system.generated.ts</c> byte-for-byte to the C# generator — the same
/// "generated, never hand-written" rule the CSS follows. A drifting file (token change without
/// regeneration, or a hand edit) fails here. Refresh with <c>EQ_UPDATE_DESIGN_TS=1</c>.
/// </summary>
public class DesignSystemTsGeneratorTests
{
    private static string GeneratedFilePath([CallerFilePath] string sourcePath = "")
    {
        var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));
        return Path.Combine(repoRoot, "src", "eQuantic.UI.Runtime", "src", "shared", "design-system.generated.ts");
    }

    [Fact]
    public void GeneratedModule_MatchesCommittedFile()
    {
        var generated = DesignSystemTsGenerator.Generate(PhotonTheme.Instance);
        var path = GeneratedFilePath();

        if (Environment.GetEnvironmentVariable("EQ_UPDATE_DESIGN_TS") == "1")
        {
            File.WriteAllText(path, generated);
            return;
        }

        File.Exists(path).Should().BeTrue(
            $"the runtime ships the generated design system at {path} — run once with EQ_UPDATE_DESIGN_TS=1");
        File.ReadAllText(path).Should().Be(generated,
            "design-system.generated.ts must be regenerated (EQ_UPDATE_DESIGN_TS=1) whenever the C# tokens change");
    }

    [Fact]
    public void Generator_EmitsTheSpecA12SizeTable_AsArrays()
    {
        var ts = DesignSystemTsGenerator.Generate(PhotonTheme.Instance);

        // Medium row from the spec table: 40 · S4(16) · S2(8) · 15 · Dense(20) · Md(10) · 48.
        ts.Should().Contain("case 'medium': return [40, 16, 8, 15, 20, 10, 48];");
        // XLarge is the C# switch's default arm — unknown sizes resolve the same way on both sides.
        ts.Should().Contain("default: return [56, 24, 10, 17, 24, 14, 56];");
    }

    [Fact]
    public void Generator_EmitsPrimaryVariant_FromTheThemeSingleSource()
    {
        var ts = DesignSystemTsGenerator.Generate(PhotonTheme.Instance);

        // Primary.Base = #0050A0 light / #5CA2E8 dark — the same pair the CSS generator and the
        // cross-pinned lowering literal use.
        ts.Should().Contain("primary: new VariantColors(");
        ts.Should().Contain("t(c(0, 80, 160, 255), c(92, 162, 232, 255))");
    }
}
