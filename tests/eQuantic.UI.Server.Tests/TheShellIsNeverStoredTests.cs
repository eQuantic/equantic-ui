using System.Net;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The served document is never stored (#1288): it carries what the visitor's cookies chose (theme,
/// density, culture) and the page's own state, so a cache between the browser and the server that
/// kept it would hand one visitor's data to the next. It was sent with no <c>Cache-Control</c>,
/// while the same state answered to a navigation was already <c>no-store</c>.
/// </summary>
public class TheShellIsNeverStoredTests
{
    private static async Task<HttpResponseMessage> Get(bool ssr, string path, bool navigation = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options =>
        {
            options.ScanAssembly(Assembly.GetExecutingAssembly());
            options.EnableSsr = ssr;
        });
        var app = builder.Build();
        app.MapUI();
        await app.StartAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (navigation) request.Headers.Add(UIExtensions.NavigationHeader, "1");
        var response = await app.GetTestClient().SendAsync(request);
        await app.DisposeAsync();
        return response;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task APage_IsSentNeverStored(bool ssr)
    {
        var response = await Get(ssr, "/one-pass");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        response.Headers.CacheControl!.NoStore.Should().BeTrue(
            "the document carries this visitor's choices and, rendered on the server, the page's state");
    }

    [Fact]
    public async Task APathThatMatchesNothing_IsSentNeverStored()
    {
        var response = await Get(ssr: true, "/no-page-has-this-path");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.CacheControl!.NoStore.Should().BeTrue("the not-found document is the same shell");
    }

    [Fact]
    public async Task ANavigationsState_IsSentNeverStored()
    {
        var response = await Get(ssr: true, "/one-pass", navigation: true);

        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        response.Headers.CacheControl!.NoStore.Should().BeTrue("the page's data is this visitor's");
    }
}
