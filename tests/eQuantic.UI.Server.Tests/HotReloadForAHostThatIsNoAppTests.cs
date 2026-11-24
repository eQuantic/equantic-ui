using System.Diagnostics;
using eQuantic.UI.Server.HotReload;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// Hot reload in a host that serves eQuantic.UI pages without being an eQuantic.UI app: an ordinary ASP.NET Core
/// project whose pages come from a library it references (#1757). There is no eqc for its project, and the
/// server asked MSBuild for the SDK's target anyway, so its log opened with MSBuild's MSB4057 and "Build failed",
/// and every save of its C# asked again and sent the page an error. It says once, in its own words, that hot reload
/// is off for this host, and asks MSBuild no more.
/// </summary>
[Collection(ProcessConsoleCollection.Name)]
public class HotReloadForAHostThatIsNoAppTests
{
    /// <summary>A server no edit reaches, as one run outside dotnet watch is.</summary>
    private static ServerEdits NoEdits => new(arriveOnSave: false, TimeSpan.Zero);

    /// <summary>The real MSBuild, asked about a real project that is not built with eQuantic.UI.Sdk.</summary>
    [Fact]
    public void AHostThatIsNoUiApp_SaysOnceThatHotReloadIsOff_WithNoMsBuildError()
    {
        var host = Directory.CreateTempSubdirectory("eq-host-").FullName;
        File.WriteAllText(Path.Combine(host, "Host.csproj"),
            """<Project Sdk="Microsoft.NET.Sdk.Web"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>""");
        var original = System.Console.Out;
        using var captured = new StringWriter();
        System.Console.SetOut(captured);
        CompileOutcome first, second;
        TimeSpan again;
        bool available;
        try
        {
            using var compiler = new RunningCompiler(host);
            compiler.Prepare();
            first = compiler.Compile(TimeSpan.FromMinutes(2));
            var clock = Stopwatch.StartNew();
            second = compiler.Compile(TimeSpan.FromMinutes(2));
            again = clock.Elapsed;
            available = compiler.Available;
        }
        finally
        {
            System.Console.SetOut(original);
            Directory.Delete(host, recursive: true);
        }

        var said = captured.ToString();
        said.Should().NotContain("MSB4057").And.NotContain("Build failed").And.NotContain("could not say how to start eqc");
        said.Split("hot reload is off").Should().HaveCount(2, "it is said once:\n" + said);
        first.Unavailable.Should().BeTrue();
        second.Unavailable.Should().BeTrue();
        available.Should().BeFalse();
        again.Should().BeLessThan(TimeSpan.FromSeconds(1), "a save does not ask MSBuild again once the host is known");
    }

    /// <summary>A save in such a host rebuilds nothing, says nothing and sends the page no error.</summary>
    [Fact]
    public void ASave_InAHostWithNoEqc_RebuildsNothing_AndSendsThePageNoError()
    {
        var compiles = 0;
        using var service = new HotReloadService(Path.GetTempPath(),
            new StandInCompiler(_ => { compiles++; return CompileOutcome.Off; }, available: false), TimeSpan.FromSeconds(30), NoEdits);
        var original = System.Console.Out;
        using var captured = new StringWriter();
        System.Console.SetOut(captured);
        try
        {
            service.SourceChanged();
            service.Rebuild();
        }
        finally
        {
            System.Console.SetOut(original);
        }

        captured.ToString().Should().BeEmpty("a host with no eqc has nothing to rebuild and nothing to report on a save");
        compiles.Should().Be(0);
    }
}
