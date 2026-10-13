using System.Xml;
using System.Xml.Linq;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// No MSBuild file the repository ships starts a program through a shell (#841). MSBuild's <c>Exec</c>
/// writes its command into a script for <c>/bin/sh</c> or <c>cmd.exe</c>, and every program the SDKs
/// start goes through <c>RunProgram</c> instead, with a list of arguments. Scanned from every
/// <c>.props</c> and <c>.targets</c> file under <c>src/</c> rather than from a list of the SDKs' files, so
/// an <c>Exec</c> added to a file nobody listed fails here the day it is written. The repository's own
/// build, in its project files, is not shipped and not scanned.
/// </summary>
public class NoShippedTargetStartsAShellTests
{
    [Fact]
    public void NoMSBuildFileUnderSrc_HoldsAnExec()
    {
        var src = Path.Combine(SdkProbe.RepoRoot(), "src");
        var files = Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".props", StringComparison.Ordinal) || path.EndsWith(".targets", StringComparison.Ordinal))
            .Where(path => !Path.GetRelativePath(src, path).Split(Path.DirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj" or "node_modules"))
            .ToList();

        files.Should().Contain(path => path.EndsWith(Path.Combine("eQuantic.UI.Sdk", "Sdk", "Sdk.targets"), StringComparison.Ordinal))
            .And.Contain(path => path.EndsWith(Path.Combine("eQuantic.UI.Sdk.Native", "Sdk", "Sdk.targets"), StringComparison.Ordinal),
                "the scan reads the two SDKs' own files, or it proves nothing");
        files.SelectMany(path => XDocument.Load(path, LoadOptions.SetLineInfo).Descendants()
                .Where(element => element.Name.LocalName == "Exec")
                .Select(element => $"{Path.GetRelativePath(src, path)}:{((IXmlLineInfo)element).LineNumber}"))
            .Should().BeEmpty("every program the SDKs start goes through RunProgram, with no shell between them");
    }
}
