using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using eQuantic.UI.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The density a served page is built at (#623). The server cannot see the pointer and rendered every
/// page Comfortable, the client lowered Compact under a mouse, and hydration kept the served markup:
/// each component showed the server's density until it next re-rendered and then snapped, so a page
/// showed a mix of both. The runtime now leaves its density in a session cookie, the server renders at
/// it, and the page's configuration says which density it was rendered at, so hydration lowers at that
/// one and switches the whole page at once when it is not the browser's.
/// </summary>
public class DensityHandoffTests
{
    /// <summary>A page that draws the density it is built at as a width: 20 compact, 22 comfortable,
    /// the two selection boxes of the issue.</summary>
    [Page("/density")]
    public sealed class DensityPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            new Box(new BoxStyle { Width = context.Density == Density.Compact ? 20 : 22, Height = 10 });
    }

    /// <summary>An escape-hatch page composing a write-once bridge of its own (routed with MapPage, as an
    /// escape-hatch page is): the bridge is built at the request's density too, or hydration lowers it at a
    /// density its markup was not built at.</summary>
    public sealed class BridgePage : eQuantic.UI.Web.HtmlElement
    {
        public BridgePage() => AddChild(new eQuantic.UI.Web.VisualNodeComponent(new DensityPage()));

        public override eQuantic.UI.Web.HtmlNode Render()
            => new() { Tag = "div", Children = Children.Select(child => child.Render()).ToList() };
    }

    /// <summary>What a page composes at compact density, as a dense table is to a card list.</summary>
    public sealed class CompactRows : StatelessComponent, IServerPrefetch
    {
        public string Loaded = "";

        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            Loaded = "compact rows";
            return Task.CompletedTask;
        }

        public override VisualNode Build(ComponentContext context) => new Text(Loaded, TypeRole.BodyM);
    }

    /// <summary>What the same page composes at comfortable density.</summary>
    public sealed class ComfortableCards : StatelessComponent, IServerPrefetch
    {
        public string Loaded = "";

        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            Loaded = "comfortable cards";
            return Task.CompletedTask;
        }

        public override VisualNode Build(ComponentContext context) => new Text(Loaded, TypeRole.BodyM);
    }

    /// <summary>A page that composes by its density, which every component can read.</summary>
    [Page("/density-branch")]
    public sealed class DensityBranchPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            context.Density == Density.Compact ? new CompactRows() : new ComfortableCards();
    }

    private static async Task<string> GetAsync(string? cookie, string path = "/density", bool navigate = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options => options.ScanAssembly(Assembly.GetExecutingAssembly()));
        await using var app = builder.Build();
        app.MapPage<BridgePage>("/density-bridge");
        app.MapUI();
        await app.StartAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (cookie is not null) request.Headers.Add("Cookie", cookie);
        if (navigate) request.Headers.Add("X-EQ-Navigate", "1");
        var response = await app.GetTestClient().SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task WithoutTheCookie_ThePageIsComfortable()
    {
        var html = await GetAsync(cookie: null);

        html.Should().Contain("width:22px").And.NotContain("width:20px");
        ShellConfig.In(html).GetProperty("density").GetString().Should().Be("comfortable");
    }

    [Fact]
    public async Task TheBrowsersDensity_IsTheOneThePageIsBuiltAt()
    {
        var html = await GetAsync("eq-density=compact");

        html.Should().Contain("width:20px").And.NotContain("width:22px");
        ShellConfig.In(html).GetProperty("density").GetString().Should().Be("compact",
            "hydration lowers at the density the markup was built at");
    }

    [Fact]
    public async Task ABridgeAnEscapeHatchPageComposes_IsBuiltAtTheRequestsDensity()
    {
        var html = await GetAsync("eq-density=compact", "/density-bridge");

        html.Should().Contain("width:20px").And.NotContain("width:22px");
        ShellConfig.In(html).GetProperty("density").GetString().Should().Be("compact");
    }

    /// <summary>A client navigation finds its server data in the tree the browser builds, at the
    /// browser's density. The walk that discovers it expanded the page comfortable, so a page that
    /// composes by density loaded the branch the browser does not build, and the one it builds showed
    /// its empty state (found by Copilot on #688).</summary>
    [Fact]
    public async Task AClientNavigation_FindsItsDataAtTheBrowsersDensity()
    {
        var state = JsonDocument.Parse(await GetAsync("eq-density=compact", "/density-branch", navigate: true))
            .RootElement.GetProperty("state");

        state.TryGetProperty(eQuantic.UI.Web.ComponentIdentity.Key(typeof(CompactRows), 0), out var rows)
            .Should().BeTrue("the browser builds the compact branch");
        rows.GetProperty("loaded").GetString().Should().Be("compact rows");
        state.TryGetProperty(eQuantic.UI.Web.ComponentIdentity.Key(typeof(ComfortableCards), 0), out _)
            .Should().BeFalse("nothing in the browser's tree asks for it");
    }

    [Fact]
    public async Task AnUnknownValue_IsIgnored()
    {
        var html = await GetAsync("eq-density=<script>");

        html.Should().Contain("width:22px");
        ShellConfig.In(html).GetProperty("density").GetString().Should().Be("comfortable");
    }

    /// <summary>The browser writes the cookie the server reads: one name, pinned across the two
    /// languages, or the server reads a cookie nobody writes and every load switches.</summary>
    [Fact]
    public void TheCookie_IsTheOneTheRuntimeWrites()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;
        var markers = File.ReadAllText(Path.Combine(here!.FullName, "src", "eQuantic.UI.Runtime", "src", "shared", "markers.ts"));
        var match = Regex.Match(markers, @"export\s+const\s+DENSITY_COOKIE\s*=\s*['""]([^'""]+)['""]");

        match.Success.Should().BeTrue();
        match.Groups[1].Value.Should().Be(DensityCookie.Name);
    }
}
