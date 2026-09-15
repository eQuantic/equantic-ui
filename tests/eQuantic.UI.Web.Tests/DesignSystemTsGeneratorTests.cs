using System.Runtime.CompilerServices;
using eQuantic.UI.Primitives;
using FluentAssertions;
using eQuantic.UI.Web.Build;

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
    public void Generator_EmitsTheSpecA12SizeTable_AsRungs()
    {
        var ts = DesignSystemTsGenerator.Generate(PhotonTheme.Instance);

        // The A12 table still reaches the browser; it is no longer a seven-slot array. It was one
        // while `ButtonStyles.metrics` existed to hand the Button's twin a tuple, and every number
        // in it was already a `Sizing` rung — so the table is the rungs, read one at a time the way
        // every other component reads them.
        //
        // The medium row from the spec, rung by rung: 40 · S4(16) · S2(8) · 15 · Dense(20) · Md(10) · 48.
        foreach (var rung in new[] { "height", "paddingX", "gap", "labelSize", "icon", "radius", "hitTarget" })
            ts.Should().Contain($"{rung}(", $"the A12 table reaches the browser as `Sizing.{rung}`");

        ts.Should().Contain("case 'medium': return 40;");
        ts.Should().Contain("case 'medium': return 15;");
        // XLarge is the C# switch's default arm — unknown sizes resolve the same way on both sides.
        ts.Should().Contain("default: return 56;");

        // The two that belong to one control rather than to the ladder, and cross with it.
        ts.Should().Contain("buttonMinWidth: 64,");
        ts.Should().Contain("avatarInitials(size: string): number {");
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
