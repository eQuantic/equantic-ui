using System.Net;
using eQuantic.UI.Server;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The tab's icon at the address every browser and crawler asks for (#1240): <c>/favicon.ico</c>,
/// answered from the <c>favicon.ico</c> the build wrote from the app's icon, and linked from every
/// page. An app's own file wins, a referenced project's static web asset included, and with neither
/// the answer is 404.
/// </summary>
public class FaviconTests
{
    private static readonly byte[] Ico = [0, 0, 1, 0, 1, 0, 16, 16, 0, 0, 1, 0, 32, 0, 0, 0, 0, 0, 22, 0, 0, 0];

    private static async Task<(WebApplication App, HttpClient Client)> StartAppAsync(bool generated, bool own = false,
        bool asset = false)
    {
        var webRoot = Directory.CreateTempSubdirectory("eq-favicon-").FullName;
        if (generated)
        {
            var icons = Path.Combine(webRoot, "_equantic", "icons");
            Directory.CreateDirectory(icons);
            File.WriteAllBytes(Path.Combine(icons, "favicon.ico"), Ico);
            File.WriteAllBytes(Path.Combine(icons, "apple-touch-icon.png"), [1]);
        }
        if (own) File.WriteAllBytes(Path.Combine(webRoot, "favicon.ico"), [7, 7, 7]);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { WebRootPath = webRoot });
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options => options.EnableSsr = false);

        var app = builder.Build();
        if (asset)
        {
            // What static web assets do in development: a referenced project's wwwroot joins the web
            // root's file provider, with no file under the app's own folder.
            var library = Directory.CreateTempSubdirectory("eq-favicon-library-").FullName;
            File.WriteAllBytes(Path.Combine(library, "favicon.ico"), [9, 9]);
            app.Environment.WebRootFileProvider = new Microsoft.Extensions.FileProviders.CompositeFileProvider(
                app.Environment.WebRootFileProvider, new Microsoft.Extensions.FileProviders.PhysicalFileProvider(library));
        }
        app.UseStaticFiles();
        app.MapUI();
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    [Fact]
    public async Task TheRootsFavicon_IsTheOneTheBuildWrote()
    {
        var (app, client) = await StartAppAsync(generated: true);
        await using var _ = app;

        var response = await client.GetAsync("/favicon.ico");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/x-icon");
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(Ico);
        response.Headers.CacheControl!.MaxAge.Should().Be(TimeSpan.FromDays(1),
            "the address never changes and the icon may, so it is not immutable");
    }

    [Fact]
    public async Task EveryPage_LinksTheFavicon_AtTheRoot_WithItsSizes()
    {
        var (app, client) = await StartAppAsync(generated: true);
        await using var _ = app;

        // An app with no pages answers "/" with the not-found shell, whose head is every page's.
        var html = await (await client.GetAsync("/")).Content.ReadAsStringAsync();

        html.Should().Contain("""<link rel="icon" href="/favicon.ico" sizes="16x16 32x32 48x48">""");
        html.Should().Contain("""<link rel="apple-touch-icon" href="/_equantic/icons/apple-touch-icon.png">""");
        html.Should().NotContain("favicon-32.png");
    }

    [Fact]
    public async Task AnAppsOwnFavicon_Wins_AndIsLinkedWithoutTheBuiltFilesSizes()
    {
        var (app, client) = await StartAppAsync(generated: true, own: true);
        await using var _ = app;

        (await client.GetByteArrayAsync("/favicon.ico")).Should().Equal([7, 7, 7]);
        (await (await client.GetAsync("/")).Content.ReadAsStringAsync())
            .Should().Contain("""<link rel="icon" href="/favicon.ico">""", "the sizes are the built file's, not the app's");
    }

    /// <summary>
    /// A favicon a referenced project serves as a static web asset (myartwork3d's Theme project does):
    /// under minimal hosting the route runs before UseStaticFiles, so it reads the same file provider.
    /// </summary>
    [Fact]
    public async Task AFaviconAReferencedProjectServes_Wins_WithNoIconOfTheApps()
    {
        var (app, client) = await StartAppAsync(generated: false, asset: true);
        await using var _ = app;

        (await client.GetByteArrayAsync("/favicon.ico")).Should().Equal([9, 9]);
    }

    [Fact]
    public async Task WithNoIconAndNoFile_TheFaviconIsNotFound()
    {
        var (app, client) = await StartAppAsync(generated: false);
        await using var _ = app;

        var response = await client.GetAsync("/favicon.ico");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
        (await (await client.GetAsync("/")).Content.ReadAsStringAsync()).Should().NotContain("rel=\"icon\"");
    }
}
