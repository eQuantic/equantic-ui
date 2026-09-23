using System.Diagnostics;
using FluentAssertions;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The runtime the Server serves at <c>@equantic/runtime</c> is a BUILD OUTPUT, and nothing else.
/// <para>
/// <c>BundleRuntime</c> writes <c>wwwroot/runtime.js</c> from <c>eQuantic.UI.Sdk/Resources/boot.ts</c>
/// before every build of the Server, and the assembly embeds that file. It was ALSO committed, and
/// the committed copy lagged the runtime's source twice: a pull request that changed runtime
/// TypeScript without happening to build this project committed nothing, CI stayed green, and a
/// Server serving the previous runtime shipped (#273). The copy is gone rather than compared: with
/// no committed bundle there is nothing that can lag.
/// </para>
/// </summary>
public class ServedRuntimeIsABuildOutputTests
{
    private const string Bundle = "src/eQuantic.UI.Server/wwwroot/runtime.js";

    [Fact]
    public void The_served_runtime_is_embedded_from_the_bundle_this_build_wrote()
    {
        using var stream = typeof(UIExtensions).Assembly.GetManifestResourceStream("eQuantic.UI.Server.runtime.js");
        stream.Should().NotBeNull("the Server embeds the bundle under the name UIExtensions serves it by");
        using var copy = new MemoryStream();
        stream!.CopyTo(copy);

        copy.Length.Should().BeGreaterThan(0);
        File.ReadAllBytes(Path.Combine(Repository.Root(), Bundle)).Should().Equal(copy.ToArray(),
            "the embedded runtime is the file BundleRuntime wrote in this build, not one written by some earlier one");
    }

    [Fact]
    public void The_served_runtime_is_never_committed()
    {
        var root = Repository.Root();

        Git(root, $"ls-files -- {Bundle}").Should().BeEmpty(
            "the bundle is rebuilt on every Server build, and a committed copy is exactly what lagged the runtime's source (#273)");
        Git(root, $"check-ignore -- {Bundle}").Trim().Should().Be(Bundle,
            ".gitignore keeps the build's output out of a `git add .`");
    }

    private static string Git(string root, string arguments)
    {
        using var git = Process.Start(new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        var output = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        return output;
    }
}
