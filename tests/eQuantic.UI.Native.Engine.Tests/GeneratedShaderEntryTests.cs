using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Every entry point the ONE shader source declares is in every output generated from it (#1937). The
/// Metal backend loads the committed <c>Sdf.metallib</c> first and the Vulkan one a single-entry SPIR-V
/// module per stage, so an entry added to <c>Sdf.slang</c> and missing from either output is a device that
/// cannot create its pipeline, on a Mac or on Windows, while every Reference test stays green. The entries
/// are read from the source, never listed here: a list would be a second place the shader lives.
/// </summary>
public class GeneratedShaderEntryTests
{
    private static string Shaders([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", "..",
            "src", "eQuantic.UI.Native.Engine", "Shaders"));

    /// <summary>Every <c>[shader("…")]</c> function of <c>Sdf.slang</c>, by name.</summary>
    private static IReadOnlyList<string> Entries()
    {
        var source = File.ReadAllText(Path.Combine(Shaders(), "Sdf.slang"));
        return Regex.Matches(source, @"\[shader\(""\w+""\)\]\s*\w+\s+(?<name>\w+)\s*\(")
            .Select(match => match.Groups["name"].Value)
            .ToList();
    }

    [Fact]
    public void TheSource_DeclaresTheTileLayersEntry()
    {
        Entries().Should().Contain("tiled_rgba_fragment").And.Contain("sdf_fragment");
    }

    [Fact]
    public void EveryEntryOfTheSource_IsInTheMetalSourceAndTheCommittedMetallib()
    {
        var generated = Path.Combine(Shaders(), "Generated");
        var metal = File.ReadAllText(Path.Combine(generated, "Sdf.metal"));
        var metallib = Encoding.Latin1.GetString(File.ReadAllBytes(Path.Combine(generated, "Sdf.metallib")));

        foreach (var entry in Entries())
        {
            metal.Should().Contain(entry, $"Sdf.metal is generated from the source, entry {entry} included");
            metallib.Should().Contain(entry,
                $"the Metal backend loads Sdf.metallib first, so {entry} must be compiled into it "
                + "(./scripts/generate-shaders.sh with the Xcode Metal Toolchain)");
        }
    }

    [Fact]
    public void EveryEntryOfTheSource_HasItsSingleEntrySpirvModule()
    {
        var spirv = Path.Combine(Shaders(), "Generated", "spirv");
        Entries().Where(entry => !File.Exists(Path.Combine(spirv, entry + ".spv")))
            .Should().BeEmpty("the Vulkan backend creates each pipeline from spirv/<entry>.spv, "
                + "which ./scripts/generate-shaders.sh writes for every entry it lists");
    }
}
