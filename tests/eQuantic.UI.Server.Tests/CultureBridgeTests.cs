using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// Track L D4/D5 over the REAL pipeline: culture selection is ASP.NET's
/// (<c>UseRequestLocalization</c>, wired by the APP), and the shell carries the answer —
/// <c>&lt;html lang&gt;</c> from the request's UI culture, and <c>window.__EQ_CULTURE__</c>
/// inlining the catalog that culture resolves to (exact → parent → neutral, a FILE pick because
/// the chain was flattened at build time).
/// </summary>
public class CultureBridgeTests
{
    private static async Task<(WebApplication App, HttpClient Client, string WebRoot)> StartAppAsync(
        bool withCatalogs = true)
    {
        var webRoot = Directory.CreateTempSubdirectory("eq-culture-").FullName;
        if (withCatalogs)
        {
            var strings = Path.Combine(webRoot, "_equantic", "strings");
            Directory.CreateDirectory(strings);
            File.WriteAllText(Path.Combine(strings, "neutral.json"),
                """{"Strings/Hero.Title":"Build products, not plumbing."}""");
            File.WriteAllText(Path.Combine(strings, "pt-BR.json"),
                """{"Strings/Hero.Title":"Construa produtos, não encanamento."}""");
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { WebRootPath = webRoot });
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options => options.EnableSsr = false);

        var app = builder.Build();
        // The APP wires negotiation (D5) — the SDK only reads the statics the middleware set.
        app.UseRequestLocalization(options =>
        {
            options.AddSupportedUICultures("pt-BR", "fr");
            options.AddSupportedCultures("pt-BR", "fr");
        });
        app.MapUI();
        await app.StartAsync();
        return (app, app.GetTestClient(), webRoot);
    }

    private static Task<HttpResponseMessage> GetWithLanguage(HttpClient client, string acceptLanguage)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Accept-Language", acceptLanguage);
        return client.SendAsync(request);
    }

    [Fact]
    public async Task TheShellSpeaksTheRequestsCulture()
    {
        var (app, client, _) = await StartAppAsync();
        await using var _1 = app;

        var html = await (await GetWithLanguage(client, "pt-BR")).Content.ReadAsStringAsync();

        html.Should().Contain("<html lang=\"pt-BR\"",
            "assistive tech picks pronunciation from the document language");
        html.Should().Contain("window.__EQ_CULTURE__ = {\"name\":\"pt-BR\"");
        html.Should().Contain("Construa produtos",
            "the ACTIVE culture's catalog rides the shell — first paint needs no fetch");
    }

    [Fact]
    public async Task ACultureWithoutItsOwnCatalog_FallsBackToNeutral_ButKeepsItsName()
    {
        var (app, client, _) = await StartAppAsync();
        await using var _1 = app;

        var html = await (await GetWithLanguage(client, "fr")).Content.ReadAsStringAsync();

        html.Should().Contain("<html lang=\"fr\"");
        html.Should().Contain("\"name\":\"fr\"");
        html.Should().Contain("Build products",
            "no fr.json exists — the neutral catalog answers, exactly as ResourceManager would");
    }

    [Fact]
    public async Task NoCatalogsAtAll_MeansNoCultureBridge_AndTheShellStillServes()
    {
        var (app, client, _) = await StartAppAsync(withCatalogs: false);
        await using var _1 = app;

        var html = await (await GetWithLanguage(client, "pt-BR")).Content.ReadAsStringAsync();

        html.Should().Contain("<html lang=\"pt-BR\"", "the language is true even with no strings");
        html.Should().NotContain("__EQ_CULTURE__",
            "an app with no resx pays nothing for the bridge");
    }
}
