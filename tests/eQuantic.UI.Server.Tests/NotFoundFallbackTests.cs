using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The 404 contract, over the REAL pipeline (TestServer, no mocks): an unknown route answers with
/// HTTP status 404 — never a 200 that merely looks like a 404 — and when the app declared its own
/// <c>[Page("/404")]</c>, that page is the one the shell boots. Both halves shipped dead for
/// months: the fallback served 200s, and the "default error pages" it was meant to show were
/// legacy components no client could mount.
/// </summary>
public class NotFoundFallbackTests
{
    /// <summary>An app page occupying a normal route, so known-route behavior is contrasted.</summary>
    [eQuantic.UI.Core.Page("/known")]
    public class KnownPage { }

    /// <summary>The app's branded not-found page — just another routed page.</summary>
    [eQuantic.UI.Core.Page("/404")]
    public class BrandedNotFound { }

    private static async Task<(WebApplication App, HttpClient Client)> StartAppAsync(bool scanTestPages)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options =>
        {
            // SSR off: these tests pin the ROUTING contract (status + which page boots), not
            // rendering. The test page types aren't real components and have no compiled bundles.
            options.EnableSsr = false;
            if (scanTestPages) options.ScanAssembly(Assembly.GetExecutingAssembly());
        });

        var app = builder.Build();
        app.MapUI();
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    [Fact]
    public async Task UnknownRoute_AnswersStatus404()
    {
        var (app, client) = await StartAppAsync(scanTestPages: false);
        await using var _ = app;

        var response = await client.GetAsync("/definitely-not-a-page");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        // The shell still serves (the runtime paints the styled default), with no page to boot.
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("page: null");
    }

    [Fact]
    public async Task UnknownRoute_BootsTheAppsDeclared404Page()
    {
        var (app, client) = await StartAppAsync(scanTestPages: true);
        await using var _ = app;

        var response = await client.GetAsync("/definitely-not-a-page");

        // Still a TRUE 404 — a branded page changes the pixels, not the status.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("page: 'BrandedNotFound'");
    }

    [Fact]
    public async Task DeclaredRoutes_StillAnswer200()
    {
        var (app, client) = await StartAppAsync(scanTestPages: true);
        await using var _ = app;

        (await client.GetAsync("/known")).StatusCode.Should().Be(HttpStatusCode.OK);
        // Browsing straight to /404 hits a mapped page, not the fallback.
        (await client.GetAsync("/404")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
