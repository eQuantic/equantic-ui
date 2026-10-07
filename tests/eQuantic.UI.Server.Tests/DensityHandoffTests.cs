using System.Reflection;
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

    private static async Task<string> GetAsync(string? cookie)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options => options.ScanAssembly(Assembly.GetExecutingAssembly()));
        await using var app = builder.Build();
        app.MapUI();
        await app.StartAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "/density");
        if (cookie is not null) request.Headers.Add("Cookie", cookie);
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
