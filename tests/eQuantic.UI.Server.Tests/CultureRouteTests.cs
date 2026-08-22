using System.Net;
using eQuantic.UI.Primitives;
using eQuantic.UI.Server;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The language as the first path segment, declared ONCE (<c>UseCultureRoutes</c>) and honoured by
/// the endpoints, the negotiated culture, the client's route table, the hreflang group and every
/// in-app href — all on the platform's own pieces: a route constraint, the built-in route-data
/// culture provider, <c>RequestLocalizationOptions</c> configured through DI.
/// </summary>
public class CultureRouteTests
{
    private sealed class PricingPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            new Link("/about", new Text("About", TypeRole.Heading));
    }

    private sealed class AboutPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("About", TypeRole.Heading);
    }

    private sealed class HomePage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("Home", TypeRole.Heading);
    }

    private static async Task<(WebApplication App, HttpClient Client)> StartAppAsync(bool lambdaOverload = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            WebRootPath = Directory.CreateTempSubdirectory("eq-culture-routes-").FullName,
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options =>
        {
            options.EnableSsr = true;
            options.ScanAssembly(typeof(CultureRouteTests).Assembly);
            options.UseCultureRoutes("en", "pt-BR", "es");
        });

        var app = builder.Build();
        // The easy path reads the options AddUI configured; the lambda overload builds its own and
        // names the list again — both must land in the same place.
        if (lambdaOverload) app.UseRequestLocalization(o => o.UseCultureRoutes("en", "pt-BR", "es"));
        else app.UseRequestLocalization();
        app.MapPage<HomePage>("/", title: "Home");
        app.MapPage<PricingPage>("/pricing", title: "Pricing");
        app.MapPage<AboutPage>("/about", title: "About");
        app.MapUI();
        await app.StartAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        return (app, client);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task APrefixedUrl_RendersInThatLanguage_AndLinksStayInIt(bool lambdaOverload)
    {
        var (app, client) = await StartAppAsync(lambdaOverload);
        await using var _ = app;

        var response = await client.GetAsync("/pt-BR/pricing");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("<html lang=\"pt-BR\"");
        // The author wrote `/about`; the render's link policy carried the language.
        html.Should().Contain("href=\"/pt-BR/about\"");
        html.Should().NotContain("href=\"/about\"");
    }

    [Fact]
    public async Task TheDefaultCulture_StaysUnprefixed()
    {
        var (app, client) = await StartAppAsync();
        await using var _ = app;

        var response = await client.GetAsync("/pricing");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("<html lang=\"en\"");
        html.Should().Contain("href=\"/about\"");
    }

    [Fact]
    public async Task TheHome_UnderAPrefix_IsTheBareSegment()
    {
        var (app, client) = await StartAppAsync();
        await using var _ = app;

        // `/` under pt-BR is `/pt-BR` — one URL for the home, not a second one with a slash.
        var response = await client.GetAsync("/pt-BR");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("<html lang=\"pt-BR\"").And.Contain("Home");
    }

    [Fact]
    public async Task APrefixTheAppNeverDeclared_IsNotALanguage()
    {
        var (app, client) = await StartAppAsync();
        await using var _ = app;

        // The constraint admits the declared prefixes and nothing else: English under /fr/ would
        // let a crawler index a French URL that is not French.
        var response = await client.GetAsync("/fr/pricing");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ABareUrl_AskedForInAPrefixedLanguage_GoesToItsAddress()
    {
        var (app, client) = await StartAppAsync();
        await using var _ = app;
        // The cookie CultureSwitcher writes — ASP.NET's own, read by its own provider.
        client.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c%3Dpt-BR%7Cuic%3Dpt-BR");

        var response = await client.GetAsync("/pricing?tab=2");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Be("/pt-BR/pricing?tab=2");
    }

    [Fact]
    public async Task TheRouteValue_WinsOverTheCookie()
    {
        var (app, client) = await StartAppAsync();
        await using var _ = app;
        client.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c%3Des%7Cuic%3Des");

        // A URL naming a language is a promise about what the page says: the provider that reads
        // the segment sits ahead of the cookie's.
        var html = await client.GetStringAsync("/pt-BR/pricing");

        html.Should().Contain("<html lang=\"pt-BR\"");
    }

    [Fact]
    public async Task TheClientTable_AndTheHreflangGroup_FollowTheSameMap()
    {
        var (app, client) = await StartAppAsync();
        await using var _ = app;

        var html = await client.GetStringAsync("/pricing");

        // The browser matches the literal prefixes (its matcher would accept any segment for a
        // constraint it does not know), and learns the map to prefix hrefs it lowers itself.
        html.Should().Contain("'/pt-BR/pricing'").And.Contain("'/es/pricing'");
        html.Should().Contain("cultureRoutes: { default: 'en', prefixed: ['pt-BR','es'] }");
        // hreflang from the same map, with no separate policy declared.
        html.Should().Contain("hreflang=\"pt-BR\" href=\"http://localhost/pt-BR/pricing\"");
        html.Should().Contain("hreflang=\"en\" href=\"http://localhost/pricing\"");
        html.Should().Contain("hreflang=\"x-default\" href=\"http://localhost/pricing\"");
    }

    [Fact]
    public void TheMap_ReplacesAPrefix_NeverStacksOne()
    {
        var map = CultureRouteMap.From(["en", "pt-BR"]);

        map.PathFor("pt-BR", "/pricing").Should().Be("/pt-BR/pricing");
        map.PathFor("pt-BR", "/pt-BR/pricing").Should().Be("/pt-BR/pricing");
        map.PathFor("en", "/pt-BR/pricing").Should().Be("/pricing");
        map.PathFor("pt-BR", "/").Should().Be("/pt-BR");
        map.Split("/PT-br/about").Should().Be(("pt-BR", "/about"));
        map.Split("/about").Should().Be(("en", "/about"));
    }
}
