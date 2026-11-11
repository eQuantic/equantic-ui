using System.Text;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The guard the shell sets to keep dotnet watch's refresh script out of a page the rebuild stream
/// reloads (#1275), read off the script the SDK this repository builds with injects.
/// <para>
/// The script keeps it against connecting twice, when it is both injected and written into a page by
/// hand, and returns at once when the page already holds it. It is the script's own name, not a
/// documented switch, so an SDK that renamed it would put back the second reload of every save
/// without anything else failing: the edit-to-view measurement would, in the samples job, and this
/// says why on any machine, the day global.json moves.
/// </para>
/// </summary>
public class WatchScriptGuardTests
{
    /// <summary>The property of <c>window</c> the script returns at once on.</summary>
    internal const string Guard = "_dotnet_watch_ws_injected";

    private static string Dotnet(string directory, string argument)
    {
        var (exitCode, output, error) = ChildProcess.Run("dotnet", [argument], TimeSpan.FromMinutes(1), directory);
        exitCode.Should().Be(0, $"dotnet {argument} answers: {error}");
        return output;
    }

    [Fact]
    public void TheRefreshScriptOfTheSdkThisRepositoryBuildsWith_StillReturnsOnTheGuardTheShellSets()
    {
        var root = RepoRoot.Find();
        // The SDK global.json selects here, roll-forward included, and where it is installed.
        var version = Dotnet(root, "--version").Trim();
        var installed = Dotnet(root, "--list-sdks")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(' ', 2))
            .Single(parts => parts[0] == version)[1].Trim('[', ']');
        var tools = Path.Combine(installed, version, "DotnetTools", "dotnet-watch");

        var middleware = Directory.GetFiles(tools, "Microsoft.AspNetCore.Watch.BrowserRefresh.dll", SearchOption.AllDirectories);
        middleware.Should().NotBeEmpty($"dotnet watch {version} injects its refresh script from this assembly, under {tools}");

        foreach (var assembly in middleware)
        {
            // The script is an embedded resource, written as UTF-8 text.
            var script = Encoding.UTF8.GetString(File.ReadAllBytes(assembly));
            script.Should().Contain($"const scriptInjectedSentinel = '{Guard}';",
                $"the shell names the guard of {assembly}, and a renamed one lets the script reload every page a second time");
            script.Should().Contain("if (window.hasOwnProperty(scriptInjectedSentinel)) {",
                "the script returns before it connects when the page holds the guard");
        }
    }
}
