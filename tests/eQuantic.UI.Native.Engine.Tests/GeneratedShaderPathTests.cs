using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// No committed shader names the machine that generated it (#1416). `scripts/generate-shaders.sh` handed
/// `slangc` the source's absolute path, so every `#line` of `Sdf.metal` named a home folder: the file
/// named one person's machine, and each contributor who regenerated it got a diff of path lines that said
/// nothing about the shader. The generator hands a path relative to the shader's folder, and this reads
/// every generated output, the text and the binaries alike, for a path that only one machine has.
/// </summary>
public class GeneratedShaderPathTests
{
    private static string Generated([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", "..",
            "src", "eQuantic.UI.Native.Engine", "Shaders", "Generated"));

    /// <summary>A rooted path as a machine spells it: a drive and a whole folder (<c>C:\Users\</c>), or a
    /// POSIX root under one of the folders a checkout lives in. A folder after the drive, so a binary's
    /// chance bytes that read <c>x:/</c> are no path.</summary>
    private static readonly Regex MachinePath = new(
        @"(?<![A-Za-z0-9])(?:[A-Za-z]:[\\/][A-Za-z0-9._ -]{2,}[\\/]|/(?:Users|home|Volumes|private|tmp|var|opt|mnt|root|workspace|__w)/)",
        RegexOptions.Compiled);

    [Fact]
    public void EveryLineDirectiveOfTheMetalSource_NamesAFileRelativeToTheShader()
    {
        var metal = File.ReadAllLines(Path.Combine(Generated(), "Sdf.metal"));
        var named = metal
            .Select(line => Regex.Match(line, @"^#line\s+\d+\s+""(?<file>[^""]*)"""))
            .Where(match => match.Success)
            .Select(match => match.Groups["file"].Value)
            .Distinct()
            .ToList();

        named.Should().Contain("Sdf.slang", "the source is named as the generator hands it, beside the shader");
        named.Where(Path.IsPathRooted).Should().BeEmpty("a #line names the source relative to the shader's folder");
    }

    [Fact]
    public void NoGeneratedShader_NamesAPathOfTheMachineThatGeneratedIt()
    {
        var offenders = Directory.EnumerateFiles(Generated(), "*", SearchOption.AllDirectories)
            .Select(file => (File: Path.GetRelativePath(Generated(), file),
                Match: MachinePath.Match(Encoding.Latin1.GetString(File.ReadAllBytes(file)))))
            .Where(found => found.Match.Success)
            .Select(found => $"{found.File}: {found.Match.Value}")
            .ToList();

        offenders.Should().BeEmpty("a committed shader is the same file on every machine that generates it");
    }
}
