using System.Net;
using System.Reflection;
using System.Text.Json;
using eQuantic.UI.Server.Metadata;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Server.Rendering;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// What a CLIENT NAVIGATION gets, over the real pipeline: the page's server data and its metadata.
/// <para>
/// A link used to swap the component and nothing else. No prefetch ran, so every navigated-to page
/// rendered the empty state it was written to show while data loads — and nothing loaded it. The
/// head kept the previous document's title and canonical, which for a crawler is one page asserting
/// that two URLs are the same document.
/// </para>
/// <para>
/// The request goes to the TARGET ROUTE, carrying a header. That is the load-bearing part: the route
/// params, the query and the page resolution are a full load's, because it is the same route — a
/// side endpoint taking a path would have had to reimplement all three.
/// </para>
/// </summary>
public class ClientNavigationStateTests
{
    /// <summary>A page that only has anything to say once the server has loaded it.</summary>
    [eQuantic.UI.Primitives.Page("/probe")]
    public sealed class ProbePage : eQuantic.UI.Primitives.StatelessComponent,
        eQuantic.UI.Primitives.IServerPrefetch, IHandleMetadata
    {
        public string Loaded = "";

        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            Loaded = "from the server";
            return Task.CompletedTask;
        }

        public void ConfigureMetadata(SeoBuilder seo) => seo
            .Title("The Probe Page")
            .Canonical("https://example.test/probe");

        public override eQuantic.UI.Primitives.VisualNode Build(
            eQuantic.UI.Primitives.ComponentContext context) =>
            new eQuantic.UI.Primitives.Text(Loaded, eQuantic.UI.Primitives.TypeRole.BodyM);
    }

    private static async Task<(WebApplication App, HttpClient Client)> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options =>
        {
            options.ScanAssembly(Assembly.GetExecutingAssembly());
            options.ConfigureHtmlShell(shell => shell.SetTitle("The App"));
        });

        var app = builder.Build();
        app.MapUI();
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    private static async Task<JsonElement> NavigateAsync(HttpClient client, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-EQ-Navigate", "1");
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    [Fact]
    public async Task ANavigation_CarriesTheServerDataThePageWouldHaveHadOnAFullLoad()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        var payload = await NavigateAsync(client, "/probe");

        payload.TryGetProperty("state", out var state).Should().BeTrue(
            "without it the page renders the empty state it shows while data loads, forever");
        state.GetProperty("Loaded").GetString().Should().Be("from the server");
    }

    [Fact]
    public async Task ANavigation_CarriesTheMetadataToo()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        var payload = await NavigateAsync(client, "/probe");

        payload.GetProperty("title").GetString().Should().Be("The Probe Page",
            "the title falling back to the app default is what a stale head looks like");
        payload.GetProperty("head").GetString().Should().Contain("https://example.test/probe",
            "a canonical left on the previous page tells a crawler the two URLs are one document");
    }

    /// <summary>
    /// It does NOT draw the page. A navigation needs the data and the metadata; the browser already
    /// has the component and builds the tree itself, so markup rendered here is markup thrown away —
    /// a whole tree build per navigation, which is what made warming a page on hover unaffordable.
    /// </summary>
    [Fact]
    public async Task ANavigation_DoesNotPayForMarkupNobodyReads()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        var rendering = app.Services.GetRequiredService<IServerRenderingService>();
        using var scope = app.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Path = "/probe";

        var prepared = await rendering.PreparePageAsync(nameof(ProbePage), context);

        prepared.Success.Should().BeTrue();
        prepared.Html.Should().BeEmpty("the drawing is the part a navigation does not need");
        prepared.SerializedState.Should().Contain("from the server", "…while the data still arrives");
        prepared.Metadata!.Title.Should().Be("The Probe Page");
    }

    /// <summary>Without the header the same route still serves the DOCUMENT — one route, two shapes,
    /// and the ordinary one is unchanged.</summary>
    [Fact]
    public async Task WithoutTheHeader_TheSameRouteStillServesTheDocument()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        var html = await client.GetStringAsync("/probe");

        html.Should().StartWith("<!doctype html>").And.Contain("ProbePage");
    }
}
