using System.Net;
using System.Reflection;
using System.Text.Json;
using eQuantic.UI.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// A production page loads no byte of the dev loop (#1359). The boot imported the error overlay and the
/// capture of a page's fields around a hot reload, and decided at run time whether to use them, so the
/// runtime every page loads first carried about 30 KB of code a production page never runs. The loop is
/// now the dev runtime's alone (<c>runtime.dev.js</c>, whose entry is <c>boot.dev.ts</c>), and the server
/// names the runtime a page imports: the dev one in Development and wherever it streams rebuilds, and
/// <c>runtime.js</c> everywhere else.
/// </summary>
public class ServedDevLoopTests
{
    [Page("/dev-loop")]
    public sealed class DevLoopPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("dev loop", TypeRole.BodyM);
    }

    /// <summary>The sources a served runtime's map names, read from the resource the Server embeds.</summary>
    private static List<string> SourcesOf(string runtime)
    {
        using var stream = typeof(UIExtensions).Assembly.GetManifestResourceStream($"eQuantic.UI.Server.{runtime}.map");
        stream.Should().NotBeNull($"the Server embeds {runtime}'s map beside it");
        using var map = JsonDocument.Parse(stream!);
        return map.RootElement.GetProperty("sources").EnumerateArray().Select(source => source.GetString()!).ToList();
    }

    [Fact]
    public void TheRuntimeEveryPageLoads_CarriesNoModuleOfTheDevLoop()
    {
        var sources = SourcesOf("runtime.js");

        sources.Should().HaveCountGreaterThan(100, "the map names every module the bundle holds");
        sources.Where(source => source.Contains("/src/dev/", StringComparison.Ordinal)).Should().BeEmpty(
            "the error overlay and the hot-reload state are the dev runtime's, which a production page never loads");
    }

    [Fact]
    public void TheDevRuntime_CarriesTheLoop()
    {
        var sources = SourcesOf("runtime.dev.js");

        sources.Should().Contain(source => source.EndsWith("/src/dev/error-overlay.ts", StringComparison.Ordinal));
        sources.Should().Contain(source => source.EndsWith("/src/dev/hot-reload-state.ts", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Production", null, "runtime.js")]
    [InlineData("Development", null, "runtime.dev.js")]
    // dotnet watch on an app with no launch profile is a Production one, and it streams rebuilds (#627).
    [InlineData("Production", true, "runtime.dev.js")]
    [InlineData("Development", false, "runtime.dev.js")]
    public async Task APage_ImportsTheRuntimeItsServerNames(string environment, bool? hotReload, string runtime)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options =>
        {
            options.ScanAssembly(Assembly.GetExecutingAssembly());
            options.HotReload = hotReload;
        });
        var app = builder.Build();
        app.MapUI();
        await app.StartAsync();
        try
        {
            var client = app.GetTestClient();
            var document = await client.GetStringAsync("/dev-loop");
            using var dev = await client.GetAsync("/_equantic/runtime.dev.js");

            document.Should().Contain($"\"@equantic/runtime\": \"/_equantic/{runtime}?v=",
                "the import map names the runtime the server decided on");
            dev.StatusCode.Should().Be(runtime == "runtime.dev.js" ? HttpStatusCode.OK : HttpStatusCode.NotFound,
                "the dev runtime is answered only where the dev loop is served");
        }
        finally
        {
            // Stopped before it is disposed: hot reload is disposed when the app stops (#1366).
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
