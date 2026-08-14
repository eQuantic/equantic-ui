using System.Runtime.CompilerServices;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Pins the runtime's <c>enums.generated.ts</c> byte-for-byte to the C# generator, the same
/// "generated, never hand-written" rule the design system follows. A union written by hand drifts
/// the day someone adds an enum member, and a drifted union is worse than the bare <c>string</c> it
/// replaced: it is a type that lies. Refresh with <c>EQ_UPDATE_ENUMS_TS=1</c>.
/// </summary>
public class EnumUnionsTsGeneratorTests
{
    private static string GeneratedFilePath([CallerFilePath] string sourcePath = "")
    {
        var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));
        return Path.Combine(repoRoot, "src", "eQuantic.UI.Runtime", "src", "shared", "enums.generated.ts");
    }

    [Fact]
    public void GeneratedModule_MatchesCommittedFile()
    {
        var generated = EnumUnionsTsGenerator.Generate();
        var path = GeneratedFilePath();

        if (Environment.GetEnvironmentVariable("EQ_UPDATE_ENUMS_TS") == "1")
        {
            File.WriteAllText(path, generated);
            return;
        }

        File.Exists(path).Should().BeTrue(
            $"the runtime ships the generated unions at {path} — run once with EQ_UPDATE_ENUMS_TS=1");
        File.ReadAllText(path).Should().Be(generated,
            "the unions mirror the C# enums and are regenerated, never edited "
            + "(EQ_UPDATE_ENUMS_TS=1 dotnet test eQuantic.UI.Web.Tests)");
    }

    /// <summary>
    /// The union says exactly what the TRANSPILER emits: a camelCase member string. Spot-checked
    /// against the three shapes that could drift apart — an ordinary member, one whose second word
    /// keeps its capital, and one whose name is a single letter plus a digit.
    /// </summary>
    [Fact]
    public void TheMembersAreSpelledTheWayTheTranspilerEmitsThem()
    {
        var generated = EnumUnionsTsGenerator.Generate();

        generated.Should().Contain("export type MainAlignValue = 'start' | 'center' | 'end' | 'spaceBetween';");
        generated.Should().Contain("'bodyM'", "TypeRole.BodyM keeps the capital the transpiler keeps");
        generated.Should().Contain("'topStart'", "a two-word member camelCases only its first letter");
    }

    /// <summary>A [Flags] enum combines its members, so its runtime value is a NUMBER and no union
    /// describes it. Its absence here is the contract, not an omission.</summary>
    [Fact]
    public void AFlagsEnumHasNoUnion()
    {
        var generated = EnumUnionsTsGenerator.Generate();

        generated.Should().NotContain("SafeEdgesValue");
        generated.Should().NotContain("KeyModifiersValue");
        EnumUnionsTsGenerator.Unions().Should().NotContain(typeof(SafeEdges));
        EnumUnionsTsGenerator.Unions().Should().Contain(typeof(MainAlign));
    }
}
