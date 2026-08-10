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
