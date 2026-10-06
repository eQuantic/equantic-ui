using System.Reflection;
using System.Text.Json;
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

    /// <summary>
    /// The FORMAT culture travels on every page, catalog or not (#471): an app with no resx was
    /// handed no culture at all, so the browser formatted in its host's locale while the server
    /// formatted in the request's, and the SSR markup and the hydrated page printed one number two
    /// ways. The strings travel only when there are some.
    /// </summary>
    [Fact]
    public async Task NoCatalogsAtAll_StillCarriesTheFormatCulture_AndNoStrings()
    {
        var (app, client, _) = await StartAppAsync(withCatalogs: false);
        await using var _1 = app;

        var html = await (await GetWithLanguage(client, "pt-BR")).Content.ReadAsStringAsync();

        html.Should().Contain("<html lang=\"pt-BR\"", "the language is true even with no strings");
        var data = CultureData(html);
        data.GetProperty("formatName").GetString().Should().Be("pt-BR");
        data.TryGetProperty("strings", out _).Should().BeFalse("an app with no resx has no strings to send");
        var format = data.GetProperty("format");
        format.GetProperty("numberFormat").GetProperty("currencySymbol").GetString().Should().Be("R$");
        format.GetProperty("numberFormat").GetProperty("numberDecimalSeparator").GetString().Should().Be(",");
        format.GetProperty("dateTimeFormat").GetProperty("shortDatePattern").GetString().Should().Be("dd/MM/yyyy");
    }

    /// <summary>The format half is the request's FORMAT culture's, written from .NET's own data for
    /// it, beside the catalog the UI culture picks.</summary>
    [Fact]
    public async Task TheFormatHalf_IsTheFormatCulturesOwnData()
    {
        var (app, client, _) = await StartAppAsync();
        await using var _1 = app;

        var data = CultureData(await (await GetWithLanguage(client, "fr")).Content.ReadAsStringAsync());

        var culture = System.Globalization.CultureInfo.GetCultureInfo("fr");
        data.GetProperty("format").GetRawText().Should().Be(eQuantic.UI.Web.CultureFormatBridge.SerializeJson(culture));
        data.GetProperty("format").GetProperty("numberFormat").GetProperty("currencySymbol").GetString().Should().Be("¤",
            "a neutral culture has no currency of its own, and .NET writes the generic ¤ for it");
        data.GetProperty("strings").GetProperty("Strings/Hero.Title").GetString().Should().StartWith("Build products");
    }

    /// <summary>
    /// A culture switch fetches the format half of the culture it switches to: what the shell would
    /// write for a request in it, from the server that renders the next one. A name that is no
    /// culture is not found.
    /// </summary>
    [Fact]
    public async Task TheCultureEndpoint_AnswersAFormatCulturesHalf()
    {
        var (app, client, _) = await StartAppAsync(withCatalogs: false);
        await using var _1 = app;

        var response = await client.GetAsync("/_equantic/culture/de-DE.json");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("formatName").GetString().Should().Be("de-DE");
        document.RootElement.GetProperty("format").GetRawText().Should().Be(
            eQuantic.UI.Web.CultureFormatBridge.SerializeJson(System.Globalization.CultureInfo.GetCultureInfo("de-DE")));
        document.RootElement.GetProperty("format").GetProperty("dateTimeFormat").GetProperty("firstDayOfWeek").GetInt32()
            .Should().Be(1, "a calendar's first day travels in the culture's date data, its names' one source");
        document.RootElement.TryGetProperty("calendar", out _).Should().BeFalse(
            "the calendar's names were a second copy of the format data's, beside it");

        (await client.GetAsync("/_equantic/culture/not-a-culture.json")).StatusCode
            .Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    /// <summary>The object the shell assigns to <c>window.__EQ_CULTURE__</c>.</summary>
    private static JsonElement CultureData(string html)
    {
        const string marker = "window.__EQ_CULTURE__ = ";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        start.Should().BeGreaterThan(0, "every page carries the culture bridge");
        // One JSON value from there, whatever the script writes after it.
        var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(html[(start + marker.Length)..]));
        using var document = JsonDocument.ParseValue(ref reader);
        return document.RootElement.Clone();
    }
}
