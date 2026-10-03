using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using eQuantic.UI.Conformance.Tests.Hydration;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using eQuantic.UI.Server;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The hydration crossing with both halves executed: the server renders a page and writes its payload,
/// and the page's twin, transpiled from the same source, builds in the embedded Bun on that payload. The
/// browser's first build has to draw what the server drew.
/// <para>
/// Each half had tests of its own, and each agreed with itself. The server named an auto-property after
/// the field the C# compiler synthesized and a captured parameter after its storage, the twin declared
/// both under eqc's names, and the values drew on the server and vanished on hydration (#509). A service
/// the container handed a page crossed whole, and the browser, which builds the page without it, drew
/// the other branch wherever the payload did not carry it (#510). Only running one half on the other's
/// output shows either.
/// </para>
/// <para>
/// The twin renders without a DOM: adopting the payload, building and lowering are the steps that
/// decide what the first build draws, and none of them touches the document.
/// </para>
/// </summary>
public class HydrationCrossingTests(CrossingTwins twins) : IClassFixture<CrossingTwins>
{
    private const string Authority = "https://login.example.test/crossing-tenant";
    private const string ApiKey = "sk-crossing-0123456789";

    [Fact]
    public async Task APrefetchedPropertyAndACapturedParameter_DrawWhatTheServerDrew()
    {
        var (html, drawn) = await CrossAsync("/crossing", nameof(CrossingPage));

        html.Should().Contain("downloads 42").And.Contain("quote ACME",
            "the server draws what the prefetches loaded, which is what the twin has to match");
        drawn[0].Should().Be("downloads 42 | quote ACME",
            "the first build after hydration draws the values the server wrote into the document");
        drawn[1].Should().Be(drawn[0], "a client navigation hands the router the same values, under the same names");
    }

    [Fact]
    public async Task AServerValueReadForWhetherItIsThere_DrawsTheServersBranch_WithNothingOfIt()
    {
        var (html, drawn) = await CrossAsync("/crossing-identity", nameof(CrossingIdentityPage));

        html.Should().Contain("href=\"/account\"").And.Contain("account").And.NotContain("crossing-tenant");
        drawn[0].Should().Be("href /account | account", "the browser builds the page without the identity");
        drawn[1].Should().Be(drawn[0]);
    }

    [Fact]
    public async Task AServerValueReadThroughAField_DrawsTheMemberTheServerDrew_AndNoOther()
    {
        var (html, drawn) = await CrossAsync("/crossing-options", nameof(CrossingOptionsPage));

        html.Should().Contain("title The Docs").And.NotContain(ApiKey);
        drawn[0].Should().Be("title The Docs");
        drawn[1].Should().Be(drawn[0]);
    }

    [Fact]
    public async Task AServerValueAChildReads_DrawsWhatTheChildDrewOnTheServer()
    {
        var (html, drawn) = await CrossAsync("/crossing-account", nameof(CrossingAccountPage));

        html.Should().Contain("badge Ada").And.NotContain("crossing-tenant");
        drawn[0].Should().Be("badge Ada");
        drawn[1].Should().Be(drawn[0]);
    }

    [Fact]
    public async Task APropertyThatKeepsItsStoreThroughField_DrawsWhatTheServerDrew()
    {
        // Through its setter, the store would be halved a second time in the browser.
        var (html, drawn) = await CrossAsync("/crossing-halves", nameof(CrossingHalvesPage));

        html.Should().Contain("half 42");
        drawn[0].Should().Be("half 42");
        drawn[1].Should().Be(drawn[0]);
    }

    [Fact]
    public async Task AMemberTheRuntimeTypeHides_CrossesAsTheOneCSharpRead()
    {
        // The page reads CrossingBaseOptions.Title; the container hands it a CrossingBrandedOptions whose
        // own Title hides it, and which the page never reads.
        var (html, drawn) = await CrossAsync("/crossing-branded", nameof(CrossingBrandedPage));

        html.Should().Contain("brand bound").And.NotContain("hidden");
        drawn[0].Should().Be("brand bound");
        drawn[1].Should().Be(drawn[0]);
    }

    [Fact]
    public async Task AServiceIsReadDownToItsScalars_EachLandingAsTheServerReadIt()
    {
        // A struct's computed long, a struct's public field and a nullable struct with and without a
        // value, which whole crossed wrong or not at all; and a list a pattern reads beneath, a
        // dictionary and a list of longs, which cross whole and are rebuilt as the browser's own.
        var (html, drawn) = await CrossAsync("/crossing-report", nameof(CrossingReportPage));

        var expected = ("count 84 | count is 42 | x 5 | maybe 42 | never none | tag a | price 84 | scores 2 first 43"
                + " | roles admin of 2").Split(" | ");
        foreach (var line in expected) html.Should().Contain(line);
        drawn[0].Split(" | ").Should().Equal(expected);
        drawn[1].Should().Be(drawn[0]);
    }

    [Fact]
    public async Task ACollectionAPrefetchFilled_DrawsAsTheClassItsCodeReads()
    {
        // A set, a stack, a queue of longs, a linked list and a sorted set: each crossed as an array.
        var (html, drawn) = await CrossAsync("/crossing-collections", nameof(CrossingCollectionsPage));

        var expected = ("roles True 2 | top 3 | next 9007199254740994 | list 2 True | sorted 1 3 | names ,a,b,B"
            + " | prices ,1,9,10 3 | index ,a,b,B | ranks ,Zeta,Alpha,Mid | amounts True").Split(" | ");
        foreach (var line in expected) html.Should().Contain(line);
        drawn[0].Split(" | ").Should().Equal(expected);
        drawn[1].Should().Be(drawn[0]);
    }

    /// <summary>
    /// The page served for a load, and what its twin draws on the payload of that load and on the state
    /// a navigation to it receives.
    /// </summary>
    private async Task<(string Html, string[] Drawn)> CrossAsync(string path, string page)
    {
        JsExecutor.RequireBun().Should().NotBeNullOrEmpty();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(new CrossingIdentity { Authority = Authority, DisplayName = "Ada" });
        builder.Services.AddSingleton(new CrossingOptions { Title = "The Docs", ApiKey = ApiKey });
        var branded = new CrossingBrandedOptions { Title = "hidden" };
        ((CrossingBaseOptions)branded).Title = "bound";
        builder.Services.AddSingleton<CrossingBaseOptions>(branded);
        builder.Services.AddSingleton(new CrossingReport
        {
            Totals = new CrossingTotals(7),
            Maybe = new CrossingTotals(7),
            Position = new CrossingPosition { X = 5 },
            Tags = ["a", "b"],
            Prices = new() { ["a"] = 42 },
            Scores = [42, 7],
            Roles = ["admin", "editor"],
        });
        builder.Services.AddUI(options => options.ScanAssembly(typeof(CrossingPage).Assembly));
        var app = builder.Build();
        app.MapUI();
        await app.StartAsync();
        await using var _ = app;
        var client = app.GetTestClient();

        var html = await client.GetStringAsync(path);
        var served = Regex.Match(html, @"window\.__INITIAL_STATE__\s*=\s*(\{.*?\});", RegexOptions.Singleline);
        served.Success.Should().BeTrue("a page whose state crosses carries it in the document");

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-EQ-Navigate", "1");
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var navigation = await response.Content.ReadAsStringAsync();
        navigation.Should().NotContain(ApiKey).And.NotContain("crossing-tenant");
        using var state = JsonDocument.Parse(navigation);

        return (html, twins.Draw(page, served.Groups[1].Value, state.RootElement.GetProperty("state").GetRawText()));
    }
}
