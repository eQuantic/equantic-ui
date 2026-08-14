using System.Net;
using eQuantic.UI.Server;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// Every response the app sends says who powers it — through the REAL pipeline (TestServer, no
/// mocks): the startup filter AddUI registers runs first, so pages, unknown routes and everything
/// in between carry the stamp with zero lines in any Program.cs.
/// </summary>
public class PoweredByHeaderTests
{
    private static async Task<(WebApplication App, HttpClient Client)> StartAppAsync(
        Action<UIOptions>? configure = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options =>
        {
            options.ScanAssembly(typeof(PoweredByHeaderTests).Assembly);
            configure?.Invoke(options);
        });
        var app = builder.Build();
        app.MapUI();
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    [Fact]
    public async Task EveryResponseSaysWhoPowersIt()
    {
        var (app, client) = await StartAppAsync();
        await using var _ = app;

        var page = await client.GetAsync("/");
        var missing = await client.GetAsync("/no-such-route");

        page.Headers.GetValues("x-powered-by").Should().ContainSingle().Which.Should().Be("eQuantic.UI");
        // The stamp is the PIPELINE's, not the page's: a 404 is still this framework answering.
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missing.Headers.GetValues("x-powered-by").Should().ContainSingle().Which.Should().Be("eQuantic.UI");
    }

    /// <summary>Name only — a version in a response header is a gift to vulnerability scanners
    /// with nothing in it for anyone else.</summary>
    [Fact]
    public async Task TheStampCarriesNoVersion()
    {
        var (app, client) = await StartAppAsync();
        await using var _ = app;

        var value = (await client.GetAsync("/")).Headers.GetValues("x-powered-by").Single();

        value.Should().NotMatchRegex(@"\d", "provenance, not a changelog");
    }

    [Fact]
    public async Task TheHardeningChecklistCanTurnItOff()
    {
        var (app, client) = await StartAppAsync(options => options.WithoutPoweredByHeader());
        await using var _ = app;

        var response = await client.GetAsync("/");

        response.Headers.Contains("x-powered-by").Should().BeFalse();
    }
}
