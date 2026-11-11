using System.Reflection;
using eQuantic.UI.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// Whether an app rebuilds its modules on a save is ONE decision, and the stream that announces a
/// rebuild, the cache of the modules and the client that listens all read it.
/// <para>
/// It was the Development environment alone, and an app run under <c>dotnet watch</c> without a launch
/// profile is a Production one: the edit reached .NET and never the browser, since eqc never ran
/// again (#627). The client asked <c>__EQ_DEV__</c> on its own, which agreed with the server only while
/// the app left <c>HotReload</c> unset.
/// </para>
/// </summary>
public class HotReloadDecisionTests
{
    [Page("/watched")]
    public sealed class WatchedPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("watched", TypeRole.BodyM);
    }

    private sealed class Host(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "App";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Theory]
    [InlineData(null, "Development", null, true)]
    [InlineData(null, "Production", null, false)]
    // dotnet watch on an app with no launch profile, whose environment is Production (#627).
    [InlineData(null, "Production", "1", true)]
    [InlineData(null, "Staging", "1", true)]
    [InlineData(null, "Production", "0", false)]
    // The app's word wins either way.
    [InlineData(false, "Development", "1", false)]
    [InlineData(true, "Production", null, true)]
    public void TheDecision_IsTheAppsWord_ThenDevelopment_OrDotnetWatch(
        bool? hotReload, string environment, string? dotnetWatch, bool hotReloads)
    {
        var options = new UIOptions { HotReload = hotReload };

        options.HotReloads(new Host(environment), dotnetWatch).Should().Be(hotReloads);
    }

    /// <summary>
    /// A started app that is STOPPED before it is disposed. Hot reload lives as long as the app and is
    /// disposed when the app stops (<c>ApplicationStopping</c>), and disposing a host that was not
    /// stopped never stops it: the watchers on the content root and the MSBuild the running compiler
    /// asks, whose working directory is that root, went on holding it. Windows refuses to remove a
    /// folder something holds, so the case that removes its content root failed there and passed on
    /// Linux and macOS (#1366).
    /// </summary>
    private sealed class Running(WebApplication app) : IAsyncDisposable
    {
        public HttpClient Client { get; } = app.GetTestClient();

        public async ValueTask DisposeAsync()
        {
            try
            {
                await app.StopAsync();
            }
            finally
            {
                await app.DisposeAsync();
            }
        }
    }

    private static async Task<Running> StartAsync(
        string environment, bool? hotReload, string? contentRoot = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environment,
            ContentRootPath = contentRoot,
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options =>
        {
            options.ScanAssembly(Assembly.GetExecutingAssembly());
            options.HotReload = hotReload;
        });
        var app = builder.Build();
        app.MapUI();
        await app.StartAsync();
        return new Running(app);
    }

    [Theory]
    // Each case sets the environment against the decision: the client follows the server, never the
    // environment, so a page listens exactly when there is a stream to listen to.
    [InlineData("Development", false, false)]
    [InlineData("Production", true, true)]
    [InlineData("Production", null, false)]
    public async Task TheClient_ListensExactlyWhenTheServerStreamsRebuilds(string environment, bool? hotReload, bool hotReloads)
    {
        await using var app = await StartAsync(environment, hotReload);
        var client = app.Client;

        var document = await client.GetStringAsync("/watched");
        var config = ShellConfig.In(document);
        using var stream = new HttpRequestMessage(HttpMethod.Get, "/_equantic/hmr");
        using var answer = await client.SendAsync(stream, HttpCompletionOption.ResponseHeadersRead);

        config.GetProperty("hotReload").GetBoolean().Should().Be(hotReloads);
        (answer.Content.Headers.ContentType?.MediaType == "text/event-stream").Should().Be(hotReloads,
            "the page is told to listen exactly when the stream is mapped");
        // The stream is the page's one source of reloads, so dotnet watch's refresh script is kept out
        // of it exactly when there is one (#1275), and left alone on every other page.
        document.Contains($"window.{WatchScriptGuardTests.Guard} = true;", StringComparison.Ordinal).Should().Be(hotReloads,
            "the page that listens to the stream tells dotnet watch's script it is taken");
    }

    /// <summary>
    /// The modules a rebuild rewrites revalidate exactly when the app rebuilds them, and the stage-one
    /// maps, which carry the app's C#, are served where the error overlay that reads them installs:
    /// in Development, and nowhere else, a run under dotnet watch included.
    /// </summary>
    [Theory]
    [InlineData("Production", true, "no-cache", false)]
    [InlineData("Development", false, "public, max-age=31536000, immutable", true)]
    [InlineData("Production", null, "public, max-age=31536000, immutable", false)]
    public async Task TheModulesCache_FollowsTheDecision_AndTheCSharpMapsStayInDevelopment(
        string environment, bool? hotReload, string cacheControl, bool mapsServed)
    {
        var root = Path.Combine(Path.GetTempPath(), $"eq-hot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "wwwroot", "_equantic"));
        Directory.CreateDirectory(Path.Combine(root, "obj", "eQuantic", "ts"));
        File.WriteAllText(Path.Combine(root, "wwwroot", "_equantic", "Probe.js"), "export {};");
        File.WriteAllText(Path.Combine(root, "obj", "eQuantic", "ts", "Probe.ts.map"), "{\"sourcesContent\":[\"// C#\"]}");
        try
        {
            // Stopped and disposed at the end of this block, before the finally removes the root.
            await using var app = await StartAsync(environment, hotReload, root);
            var client = app.Client;

            using var module = await client.GetAsync("/_equantic/Probe.js");
            using var map = await client.GetAsync("/_equantic/src-map/Probe.ts.map");

            module.Headers.CacheControl!.ToString().Should().Be(cacheControl);
            (map.Content.Headers.ContentType?.MediaType == "application/json").Should().Be(mapsServed);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
