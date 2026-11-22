using eQuantic.UI.Testing;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace eQuantic.UI.Testing.Tests;

/// <summary>
/// A served page hydrated in a test (#1775): each defect a real site met only once its pages hydrated, on a
/// page of <c>tests/HydrationSite</c>, fails <see cref="HydrationAssert"/> with a message that names it, and a
/// page that hydrates as the server drew it passes. The site is built from the source tree and served by
/// Kestrel; its scripts run in happy-dom under the embedded bun its build used.
/// </summary>
public class HydrationAssertTests(HydrationSiteFactory site) : IClassFixture<HydrationSiteFactory>
{
    private static async Task<HydrationException> Fails(Func<Task> hydrate) =>
        (await hydrate.Should().ThrowAsync<HydrationException>()).Which;

    [Fact]
    public async Task APageThatHydratesAsTheServerDrewIt_Passes()
    {
        var report = await HydrationAssert.PageAsync(site, "/");

        report.Passed.Should().BeTrue(report.ToString());
        report.Page.Should().Be("HomePage");
        report.RoutedPage.Should().Be("HomePage");
        report.Hydrated.Should().BeTrue();
        report.Differences.Should().BeEmpty();
    }

    [Fact]
    public async Task ALabelTheBrowserDrawsBlank_IsNamedWithBothTexts()
    {
        var failure = await Fails(() => HydrationAssert.PageAsync(site, "/labels"));

        failure.Report.Differences.Should().ContainSingle(difference => difference.Kind == "text")
            .Which.Should().Match<HydrationDifference>(difference => difference.Server == "Our cakes" && difference.Client == "");
        failure.Message.Should().Contain("was 'Our cakes' on the server and '' in the browser");
    }

    [Fact]
    public async Task AnIconSizeTheBrowserRefuses_IsNamedByWhatItThrew()
    {
        var failure = await Fails(() => HydrationAssert.PageAsync(site, "/icon"));

        failure.Report.Errors.Should().Contain(error => error.Contains("Icon size 22", StringComparison.Ordinal));
        failure.Message.Should().Contain("the page reported an error").And.Contain("Icon size 22");
    }

    [Fact]
    public async Task AStringTheServerKeptInTheFirstRequestsLanguage_IsNamedWithBothTexts()
    {
        using (var client = site.CreateClient())
        {
            using var first = new HttpRequestMessage(HttpMethod.Get, "/greeting");
            first.Headers.AcceptLanguage.ParseAdd("pt-BR");
            (await client.SendAsync(first)).EnsureSuccessStatusCode();
        }

        var failure = await Fails(() => HydrationAssert.PageAsync(site, "/greeting"));

        failure.Message.Should().Contain("was 'Olá' on the server and 'Hello' in the browser");
    }

    [Fact]
    public async Task APathTheRouterGivesAnotherPage_IsNamedWithBothPages()
    {
        var failure = await Fails(() => HydrationAssert.PageAsync(site, "/promo"));

        failure.Report.Page.Should().Be("CataloguePage");
        failure.Report.RoutedPage.Should().Be("PromoPage");
        failure.Message.Should().Contain("the server drew CataloguePage, the browser's router chose PromoPage for /promo");
    }

    /// <summary>A redirected page is drawn where it was served: the window, the router and the report stand at
    /// the address the redirect led to, not the one asked for.</summary>
    [Fact]
    public async Task ARedirectedPage_IsDrawnWhereItWasServed()
    {
        var report = await HydrationAssert.PageAsync(site, "/?go=catalogue");

        report.Address.Should().EndWith("/catalogue");
        report.Page.Should().Be("CataloguePage");
        report.RoutedPage.Should().Be("CataloguePage");
    }

    /// <summary>
    /// A page whose module imports a shared chunk for its side effects alone hydrates (#1804). The bundler writes
    /// that import with no space after <c>import</c>, and the harness left it relative, which bun could not resolve
    /// among the modules it fetches from the server: the boot failed, and so did 15 of a real site's 18 pages.
    /// </summary>
    [Fact]
    public async Task APageThatImportsAChunkForItsSideEffects_Hydrates()
    {
        using (var client = site.CreateClient())
        {
            var module = await client.GetStringAsync("/_equantic/ShelfOnePage.js");
            module.Should().MatchRegex(@"(^|;)import""\./[^""]+\.js"";",
                "the shelf page imports the ribbon's chunk for its side effects alone, or this case proves nothing");
        }

        var report = await HydrationAssert.PageAsync(site, "/shelf/one");

        report.Passed.Should().BeTrue(report.ToString());
        report.Page.Should().Be("ShelfOnePage");
        report.Hydrated.Should().BeTrue();
    }

    /// <summary>In development the browser itself says it, naming both pages and the docs (#1778).</summary>
    [Fact]
    public async Task InDevelopment_TheBrowserSaysTheRouterChoseAnotherPage()
    {
        var failure = await Fails(() => HydrationAssert.PageAsync(site, "/promo"));

        failure.Report.Errors.Should().ContainSingle(error => error.Contains("the router chose", StringComparison.Ordinal))
            .Which.Should().Contain("the server drew CataloguePage, the router chose PromoPage for /promo")
            .And.Contain("wiki/ServerIntegration#the-router-and-the-server-choose-the-same-page");
    }

    /// <summary>In production the browser says nothing, and the test still fails: the report reads what each chose.</summary>
    [Fact]
    public async Task InProduction_TheBrowserSaysNothing_AndTheTestStillFails()
    {
        await using var production = site.WithWebHostBuilder(host => host.UseEnvironment("Production"));

        var failure = await Fails(() => HydrationAssert.PageAsync(production, "/promo"));

        failure.Report.Errors.Should().BeEmpty();
        failure.Report.RoutedPage.Should().Be("PromoPage");
        failure.Message.Should().Contain("the browser's router chose PromoPage");
    }
}
