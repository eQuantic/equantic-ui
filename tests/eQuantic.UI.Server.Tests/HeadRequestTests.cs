using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// A HEAD is a GET without the content (RFC 9110, section 9.3.2), so a page and a module answer it
/// as they answer the GET, with no body (#575). The page and module routes were mapped for GET
/// alone, so a HEAD fell through to the fallback: an uptime monitor, a link checker or a crawler that
/// asks with HEAD read every page as missing. This runs on Kestrel, the server an app ships on,
/// because leaving a HEAD's body out is the server's part, which the test host does not play.
/// </summary>
public class HeadRequestTests
{
    [Theory]
    [InlineData("/same-named/admin")] // a [Page] route
    [InlineData("/same-named/mapped")] // a MapPage<T> route
    [InlineData("/_equantic/runtime.js")] // the runtime
    [InlineData("/_equantic/Probe.js")] // an app's module
    public async Task AHead_AnswersAsTheGetDoes_WithNoBody(string route)
    {
        await using var app = await StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        using var get = await client.GetAsync(route);
        using var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, route));

        get.StatusCode.Should().Be(HttpStatusCode.OK);
        head.StatusCode.Should().Be(HttpStatusCode.OK);
        head.Content.Headers.ContentType.Should().Be(get.Content.Headers.ContentType);
        head.Headers.CacheControl.Should().Be(get.Headers.CacheControl);
        (await get.Content.ReadAsByteArrayAsync()).Should().NotBeEmpty();
        (await head.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task AHeadForNoPage_IsStillNotFound()
    {
        await using var app = await StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        using var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/no-such-page"));

        head.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<WebApplication> StartAsync()
    {
        var webRoot = Directory.CreateTempSubdirectory("eq-head-").FullName;
        Directory.CreateDirectory(Path.Combine(webRoot, "_equantic"));
        await File.WriteAllTextAsync(Path.Combine(webRoot, "_equantic", "Probe.js"), "export const probe = 1;\n");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { WebRootPath = webRoot });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddUI(options =>
        {
            options.EnableSsr = true;
            options.ScanAssembly(typeof(HeadRequestTests).Assembly);
        });
        var app = builder.Build();
        app.MapPage<SameNamedPages.Mapped.Report>("/same-named/mapped");
        app.MapUI();
        await app.StartAsync();
        return app;
    }
}
