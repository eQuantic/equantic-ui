using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using eQuantic.UI.Server.Rendering;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using StatefulComponent = eQuantic.UI.Primitives.StatefulComponent;

namespace eQuantic.UI.Server.Tests.Rendering;

/// <summary>
/// SSR of WRITE-ONCE pages (unification slice 3): a Primitives StatefulComponent with [Page] is a
/// full page — the scan registers it and the render bridges it through the web realizer, producing
/// token-styled HTML with the component's initial state (v1 fence: field defaults, no server-driven
/// initial state).
/// </summary>
public class WriteOncePageSsrTests
{
    [Page("/write-once-test", Title = "Write-once test page")]
    private sealed class WriteOnceTestPage : StatefulComponent
    {
        private int _count;

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2);
            column.Add(new Text($"Count: {_count}", TypeRole.Heading));
            return new Primitives.Box(new BoxStyle
            {
                Padding = EdgeInsets.All(Space.S4),
                Background = context.Theme.Surface,
            }, column);
        }
    }

    /// <summary>The port a page's prefetch reads through — the shape every real loader has.</summary>
    private interface ITestStats
    {
        Task<long> GetAsync(CancellationToken cancellationToken);
    }

    private sealed class TestStats : ITestStats
    {
        public Task<long> GetAsync(CancellationToken cancellationToken) => Task.FromResult(675_617L);
    }

    [Page("/prefetch-test", Title = "Prefetch test page")]
    private sealed class PrefetchTestPage : Primitives.StatelessComponent, IServerPrefetch
    {
        // The field default is what the page states when the prefetch cannot run.
        private long _downloads = 627_000;

        [ServerOnly]
        public async Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
            => _downloads = await services.GetRequiredService<ITestStats>().GetAsync(cancellationToken);

        public override VisualNode Build(ComponentContext context) =>
            new Text($"Downloads: {_downloads}", TypeRole.Heading);
    }

    /// <summary>
    /// A page that TAKES A DEPENDENCY and prefetches — the ordinary shape of a real page, and the
    /// one that used to lose everything. The captured field is an interface the client resolves for
    /// itself, so it belongs nowhere near the payload.
    /// </summary>
    private interface IPageAmbient;

    /// <summary>
    /// What a real ambient looks like to a serializer: it refers back to itself, exactly as an
    /// IHttpContextAccessor reaches a request that reaches it. Writing one throws.
    /// </summary>
    private sealed class RequestAmbient : IPageAmbient
    {
        public RequestAmbient Current => this;
    }

    /// <summary>
    /// The ambient that serializes PERFECTLY, and is the reason dropping-on-failure is not enough:
    /// nothing throws, so nothing is dropped, and a server-side value is published in the HTML of
    /// every page that took the dependency.
    /// </summary>
    private sealed class ConfiguredAmbient : IPageAmbient
    {
        public string ConnectionString => "Server=db;Password=hunter2";
    }

    [Page("/prefetch-with-dependency")]
    private sealed class PrefetchWithDependencyPage(IPageAmbient? ambient = null)
        : Primitives.StatelessComponent, IServerPrefetch
    {
        private readonly IPageAmbient? _injected = ambient;
        private long _downloads = 627_000;

        [ServerOnly]
        public async Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
            => _downloads = await services.GetRequiredService<ITestStats>().GetAsync(cancellationToken);

        public override VisualNode Build(ComponentContext context) =>
            new Text($"Downloads: {_downloads}", TypeRole.Heading);
    }

    /// <summary>
    /// A page that serves MANY documents from one route — a docs slug, a product id — and describes
    /// itself from what it loaded. This is the shape that made the ordering matter.
    /// </summary>
    [Page("/prefetch-metadata-test")]
    private sealed class MetadataFromPrefetchPage
        : Primitives.StatelessComponent, IServerPrefetch, Core.Metadata.IHandleMetadata
    {
        private string _slug = "(not loaded)";

        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            _slug = "getting-started";
            return Task.CompletedTask;
        }

        public void ConfigureMetadata(Core.Metadata.SeoBuilder seo) =>
            seo.Title($"Docs — {_slug}").Canonical($"https://example.test/docs/{_slug}");

        public override VisualNode Build(ComponentContext context) => new Text(_slug, TypeRole.Heading);
    }

    /// <summary>
    /// Metadata is built AFTER the prefetch, and the order is the whole point: a page that serves
    /// many documents can only describe itself from what it loaded. Running ConfigureMetadata first
    /// left every one of them emitting the same canonical — seventy URLs asserting they are one
    /// document, which is worse for a crawler than no canonical at all.
    /// </summary>
    [Fact]
    public async Task Metadata_IsBuiltFromWhatThePrefetchLoaded()
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };

        var result = await CreateService().RenderPageAsync(nameof(MetadataFromPrefetchPage), context);

        result.Success.Should().BeTrue();
        result.Metadata!.Title.Should().Be("Docs — getting-started");
        result.Metadata.RenderTags().Should().Contain("https://example.test/docs/getting-started",
            "the canonical has to describe the document this request served, not the one the page "
            + "knew about before it loaded anything");
    }

    /// <summary>A route that matches every slug, including the ones naming no document.</summary>
    [Page("/status-test")]
    private sealed class MissingContentPage
        : Primitives.StatelessComponent, IServerPrefetch, Primitives.IHandleStatus
    {
        private bool _found;

        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            _found = false;   // the slug named nothing
            return Task.CompletedTask;
        }

        public int StatusCode => _found ? 200 : 404;

        public override VisualNode Build(ComponentContext context) =>
            new Text("Not found", TypeRole.Heading);
    }

    /// <summary>
    /// The page renders the right thing for a READER and must not tell every machine the request
    /// succeeded. A 200 here means a crawler indexes the empty page, a link checker calls the site
    /// healthy, and an uptime probe never notices — the failure is invisible to exactly the things
    /// whose job is to notice.
    /// </summary>
    [Fact]
    public async Task APageThatFoundNothing_AnswersItsOwnStatus()
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };

        var result = await CreateService().RenderPageAsync(nameof(MissingContentPage), context);

        result.Success.Should().BeTrue("it rendered — the content is missing, not the page");
        result.Html.Should().Contain("Not found");
        result.StatusCode.Should().Be(404);
    }

    /// <summary>A page that says nothing about status still answers 200, so this changes nothing
    /// for every page written before it existed.</summary>
    [Fact]
    public async Task APageWithoutAStatus_StillAnswersOk()
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };

        var result = await CreateService().RenderPageAsync(nameof(WriteOnceTestPage), context);

        result.StatusCode.Should().Be(200);
    }

    /// <summary>
    /// The page ONE route serves many documents through — the shape the whole feature exists for.
    /// It reads the slug from its own context, and the prefetch loads BY that slug: nothing here
    /// mentions HTTP, which is the point, because the same page has to build on Photon.
    /// </summary>
    [Page("/docs/{slug}")]
    private sealed class RoutedDocPage : Primitives.StatelessComponent, IServerPrefetch
    {
        private string _loaded = "(nothing)";

        [ServerOnly]
        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            // The route is ambient by now, so a loader can key off it without being handed one.
            _loaded = $"document:{RouteValues.Current.Param("slug")}";
            return Task.CompletedTask;
        }

        public override VisualNode Build(ComponentContext context) =>
            new Text($"{_loaded} page={context.Route.Query("page")}", TypeRole.Heading);
    }

    private static DefaultHttpContext RequestFor(string slug, string page)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };
        context.Request.RouteValues["slug"] = slug;
        context.Request.QueryString = new QueryString($"?page={page}");
        return context;
    }

    /// <summary>
    /// A write-once page reads its own route. Before this the only way there was
    /// <c>IHttpContextAccessor</c>, which works and costs the page everything that makes it
    /// write-once: a component that knows ASP.NET is a component that cannot run on Photon.
    /// </summary>
    [Fact]
    public async Task AWriteOncePage_ReadsItsRoute_WithoutKnowingTheWeb()
    {
        var result = await CreateService().RenderPageAsync(
            nameof(RoutedDocPage), RequestFor("getting-started", "2"));

        result.Success.Should().BeTrue(result.Error);
        result.Html.Should().Contain("page=2", "the query reached Build through the context");
    }

    /// <summary>
    /// And the PREFETCH sees it too, which is the ordering that matters: a page on /docs/{slug}
    /// loads by the slug, so a route arriving after the prefetch arrives after the only question it
    /// was there to answer.
    /// </summary>
    [Fact]
    public async Task ThePrefetch_AlreadyKnowsTheRoute_WhenItRuns()
    {
        var result = await CreateService().RenderPageAsync(
            nameof(RoutedDocPage), RequestFor("getting-started", "1"));

        result.Html.Should().Contain("document:getting-started",
            "the loader keyed off the slug — a null there loads the wrong document, or none");
    }

    /// <summary>
    /// The ambient route does not OUTLIVE its request. SSR renders concurrent requests on shared
    /// state, and a slug left behind is one visitor's document served to the next one.
    /// </summary>
    [Fact]
    public async Task TheRoute_DoesNotSurviveTheRequest()
    {
        await CreateService().RenderPageAsync(nameof(RoutedDocPage), RequestFor("first", "1"));

        RouteValues.Current.Param("slug").Should().BeNull();
    }

    /// <summary>A page rendered with no route at all reads null rather than throwing — every page
    /// written before this one renders unchanged.</summary>
    [Fact]
    public async Task APageWithNoRouteValues_ReadsNull()
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };

        var result = await CreateService().RenderPageAsync(nameof(RoutedDocPage), context);

        result.Success.Should().BeTrue(result.Error);
        result.Html.Should().Contain("document: page=");
    }

    private static ServerRenderingService CreateService(IAppTheme? theme = null)
    {
        var options = new UIOptions();
        options.ScanAssembly(typeof(WriteOncePageSsrTests).Assembly);
        if (theme != null) options.UseTheme(theme);
        return new ServerRenderingService(
            new ServiceCollection().BuildServiceProvider(),
            options,
            NullLogger<ServerRenderingService>.Instance);
    }

    [Fact]
    public async Task RenderPageAsync_RendersAWriteOncePage_WithTokensAndInitialState()
    {
        var service = CreateService();
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };

        var result = await service.RenderPageAsync(nameof(WriteOnceTestPage), context);

        result.Success.Should().BeTrue(result.Error);
        result.Html.Should().Contain("Count: 0", "field defaults are the v1 initial state");
        // ATOMIC pipeline: the markup carries class names; the rules ride an injected style asset
        // (id eq-atomic) the client registry adopts — colors as var(--eq-color-*, resolved-fallback).
        result.Html.Should().Contain("class=\"eq-", "styles become atomic classes");
        result.Html.Should().NotContain("style=\"box-sizing", "regular declarations never stay inline");
        var css = AtomicCss(result);
        css.Should().Contain("background-color:var(--eq-color-surface, light-dark(#ffffff, #14181e))");
        css.Should().Contain("box-sizing:border-box");
    }

    [Fact]
    public async Task RenderPageAsync_AwaitsTheServerPrefetch_BeforeBuildingTheTree()
    {
        var service = CreateService();
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton<ITestStats, TestStats>().BuildServiceProvider(),
        };

        var result = await service.RenderPageAsync(nameof(PrefetchTestPage), context);

        result.Success.Should().BeTrue(result.Error);
        result.Html.Should().Contain("Downloads: 675617",
            "the markup a crawler reads states the LOADED value, not the field default");
    }

    [Fact]
    public async Task RenderPageAsync_CarriesThePrefetchedFields_IntoTheHydrationPayload()
    {
        var service = CreateService();
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton<ITestStats, TestStats>().BuildServiceProvider(),
        };

        var result = await service.RenderPageAsync(nameof(PrefetchTestPage), context);

        // BY FIELD NAME: the transpiled twin declares the identical field, so the client's first
        // render starts from the server's value instead of flashing the default.
        result.SerializedState.Should().NotBeNull()
            .And.Contain("_downloads").And.Contain("675617");
    }

    [Fact]
    public async Task ADependencyDoesNotSilenceTheWholePayload()
    {
        var service = CreateService();
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<ITestStats, TestStats>()
                .AddSingleton<IPageAmbient, RequestAmbient>()
                .BuildServiceProvider(),
        };

        var result = await service.RenderPageAsync(nameof(PrefetchWithDependencyPage), context);

        // One unused constructor dependency used to take the ENTIRE payload with it: the interface
        // field threw, the catch returned null, and the page the server had loaded correctly reset
        // itself to its defaults the instant it hydrated. What the prefetch loaded must survive,
        // and the dependency must not be in there at all — the client resolves that one itself.
        result.SerializedState.Should().NotBeNull()
            .And.Contain("675617").And.NotContain("_injected");
    }

    [Fact]
    public async Task ADependencyNeverRidesThePayload()
    {
        var service = CreateService();
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<ITestStats, TestStats>()
                .AddSingleton<IPageAmbient, ConfiguredAmbient>()
                .BuildServiceProvider(),
        };

        var result = await service.RenderPageAsync(nameof(PrefetchWithDependencyPage), context);

        // This one hurts more than losing the payload, because nothing looks wrong. The ambient
        // writes cleanly, so no guard that only drops FAILURES would catch it, and whatever the
        // service holds is published in the page source. A dependency is not state: the client
        // resolves it, so it is never handed over — serializable or not.
        result.SerializedState.Should().NotBeNull().And.Contain("675617")
            .And.NotContain("_injected").And.NotContain("hunter2");
    }

    [Fact]
    public async Task APageWithoutPrefetch_CarriesNoPayload()
    {
        var service = CreateService();
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };

        var result = await service.RenderPageAsync(nameof(WriteOnceTestPage), context);

        result.SerializedState.Should().BeNull("nothing was loaded — there is nothing to hand over");
    }

    private static string AtomicCss(ServerRenderResult result) =>
        string.Join("\n", result.Assets!.RenderTags()).Should().Contain("id=\"eq-atomic\"", "the SSR injects the collected rules")
            .And.Subject;

    [Fact]
    public void RenderComponent_BridgesAnAbstractTree_ThroughTheWebRealizer()
    {
        var service = CreateService();
        var html = service.RenderComponent(
            new Web.VisualNodeComponent(new Primitives.Text("hello", TypeRole.Caption)));

        html.Should().Contain("eq-type-caption");
        html.Should().Contain("hello");
    }

    [Fact]
    public async Task RenderPageAsync_HonorsTheAppSelectedTheme_LoweringTheSamePageWithMaterialTokens()
    {
        // options.UseTheme(Material) → the SSR pipeline lowers the SAME write-once page with Material's
        // tokens: the M3 baseline Surface (#f7f2fa / #1d1b20), NOT Photon's (#ffffff / #14181e). This is
        // the server half of the bridge — the client half applies the matching __EQ_THEME__ at boot.
        var service = CreateService(Material.MaterialTheme.Instance);
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };

        var result = await service.RenderPageAsync(nameof(WriteOnceTestPage), context);

        result.Success.Should().BeTrue(result.Error);
        var css = AtomicCss(result);
        css.Should().Contain("background-color:var(--eq-color-surface, light-dark(#f7f2fa, #1d1b20))",
            "Material's M3 Surface token, var-referenced");
        css.Should().NotContain("light-dark(#ffffff, #14181e)", "the Photon Surface must be gone");
    }
}
