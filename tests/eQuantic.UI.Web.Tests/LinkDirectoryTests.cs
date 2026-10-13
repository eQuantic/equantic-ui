using eQuantic.UI.Sdk.Tasks;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The <c>node_modules</c> link a <c>BunPackage</c> needs, made by the system's own call (#841). It was a
/// shell script on every system: <c>ln -s</c> behind a test on macOS and Linux, and on Windows
/// <c>mklink /D</c>, a command of <c>cmd.exe</c>, so the folder's path went through a shell.
/// </summary>
public class LinkDirectoryTests
{
    [Fact]
    public void TheLinkIsMade_AndWhatIsAlreadyThereStays()
    {
        var root = Directory.CreateTempSubdirectory("eq-link-").FullName;
        try
        {
            var target = Directory.CreateDirectory(Path.Combine(root, "obj $b 'c'", "node_modules")).FullName;
            var link = Path.Combine(root, "node_modules");
            var engine = new RecordingBuildEngine();

            new LinkDirectory { BuildEngine = engine, Link = link, Target = target }.Execute().Should().BeTrue();
            if (OperatingSystem.IsWindows() && !Directory.Exists(link))
            {
                // A Windows without Developer Mode makes no symbolic link unelevated: said at high
                // importance, and the build goes on, as the script's ignored exit code let it.
                engine.Messages.Should().Contain(message => message.Contains("could not be linked"));
                return;
            }
            new DirectoryInfo(link).LinkTarget.Should().Be(target);

            var elsewhere = Directory.CreateDirectory(Path.Combine(root, "elsewhere")).FullName;
            new LinkDirectory { BuildEngine = engine, Link = link, Target = elsewhere }.Execute().Should().BeTrue();
            new DirectoryInfo(link).LinkTarget.Should().Be(target, "the link of an earlier build stays");

            var kept = Directory.CreateDirectory(Path.Combine(root, "kept")).FullName;
            new LinkDirectory { BuildEngine = engine, Link = kept, Target = target }.Execute().Should().BeTrue();
            new DirectoryInfo(kept).LinkTarget.Should().BeNull("a folder the developer keeps there stays a folder");
            Directory.EnumerateFileSystemEntries(kept).Should().BeEmpty("nothing is linked inside it, which `ln -s` did");

            engine.Errors.Should().BeEmpty();
            engine.Warnings.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
