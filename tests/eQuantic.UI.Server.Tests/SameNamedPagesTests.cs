using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// Two pages with one name in two namespaces are two pages (#514). The server keyed its pages
/// by the simple name, so the one registered last took it and the other's route rendered it,
/// with no error and no log line. Each endpoint carries its page's TYPE now, to the document and
/// to the state a client navigation asks for, and the rendering service holds its pages by type:
/// the two <c>Dashboard</c> pages are routed by their attribute, and the two <c>Report</c> pages by
/// <c>MapPage&lt;T&gt;</c>, whose endpoint is a path of its own.
/// </summary>
public class SameNamedPagesTests
{
    [Theory]
    [InlineData("/same-named/admin", "the admin dashboard", "the shop dashboard")]
    [InlineData("/same-named/shop", "the shop dashboard", "the admin dashboard")]
    [InlineData("/same-named/mapped", "the mapped report", "the reports report")]
    [InlineData("/same-named/reports", "the reports report", "the mapped report")]
    public async Task EachRoute_RendersItsOwnPage(string route, string own, string other)
    {
        await using var app = await StartAsync();

        var html = await app.GetTestClient().GetStringAsync(route);

        html.Should().Contain(own).And.NotContain(other);
    }

    [Theory]
    [InlineData("/same-named/admin", "Admin Dashboard")]
    [InlineData("/same-named/shop", "Shop Dashboard")]
    [InlineData("/same-named/mapped", "Mapped Report")]
    [InlineData("/same-named/reports", "Reports Report")]
    public async Task ANavigationToEachRoute_CarriesItsOwnPagesState(string route, string title)
    {
        await using var app = await StartAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Add("X-EQ-Navigate", "1");
        var response = await app.GetTestClient().SendAsync(request);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        payload.GetProperty("title").GetString().Should().Be(title);
    }

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            WebRootPath = Directory.CreateTempSubdirectory("eq-same-named-").FullName,
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options =>
        {
            options.EnableSsr = true;
            options.ScanAssembly(typeof(SameNamedPagesTests).Assembly);
        });
        var app = builder.Build();
        app.MapPage<SameNamedPages.Mapped.Report>("/same-named/mapped");
        app.MapPage<SameNamedPages.Reports.Report>("/same-named/reports");
        app.MapUI();
        await app.StartAsync();
        return app;
    }
}
