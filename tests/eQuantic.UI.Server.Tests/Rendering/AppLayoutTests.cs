using System.Net;
using System.Reflection;
using System.Text.Json;
using eQuantic.UI.Primitives;
using eQuantic.UI.Server.Rendering;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace eQuantic.UI.Server.Tests.Rendering;

/// <summary>
/// The app's layout (#1129): declared once with <see cref="UIOptions.UseLayout{TLayout}"/>, refused at
/// the call when it cannot hold a page, named in the client's configuration, and rendered around every
/// write-once page, with every component's server data keyed where the BROWSER's document owner reaches
/// it: measured from the layout at the root, through the slot the page stands in (#1477).
/// <para>
/// The keys asserted here are spelled out on purpose. The browser builds the same tree and computes the
/// same strings (<c>layout-host.spec.ts</c> hands its document the payload under these keys), so a key
/// that moved one step on either side is a component drawing its defaults after hydration, in silence.
/// </para>
/// </summary>
public class AppLayoutTests
{
    /// <summary>The frame of every page: a heading that says where it is, and the page below it.</summary>
    public sealed class ConsoleLayout(VisualNode body) : StatefulComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2);
            column.Add(new Text($"layout at {Navigator.Of(context).Location}", TypeRole.Heading));
            column.Add(body);
            return column;
        }
    }

    /// <summary>A component with a constructor, but none that takes the page.</summary>
    public sealed class NoBodyLayout(string title) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text(title, TypeRole.BodyM);
    }

    /// <summary>A card the page composes, which loads its own number.</summary>
    private sealed class StatsCard : StatelessComponent, IServerPrefetch
    {
        private long _downloads = -1;

        [ServerOnly]
        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            _downloads = 42;
            return Task.CompletedTask;
        }

        public override VisualNode Build(ComponentContext context) =>
            new Text($"Downloads: {_downloads}", TypeRole.BodyM);
    }

    [Page("/layout/stats")]
    public sealed class StatsPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2);
            column.Add(new Text("stats page", TypeRole.BodyM));
            column.Add(new StatsCard());
            return column;
        }
    }

    /// <summary>A page that loads its OWN data, which crossed as <c>Type#0</c> while the page was the root.</summary>
    [Page("/layout/own")]
    public sealed class OwnDataPage : StatelessComponent, IServerPrefetch
    {
        private string _greeting = "(not loaded)";

        [ServerOnly]
        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            _greeting = "loaded";
            return Task.CompletedTask;
        }

        public override VisualNode Build(ComponentContext context) => new Text(_greeting, TypeRole.BodyM);
    }

    private static UIOptions Options(bool layout)
    {
        var options = new UIOptions();
        options.ScanAssembly(typeof(AppLayoutTests).Assembly);
        if (layout) options.UseLayout<ConsoleLayout>();
        return options;
    }

    private static ServerRenderingService Service(UIOptions options) =>
        new(new ServiceCollection().BuildServiceProvider(), options, NullLogger<ServerRenderingService>.Instance);

    private static DefaultHttpContext Request(string path)
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        context.Request.Path = path;
        return context;
    }

    [Fact]
    public void UseLayout_KeepsTheLayout()
    {
        Options(layout: true).Layout.Should().Be(typeof(ConsoleLayout));
        Options(layout: false).Layout.Should().BeNull("an app that declares none renders its pages as they build");
    }

    [Fact]
    public void UseLayout_RefusesAComponentThatCannotTakeThePage_NamingItAndTheConstructor()
    {
        var call = () => new UIOptions().UseLayout<NoBodyLayout>();

        call.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{typeof(NoBodyLayout).FullName}*public NoBodyLayout(VisualNode body)*");
    }

    [Fact]
    public async Task APage_IsRenderedInsideTheLayout_WhichReadsTheRequestsLocation()
    {
        var result = await Service(Options(layout: true)).RenderPageAsync(typeof(StatsPage), Request("/layout/stats"));

        result.Success.Should().BeTrue(result.Error);
        var html = result.Html!;
        html.Should().Contain("layout at /layout/stats",
            "the layout reads Navigator.Location, which the server answers with the request's path");
        html.IndexOf("layout at", StringComparison.Ordinal).Should().BeLessThan(
            html.IndexOf("stats page", StringComparison.Ordinal), "the page's markup is inside the layout's, after its heading");
        html.Should().Contain("Downloads: 42");
    }

    /// <summary>The location is the path the app writes, so a page served under a language segment is
    /// at the path its in-app links name: a layout comparing it with an href it wrote still matches.</summary>
    [Fact]
    public async Task UnderALanguageSegment_TheLocationIsThePathTheAppWrites()
    {
        var options = Options(layout: true).UseCultureRoutes("en", "pt-BR");
        var context = Request("/pt-BR/layout/stats");

        var result = await Service(options).RenderPageAsync(typeof(StatsPage), context);

        result.Success.Should().BeTrue(result.Error);
        result.Html.Should().Contain("layout at /layout/stats",
            "the language segment is not the app's to write in an href");
        result.Html.Should().NotContain("layout at /pt-BR");
    }

    [Fact]
    public async Task WithoutALayout_ThePageIsRenderedAlone()
    {
        var result = await Service(Options(layout: false)).RenderPageAsync(typeof(StatsPage), Request("/layout/stats"));

        result.Success.Should().BeTrue(result.Error);
        result.Html.Should().NotContain("layout at").And.Contain("stats page").And.Contain("Downloads: 42");
    }

    [Fact]
    public async Task AComposedComponentsData_IsKeyedWhereTheDocumentOwnerReachesIt()
    {
        var result = await Service(Options(layout: true)).RenderPageAsync(typeof(StatsPage), Request("/layout/stats"));

        using var payload = JsonDocument.Parse(result.SerializedState!);
        // The layout at the root (``), its column (`0`), the slot it placed second (`0.1`), the page in
        // the slot (`0.1.0`), the page's column (`0.1.0.0`) and the card it placed second.
        var key = $"r0/{Web.ComponentIdentity.Of(typeof(StatsCard))}@0.1.0.0.1";
        payload.RootElement.TryGetProperty(key, out var card).Should().BeTrue(
            $"the browser's document owner names the card {key}; the payload named: {result.SerializedState}");
        card.GetProperty("_downloads").GetString().Should().Be("42");
    }

    [Fact]
    public async Task APagesOwnData_TravelsUnderItsPosition_NeverTheRootsKey()
    {
        var result = await Service(Options(layout: true)).RenderPageAsync(typeof(OwnDataPage), Request("/layout/own"));

        result.Html.Should().Contain("loaded").And.NotContain("(not loaded)");
        using var payload = JsonDocument.Parse(result.SerializedState!);
        var key = $"r0/{Web.ComponentIdentity.Of(typeof(OwnDataPage))}@0.1.0";
        payload.RootElement.GetProperty(key).GetProperty("_greeting").GetString().Should().Be("loaded");
        payload.RootElement.TryGetProperty(Web.ComponentIdentity.Key(typeof(OwnDataPage), 0), out _).Should().BeFalse(
            "under a layout the page is a position of the document's owner, and the browser never names it Type#0");
    }

    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(bool layout)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options =>
        {
            options.ScanAssembly(Assembly.GetExecutingAssembly());
            if (layout) options.UseLayout<ConsoleLayout>();
        });
        var app = builder.Build();
        app.MapUI();
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    [Fact]
    public async Task TheDocument_NamesTheLayoutsModule_AsItNamesThePages()
    {
        var (app, client) = await StartAsync(layout: true);
        await using var _ = app;

        var config = ShellConfig.In(await client.GetStringAsync("/layout/stats"));

        config.GetProperty("layout").GetString().Should().Be(TwinName.OfType(typeof(ConsoleLayout)));
        config.GetProperty("page").GetString().Should().Be(TwinName.OfType(typeof(StatsPage)));
    }

    [Fact]
    public async Task ADocumentOfAnAppWithoutALayout_CarriesNoMemberForIt()
    {
        var (app, client) = await StartAsync(layout: false);
        await using var _ = app;

        var config = ShellConfig.In(await client.GetStringAsync("/layout/stats"));

        config.TryGetProperty("layout", out var __).Should().BeFalse();
    }

    [Fact]
    public async Task ANavigationsData_IsKeyedAsTheFullLoadsIs()
    {
        // The browser builds a page it navigates to under the layout it kept, so the navigation's payload
        // is measured from the layout too, or the card the full load fed would draw its defaults after a link.
        var (app, client) = await StartAsync(layout: true);
        await using var _ = app;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/layout/stats");
        request.Headers.Add("X-EQ-Navigate", "1");
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var navigation = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var key = $"r0/{Web.ComponentIdentity.Of(typeof(StatsCard))}@0.1.0.0.1";
        navigation.RootElement.GetProperty("state").GetProperty(key).GetProperty("_downloads").GetString().Should().Be("42");
    }
}
