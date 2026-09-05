using Microsoft.Extensions.DependencyInjection;
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
    [eQuantic.UI.Primitives.Page("/known")]
    public class KnownPage { }

    /// <summary>
    /// The app's branded not-found page. A REAL component that prefetches, because the branding on
    /// a 404 is exactly the thing an app loads rather than hardcodes — a logo, a support link, the
    /// tenant's name — and it is reached two ways: mapped at /404, and by every URL that matched
    /// nothing. Both have to hydrate from the same payload.
    /// </summary>
    [eQuantic.UI.Primitives.Page("/404")]
    public class BrandedNotFound : eQuantic.UI.Primitives.StatelessComponent, eQuantic.UI.Primitives.IServerPrefetch
    {
        private string _brand = "(not loaded)";

        [eQuantic.UI.Primitives.ServerOnly]
        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            if (services.GetService(typeof(IBreakTheNotFoundPage)) != null)
                throw new InvalidOperationException("the branded 404 page could not load its branding");

            _brand = "northwind-brand";
            return Task.CompletedTask;
        }

        public override eQuantic.UI.Primitives.VisualNode Build(eQuantic.UI.Primitives.ComponentContext context) =>
            new eQuantic.UI.Primitives.Text(_brand, eQuantic.UI.Primitives.TypeRole.Heading);
    }

    /// <summary>Registered only by the test that wants the 404 page to fail, so the page can break
    /// on demand without a static flag that leaks into every other test in the class.</summary>
    private interface IBreakTheNotFoundPage { }

    private sealed class BreakIt : IBreakTheNotFoundPage { }

    /// <summary>The app's 500 page, so the error path has somewhere to go.</summary>
    [eQuantic.UI.Primitives.Page("/500")]
    public class AppError : eQuantic.UI.Primitives.StatelessComponent
    {
        public override eQuantic.UI.Primitives.VisualNode Build(eQuantic.UI.Primitives.ComponentContext context) =>
            new eQuantic.UI.Primitives.Text("something broke", eQuantic.UI.Primitives.TypeRole.Heading);
    }

    private static async Task<(WebApplication App, HttpClient Client)> StartAppAsync(
        bool scanTestPages, bool ssr = false, bool breakNotFound = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options =>
        {
            // SSR OFF by default: most of these pin the ROUTING contract — the status, and which
            // page boots — and the routing pages are not real components. The payload test turns it
            // on, because what it compares is what SSR hands over.
            options.EnableSsr = ssr;
            if (scanTestPages) options.ScanAssembly(Assembly.GetExecutingAssembly());
        });

        if (breakNotFound) builder.Services.AddSingleton<IBreakTheNotFoundPage, BreakIt>();

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

    [Fact]
    public async Task TheFallbackCarriesTheSamePayloadAsTheMappedRoute()
    {
        var (app, client) = await StartAppAsync(scanTestPages: true, ssr: true);
        await using var _ = app;

        var mapped = await client.GetAsync("/404");
        var fallback = await client.GetAsync("/definitely-not-a-page");

        var mappedHtml = await mapped.Content.ReadAsStringAsync();
        var fallbackHtml = await fallback.Content.ReadAsStringAsync();

        // ONE page, two doors. The fallback branch rendered the same markup and then forgot to hand
        // over the state, so an app whose 404 loads its branding served it correctly and blanked it
        // the moment the page hydrated — on the door every real 404 comes through, and on no other.
        mappedHtml.Should().Contain("__INITIAL_STATE__").And.Contain("northwind-brand");
        fallbackHtml.Should().Contain("__INITIAL_STATE__").And.Contain("northwind-brand");
        fallback.StatusCode.Should().Be(HttpStatusCode.NotFound, "branding does not make a page found");

        // And the HEAD, not only the payload: the page's atomic CSS rides in the render result's
        // assets, so a fallback that adopts less of the result serves the right markup with none of
        // its classes defined. Comparing the <head> is what makes "one page, two doors" testable at
        // all — anything a render result grows later is covered by the same line.
        Head(mappedHtml).Should().Be(Head(fallbackHtml), "one page cannot have two heads");
    }

    [Fact]
    public async Task A404PageThatFailsStillAnswers404()
    {
        var (app, client) = await StartAppAsync(scanTestPages: true, ssr: true, breakNotFound: true);
        await using var _ = app;

        var response = await client.GetAsync("/definitely-not-a-page");

        // The request has not changed: it is still a URL that matches nothing. The app's own 404
        // page failing is the SERVER's problem and not the reader's, and answering 500 tells every
        // crawler and link checker the server broke — which also undoes the whole reason the
        // fallback swallows that failure in the first place.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>The document head, which is where everything a render result contributes lands.</summary>
    private static string Head(string html)
    {
        var start = html.IndexOf("<head", StringComparison.OrdinalIgnoreCase);
        var end = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        return start >= 0 && end > start ? html[start..end] : html;
    }
}
