using eQuantic.UI.Core.Metadata;
using eQuantic.UI.Server;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The translation group in the head. A localized site that never says which URL is which language
/// is a site a search engine indexes as duplicates of one another — and the failure is silent, so
/// the rules the standard imposes are pinned here rather than trusted.
/// </summary>
public class AlternateLinkTests
{
    private static HttpRequest Request(string path, string host = "equantic.tech", string scheme = "https")
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(host);
        context.Request.Path = path;
        return context.Request;
    }

    /// <summary>One page has ONE canonical and one alternate PER LANGUAGE. Keying a link by `rel`
    /// alone collapsed the group into its last member, which says the page has no translations.</summary>
    [Fact]
    public void EveryLanguageKeepsItsOwnLink()
    {
        var metadata = new MetadataCollection();
        var seo = new SeoBuilder(metadata);

        seo.Canonical("https://equantic.tech/pricing");
        seo.Alternate("en", "https://equantic.tech/en/pricing");
        seo.Alternate("pt-BR", "https://equantic.tech/pt-BR/pricing");
        seo.AlternateDefault("https://equantic.tech/en/pricing");

        var rendered = metadata.RenderTags();
        rendered.Should().Contain("<link rel=\"alternate\" hreflang=\"en\" href=\"https://equantic.tech/en/pricing\">");
        rendered.Should().Contain("hreflang=\"pt-BR\"");
        rendered.Should().Contain("hreflang=\"x-default\"");
        rendered.Should().Contain("<link rel=\"canonical\"");
        metadata.Tags.Count(tag => tag is LinkTag { Rel: "alternate" }).Should().Be(3);
    }

    /// <summary>Writing the same language twice REPLACES it — the page's own word over the app's
    /// default, which is what makes an app-wide policy safe to turn on.</summary>
    [Fact]
    public void APageOverridesTheAppsAnswerForOneLanguage()
    {
        var metadata = new MetadataCollection();
        var seo = new SeoBuilder(metadata);

        seo.Alternate("pt-BR", "https://equantic.tech/pt-BR/pricing");
        seo.Alternate("pt-BR", "https://equantic.tech/precos");

        metadata.Tags.OfType<LinkTag>().Should().ContainSingle()
            .Which.Href.Should().Be("https://equantic.tech/precos");
    }

    /// <summary>The culture is the first SEGMENT, and the one already there is replaced rather than
    /// stacked: a reader on the pt-BR page must still get the right link to the en one.</summary>
    [Theory]
    [InlineData("/pricing", "https://equantic.tech/pt-BR/pricing")]
    [InlineData("/en/pricing", "https://equantic.tech/pt-BR/pricing")]
    [InlineData("/pt-BR/pricing", "https://equantic.tech/pt-BR/pricing")]
    [InlineData("/", "https://equantic.tech/pt-BR")]
    public void ThePathPrefixPolicySwapsTheSegment(string path, string expected)
    {
        var policy = AlternateUrls.PathPrefix();

        policy(new AlternateRequest("pt-BR", Request(path))).Should().Be(expected);
    }

    /// <summary>A page whose own name looks nothing like a culture keeps it: the shape test must
    /// not eat a segment the site needs.</summary>
    [Fact]
    public void APageSegmentIsNotMistakenForALanguage()
    {
        AlternateUrls.PathPrefix()(new AlternateRequest("es", Request("/docs/getting-started")))
            .Should().Be("https://equantic.tech/es/docs/getting-started");
    }

    /// <summary>The query policy keeps the rest of the query and replaces only its own key.</summary>
    [Fact]
    public void TheQueryPolicyReplacesOnlyItsOwnParameter()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("equantic.tech");
        context.Request.Path = "/pricing";
        context.Request.QueryString = new QueryString("?plan=team&culture=en");

        AlternateUrls.QueryString()(new AlternateRequest("pt-BR", context.Request))
            .Should().Be("https://equantic.tech/pricing?plan=team&culture=pt-BR");
    }

    /// <summary>A relative answer from an app's own lambda is made ABSOLUTE: a relative hreflang is
    /// the mistake that looks right in the markup and is dropped by every crawler.</summary>
    [Fact]
    public void ARelativeAnswerBecomesAbsolute()
    {
        AlternateUrls.Absolute(Request("/pricing"), "/es/pricing")
            .Should().Be("https://equantic.tech/es/pricing");
        AlternateUrls.Absolute(Request("/pricing"), "https://es.equantic.tech/pricing")
            .Should().Be("https://es.equantic.tech/pricing");
    }

    private sealed class PricingPage : eQuantic.UI.Primitives.StatelessComponent
    {
        public override eQuantic.UI.Primitives.VisualNode Build(eQuantic.UI.Primitives.ComponentContext context) =>
            new eQuantic.UI.Primitives.Text("Pricing", eQuantic.UI.Primitives.TypeRole.Heading);
    }

    /// <summary>A page whose Portuguese translation lives at a slug the policy could never guess.</summary>
    private sealed class SlugPage : eQuantic.UI.Primitives.StatelessComponent, IHandleMetadata
    {
        public override eQuantic.UI.Primitives.VisualNode Build(eQuantic.UI.Primitives.ComponentContext context) =>
            new eQuantic.UI.Primitives.Text("Pricing", eQuantic.UI.Primitives.TypeRole.Heading);

        public void ConfigureMetadata(SeoBuilder seo) =>
            seo.Alternate("pt-BR", "https://acme.test/precos");
    }

    /// <summary>
    /// The same thing over the REAL pipeline, because every unit above passed while the head came
    /// out empty: the app wires negotiation with the lambda overload every sample uses, which
    /// registers nothing in the container, so a feature that asked DI for the language list emitted
    /// nothing at all and said nothing about it.
    /// </summary>
    private static async Task<(WebApplication App, HttpClient Client)> StartAppAsync(
        Action<UIOptions> configure, bool shareOptionsThroughDi = false, bool ssr = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            WebRootPath = Directory.CreateTempSubdirectory("eq-hreflang-").FullName,
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options =>
        {
            options.EnableSsr = ssr;
            options.ScanAssembly(typeof(AlternateLinkTests).Assembly);
            configure(options);
        });
        if (shareOptionsThroughDi)
            builder.Services.Configure<Microsoft.AspNetCore.Builder.RequestLocalizationOptions>(options =>
            {
                options.AddSupportedUICultures("en", "pt-BR", "es");
                options.AddSupportedCultures("en", "pt-BR", "es");
                options.SetDefaultCulture("pt-BR");
            });

        var app = builder.Build();
        if (shareOptionsThroughDi) app.UseRequestLocalization();
        else
            app.UseRequestLocalization(options =>
            {
                options.AddSupportedUICultures("en", "pt-BR", "es");
                options.AddSupportedCultures("en", "pt-BR", "es");
            });
        app.MapPage<PricingPage>("/pricing", title: "Pricing");
        app.MapPage<SlugPage>("/slug", title: "Slug");
        app.MapUI();
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    private static IReadOnlyList<string> Alternates(string html)
    {
        var head = html[..html.IndexOf("</head>", StringComparison.Ordinal)];
        return System.Text.RegularExpressions.Regex
            .Matches(head, "<link rel=\"alternate\" hreflang=\"([^\"]+)\" href=\"([^\"]+)\">")
            .Select(match => $"{match.Groups[1].Value} {match.Groups[2].Value}")
            .ToList();
    }

    [Fact]
    public async Task TheAppNamesItsLanguagesAndEveryPageAdvertisesTheWholeGroup()
    {
        var (app, client) = await StartAppAsync(options =>
            options.UseAlternateLinks(AlternateUrls.PathPrefix(), "en", "pt-BR", "es"));
        await using var _ = app;

        var links = Alternates(await client.GetStringAsync("/pricing"));

        links.Should().BeEquivalentTo([
            "en http://localhost/en/pricing",
            "pt-BR http://localhost/pt-BR/pricing",
            "es http://localhost/es/pricing",
            "x-default http://localhost/en/pricing",
        ], "the group includes the page ITSELF, and x-default takes the first language named");
    }

    /// <summary>Reciprocity is the rule crawlers actually enforce: the pt-BR page must advertise the
    /// SAME set the en one does, or the group is discarded whole.</summary>
    [Fact]
    public async Task EveryTranslationAdvertisesTheIdenticalSet()
    {
        var (app, client) = await StartAppAsync(options =>
            options.UseAlternateLinks(AlternateUrls.QueryString(), "en", "pt-BR", "es"));
        await using var _ = app;

        var english = Alternates(await client.GetStringAsync("/pricing?culture=en"));
        var portuguese = Alternates(await client.GetStringAsync("/pricing?culture=pt-BR"));

        portuguese.Should().BeEquivalentTo(english);
        portuguese.Should().Contain("pt-BR http://localhost/pricing?culture=pt-BR",
            "a page that omits ITSELF from the group invalidates the whole group");
    }

    /// <summary>An app that shares its localization options through DI keeps ONE list: the languages
    /// and the default both come from the middleware it already configured.</summary>
    [Fact]
    public async Task SharedLocalizationOptionsNeedNoSecondList()
    {
        var (app, client) = await StartAppAsync(
            options => options.UseAlternateLinks(AlternateUrls.QueryString()),
            shareOptionsThroughDi: true);
        await using var _ = app;

        var links = Alternates(await client.GetStringAsync("/pricing"));

        links.Should().HaveCount(4);
        links.Should().Contain("x-default http://localhost/pricing?culture=pt-BR",
            "x-default follows the app's OWN default culture when it declared one");
    }

    /// <summary>
    /// The app-wide policy is a DEFAULT. A page whose Portuguese translation is a different slug
    /// says so, wins for that language by key, and leaves the rest of the group standing — which is
    /// what makes turning the policy on safe for a site with a handful of translated URLs.
    /// </summary>
    [Fact]
    public async Task APageOverridesOneLanguageAndKeepsTheGroup()
    {
        // SSR ON: a page's own metadata reaches the head through the render result, so this is the
        // only wiring in which the override can be observed at all.
        var (app, client) = await StartAppAsync(options =>
            options.UseAlternateLinks(AlternateUrls.PathPrefix(), "en", "pt-BR", "es"), ssr: true);
        await using var _ = app;

        var links = Alternates(await client.GetStringAsync("/slug"));

        links.Should().Contain("pt-BR https://acme.test/precos");
        links.Should().Contain("en http://localhost/en/slug", "the other languages keep the policy");
        links.Should().HaveCount(4, "an override REPLACES its language, it does not add one");
    }

    [Fact]
    public async Task AnAppThatDeclaredNoPolicySaysNothing()
    {
        var (app, client) = await StartAppAsync(_ => { });
        await using var _1 = app;

        Alternates(await client.GetStringAsync("/pricing")).Should().BeEmpty();
    }
}
