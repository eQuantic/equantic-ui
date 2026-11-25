using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// A page ships the catalogue strings its modules read, not the whole catalogue (#1782). A real site's
/// shop pages carried the back office's strings in their HTML, and a test could not say a word was absent
/// from a page. eqc writes, beside the catalogues, which strings each page and the layout read
/// (<c>page-strings.json</c>); the server inlines a document's page and layout strings, and a client
/// navigation's payload brings the next page's. A catalogue the build ships whole (the SDK's own) rides
/// every page, and a page the index does not know gets the whole catalogue.
/// </summary>
public class PageStringsTests
{
    [eQuantic.UI.Primitives.Page("/strings-shop")]
    public sealed class StringsShopPage : eQuantic.UI.Primitives.StatelessComponent
    {
        public override eQuantic.UI.Primitives.VisualNode Build(eQuantic.UI.Primitives.ComponentContext context) =>
            new eQuantic.UI.Primitives.Text("Shop", eQuantic.UI.Primitives.TypeRole.BodyM);
    }

    [eQuantic.UI.Primitives.Page("/strings-unindexed")]
    public sealed class StringsUnindexedPage : eQuantic.UI.Primitives.StatelessComponent
    {
        public override eQuantic.UI.Primitives.VisualNode Build(eQuantic.UI.Primitives.ComponentContext context) =>
            new eQuantic.UI.Primitives.Text("Unindexed", eQuantic.UI.Primitives.TypeRole.BodyM);
    }

    public sealed class StringsLayout(eQuantic.UI.Primitives.VisualNode body) : eQuantic.UI.Primitives.StatelessComponent
    {
        public override eQuantic.UI.Primitives.VisualNode Build(eQuantic.UI.Primitives.ComponentContext context) => body;
    }

    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(bool withLayout = false)
    {
        var webRoot = Directory.CreateTempSubdirectory("eq-page-strings-").FullName;
        var equantic = Path.Combine(webRoot, "_equantic");
        Directory.CreateDirectory(Path.Combine(equantic, "strings"));
        File.WriteAllText(Path.Combine(equantic, "strings", "neutral.json"), """
            {"SdkResources/Search":"Search…","Strings/Admin.Orders":"Orders to ship","Strings/Menu.Title":"Menu","Strings/Shop.Title":"Our cakes"}
            """);
        File.WriteAllText(Path.Combine(equantic, "page-strings.json"), """
            {"whole":["SdkResources"],"pages":{"StringsShopPage":["Strings/Shop.Title"],"StringsLayout":["Strings/Menu.Title"]}}
            """);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { WebRootPath = webRoot });
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options =>
        {
            options.EnableSsr = false;
            options.ScanAssembly(Assembly.GetExecutingAssembly());
            if (withLayout) options.UseLayout<StringsLayout>();
        });
        var app = builder.Build();
        app.MapUI();
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    /// <summary>The strings the document inlined, as the shell wrote them in <c>window.__EQ_CULTURE__</c>.</summary>
    private static async Task<JsonElement> InlinedAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        const string marker = "window.__EQ_CULTURE__ = ";
        var start = html.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = html.IndexOf("};", start, StringComparison.Ordinal) + 1;
        return JsonDocument.Parse(html[start..end]).RootElement.GetProperty("strings");
    }

    private static IEnumerable<string> Keys(JsonElement strings) => strings.EnumerateObject().Select(entry => entry.Name);

    [Fact]
    public async Task APage_ShipsTheStringsItReads_AndTheSdksWhole()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        Keys(await InlinedAsync(client, "/strings-shop")).Should().BeEquivalentTo(["Strings/Shop.Title", "SdkResources/Search"],
            "the back office's strings are not the shop page's, and a library's catalogue ships whole");
    }

    [Fact]
    public async Task ThePageAndItsLayout_ShipTheirStrings()
    {
        var (app, client) = await StartAsync(withLayout: true);
        await using var _ = app;

        Keys(await InlinedAsync(client, "/strings-shop")).Should().BeEquivalentTo(
            ["Strings/Shop.Title", "Strings/Menu.Title", "SdkResources/Search"]);
    }

    [Fact]
    public async Task APageTheIndexDoesNotKnow_ShipsTheWholeCatalogue()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        Keys(await InlinedAsync(client, "/strings-unindexed")).Should().HaveCount(4,
            "a page the build did not index keeps working, with every string");
    }

    [Fact]
    public async Task ANavigation_BringsTheNextPagesStrings()
    {
        var (app, client) = await StartAsync(withLayout: true);
        await using var _ = app;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/strings-shop");
        request.Headers.Add("X-EQ-Navigate", "1");
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Keys(payload.GetProperty("strings")).Should().BeEquivalentTo(["Strings/Shop.Title", "SdkResources/Search"],
            "the layout's are already in the browser, and the page's are what the document did not carry");
    }
}
