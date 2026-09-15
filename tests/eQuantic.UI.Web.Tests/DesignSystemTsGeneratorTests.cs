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
        // in it was already a `Sizing` rung — so the table is the rungs, read one at a time.
        //
        // Asserted rung by rung, each against ITS OWN body. The first version of this test after
        // the change checked that each method NAME appeared and then matched two values anywhere in
        // the file, which a generator mapping one rung's switch onto another name would have
        // satisfied — the medium height would have been found under `icon` and the test would have
        // passed. Found in review. The rows below are the spec's medium column, per rung.
        var medium = new (string Rung, string Value)[]
        {
            ("height", "40"), ("paddingX", "16"), ("gap", "8"),
            ("labelSize", "15"), ("icon", "20"), ("radius", "10"), ("hitTarget", "48"),
        };

        foreach (var (rung, value) in medium)
            Body(ts, rung).Should().Contain($"case 'medium': return {value};",
                $"the A12 medium row reaches the browser as `Sizing.{rung}`");

        // XLarge is the C# switch's default arm — unknown sizes resolve the same way on both sides.
        Body(ts, "height").Should().Contain("default: return 56;");

        // The two that belong to one control rather than to the ladder, and cross with the rest.
        ts.Should().Contain("buttonMinWidth: 64,");
        Body(ts, "avatarInitials").Should().Contain("case 'medium': return 13;");
    }

    /// <summary>The emitted body of ONE `Sizing` rung — from its name to the member that closes it.
    /// Reading a value out of the whole file would find it under any rung that happens to share
    /// the number, which is what made the assertions above mean less than they looked.</summary>
    private static string Body(string ts, string rung)
    {
        var start = ts.IndexOf($"  {rung}(", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, $"the generator emits `Sizing.{rung}`");
        var end = ts.IndexOf("\n  },", start, StringComparison.Ordinal);
        end.Should().BeGreaterThan(start, $"`Sizing.{rung}` is a closed member");
        return ts[start..end];
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
