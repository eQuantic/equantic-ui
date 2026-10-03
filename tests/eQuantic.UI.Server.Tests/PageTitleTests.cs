using System.Net;
using System.Reflection;
using System.Text.Json;
using eQuantic.UI.Primitives;
using eQuantic.UI.Server.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The document's title and description, as a route declares them and a page says them: the page's
/// own words over its route's, and its route's over the app's, the same on a full load and on a client
/// navigation.
/// <para>
/// The route's title was applied only when the metadata had none, which the app's default title always
/// filled, so no page ever got it, and a navigation answered with the app's title over the one the
/// client had just set from its route table (#416). And the client's configuration quoted each title by
/// hand, so one holding a line break was a syntax error in the script every page carries, and stopped
/// the client of the whole app (#526).
/// </para>
/// </summary>
public class PageTitleTests
{
    [Page("/titled", Title = "Parity", Description = "The parity page")]
    public sealed class ParityTitledPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("titled", TypeRole.BodyM);
    }

    /// <summary>Two routes, each with its own title, one of them holding a line break.</summary>
    [Page("/orders", Title = "Orders")]
    [Page("/orders/admin", Title = "Orders\nAdmin")]
    public sealed class OrdersTitledPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("orders", TypeRole.BodyM);
    }

    [Page("/spoken", Title = "Static", Description = "Static words")]
    public sealed class SpokenTitledPage : StatelessComponent, IHandleMetadata
    {
        public void ConfigureMetadata(SeoBuilder seo) => seo.Title("Dynamic").Description("Dynamic words");

        public override VisualNode Build(ComponentContext context) => new Text("spoken", TypeRole.BodyM);
    }

    [Page("/untitled")]
    public sealed class UntitledPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("untitled", TypeRole.BodyM);
    }

    /// <summary>A page the server never renders, whose route still says what it is.</summary>
    [Page("/client-only", Title = "Client only", Description = "Drawn in the browser", DisableSsr = true)]
    public sealed class ClientOnlyPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("client", TypeRole.BodyM);
    }

    [Page("/scripted", Title = "</script><b>x")]
    public sealed class ScriptTitledPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("scripted", TypeRole.BodyM);
    }

    private static async Task<(WebApplication App, HttpClient Client)> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options =>
        {
            options.ScanAssembly(Assembly.GetExecutingAssembly());
            options.ConfigureHtmlShell(shell => shell.SetTitle("The App").AddDescription("The app's words"));
            options.UseThemeCookie("eq'theme");
        });
        var app = builder.Build();
        app.MapUI();
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    private static async Task<JsonElement> NavigateAsync(HttpClient client, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-EQ-Navigate", "1");
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    [Theory]
    [InlineData("/titled", "Parity")]
    [InlineData("/orders", "Orders")]
    [InlineData("/orders/admin", "Orders\nAdmin")]
    [InlineData("/spoken", "Dynamic")]
    [InlineData("/untitled", "The App")]
    public async Task TheTitle_IsThePagesOwn_ThenItsRoutes_ThenTheApps_OnALoadAndANavigation(string path, string title)
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        var html = await client.GetStringAsync(path);
        var navigation = await NavigateAsync(client, path);

        html.Should().Contain($"<title>{System.Web.HttpUtility.HtmlEncode(title)}</title>");
        navigation.GetProperty("title").GetString().Should().Be(title);
    }

    [Theory]
    [InlineData("/titled", "The parity page")]
    [InlineData("/spoken", "Dynamic words")]
    [InlineData("/untitled", "The app&#39;s words")]
    public async Task TheDescription_FollowsTheSameOrder(string path, string description)
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        var html = await client.GetStringAsync(path);
        var head = (await NavigateAsync(client, path)).GetProperty("head").GetString();

        html.Should().Contain($"<meta name=\"description\" content=\"{description}\"");
        head.Should().Contain($"<meta name=\"description\" content=\"{description}\"",
            "a navigation's head replaces the previous page's description, or keeps a stale one");
    }

    [Fact]
    public async Task TheClientConfiguration_IsJson_AndCarriesEveryStringAsWritten()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        var html = await client.GetStringAsync("/untitled");
        var config = ShellConfig.In(html);

        var titles = config.GetProperty("routes").EnumerateArray()
            .Where(route => route.TryGetProperty("title", out var _))
            .ToDictionary(route => route.GetProperty("pattern").GetString()!, route => route.GetProperty("title").GetString());
        titles["/orders/admin"].Should().Be("Orders\nAdmin");
        titles["/scripted"].Should().Be("</script><b>x");
        config.GetProperty("routes").EnumerateArray().Single(route => route.GetProperty("pattern").GetString() == "/untitled")
            .TryGetProperty("title", out var __).Should().BeFalse("a route that declares no title says nothing");
        config.GetProperty("themeCookie").GetProperty("name").GetString().Should().Be("eq'theme");
        config.GetProperty("page").GetString().Should().Be(nameof(UntitledPage));
        // The title of a route closes no script element: its `<` is escaped where it is written.
        html.Should().NotContain("</script><b>x");
    }

    [Fact]
    public async Task ANavigationToAPageTheServerDoesNotRender_StillAnswersItsRoutesTitleAndHead()
    {
        // It answered `{}`, so the previous page's description and canonical stayed in the head.
        var (app, client) = await StartAsync();
        await using var _ = app;

        var navigation = await NavigateAsync(client, "/client-only");

        navigation.GetProperty("title").GetString().Should().Be("Client only");
        navigation.GetProperty("head").GetString().Should().Contain("<meta name=\"description\" content=\"Drawn in the browser\" data-eq-meta>");
        navigation.TryGetProperty("state", out var __).Should().BeFalse("nothing was prepared");
    }

    [Fact]
    public async Task EveryTagTheMetadataWrites_IsMarked_OnALoadAndANavigation()
    {
        // The client removes the marked set the previous page left and writes the next one's, so a tag
        // the next page does not have is gone instead of standing beside the new ones.
        var (app, client) = await StartAsync();
        await using var _ = app;

        var html = await client.GetStringAsync("/spoken");
        var head = (await NavigateAsync(client, "/spoken")).GetProperty("head").GetString()!;

        html.Should().Contain("<meta name=\"description\" content=\"Dynamic words\" data-eq-meta>");
        html.Should().Contain("<meta property=\"og:title\" content=\"Dynamic\" data-eq-meta>");
        System.Text.RegularExpressions.Regex.Matches(head, "<(meta|link) [^>]*>").Should().NotBeEmpty()
            .And.OnlyContain(tag => tag.Value.EndsWith(" data-eq-meta>"));
    }
}
