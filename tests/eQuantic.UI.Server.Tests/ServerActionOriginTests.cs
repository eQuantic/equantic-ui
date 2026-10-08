using System.Net;
using System.Text;
using eQuantic.UI.Primitives;
using eQuantic.UI.Server.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// A Server Action refuses a request another site sends (#678). Nothing asked where a request came
/// from, and the endpoint read a body whatever its content type, so another site's page could post a
/// "simple" request, which a browser sends without a CORS preflight, and an anonymous action ran for it
/// as it runs for the app's own page. A browser says where a request came from in <c>Origin</c>, which
/// it sends on every POST, and in <c>Sec-Fetch-Site</c>.
/// </summary>
public class ServerActionOriginTests
{
    private const string Body = """{"actionId":"ContactActions/Send","arguments":[]}""";

    public sealed class ActionLog
    {
        public int Calls;
    }

    public sealed class ContactActions(ActionLog log) : StatelessComponent
    {
        [ServerAction]
        public Task<string> Send()
        {
            Interlocked.Increment(ref log.Calls);
            return Task.FromResult("sent");
        }

        public override VisualNode Build(ComponentContext context) => new Column(gap: 0);
    }

    private sealed class AlwaysAllowed : IServerActionAuthorizationService
    {
        public Task<ServerActionAuthorizationResult> AuthorizeAsync(
            HttpContext context, ServerActionDescriptor descriptor) =>
            Task.FromResult(ServerActionAuthorizationResult.Success());
    }

    [Theory]
    [InlineData("https://app.example", "app.example")]
    [InlineData("https://APP.example", "app.example")]
    [InlineData("https://app.example:443", "app.example")]
    [InlineData("https://app.example:8443", "app.example:8443")]
    [InlineData("http://app.example", "app.example")]
    public async Task TheAppsOwnPage_Runs(string origin, string host)
    {
        var (status, calls) = await Invoke(host, ("Origin", origin));

        status.Should().Be(StatusCodes.Status200OK);
        calls.Should().Be(1);
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("https://app.example.evil.example")]
    [InlineData("https://app.example:8443")]
    [InlineData("null")]
    public async Task AnotherSite_IsRefused_AndTheActionNeverRuns(string origin)
    {
        var (status, calls) = await Invoke("app.example", ("Origin", origin));

        status.Should().Be(StatusCodes.Status403Forbidden);
        calls.Should().Be(0);
    }

    [Theory]
    [InlineData("cross-site", StatusCodes.Status403Forbidden, 0)]
    [InlineData("same-site", StatusCodes.Status403Forbidden, 0)]
    [InlineData("same-origin", StatusCodes.Status200OK, 1)]
    [InlineData("none", StatusCodes.Status200OK, 1)]
    public async Task WithoutAnOrigin_TheBrowsersFetchSiteDecides(string fetchSite, int expectedStatus, int expectedCalls)
    {
        var (status, calls) = await Invoke("app.example", ("Sec-Fetch-Site", fetchSite));

        status.Should().Be(expectedStatus);
        calls.Should().Be(expectedCalls);
    }

    [Fact]
    public async Task NotABrowser_Runs()
    {
        // Every browser sends Origin on a POST: a request with neither header is a server or a tool,
        // which a cross-site forgery is not about.
        var (status, calls) = await Invoke("app.example");

        status.Should().Be(StatusCodes.Status200OK);
        calls.Should().Be(1);
    }

    [Theory]
    [InlineData("app.example")]
    [InlineData("app.example, proxy.internal")]
    public async Task BehindAProxy_TheForwardedHostIsTheAppsOwn(string forwardedHost)
    {
        var (status, calls) = await Invoke("localhost:5000",
            ("Origin", "https://app.example"), ("X-Forwarded-Host", forwardedHost));

        status.Should().Be(StatusCodes.Status200OK);
        calls.Should().Be(1);
    }

    [Theory]
    [InlineData("https://admin.example")]
    [InlineData("https://Admin.Example/")]
    public async Task AnAllowedOrigin_Runs(string allowed)
    {
        var (status, calls) = await Invoke("api.example", [allowed], ("Origin", "https://admin.example"));

        status.Should().Be(StatusCodes.Status200OK);
        calls.Should().Be(1);
    }

    [Theory]
    [InlineData("https://admin.example/login")]
    [InlineData("https://admin.example?next=1")]
    [InlineData("admin.example")]
    [InlineData("ftp://admin.example")]
    public async Task AMalformedAllowedOrigin_StopsTheApp_NamingIt(string origin)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options => options.AllowServerActionOrigins(origin));
        var app = builder.Build();

        var start = () => app.StartAsync();

        (await start.Should().ThrowAsync<OptionsValidationException>()).WithMessage($"*{origin}*");
        await app.DisposeAsync();
    }

    [Fact]
    public async Task TheAllowedOrigins_AreConfigurationsAndProgramsTogether()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{ServerActionsOptions.SectionName}:AllowedOrigins:0"] = "https://staging.example",
        });
        builder.Services.AddUI(options => options.AllowServerActionOrigins("https://admin.example"));
        await using var app = builder.Build();

        app.Services.GetRequiredService<IOptions<ServerActionsOptions>>().Value.AllowedOrigins
            .Should().Equal("https://staging.example", "https://admin.example");
    }

    [Fact]
    public async Task ThroughTheAppsPipeline_AnotherSiteIsRefused_AndAnAllowedOneRuns()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<ActionLog>();
        builder.Services.AddSingleton<IServerActionAuthorizationService, AlwaysAllowed>();
        builder.Services.AddUI(options => options
            .ScanAssembly(typeof(ServerActionOriginTests).Assembly)
            .AllowServerActionOrigins("https://admin.example"));
        await using var app = builder.Build();
        app.UseServerActions();
        await app.StartAsync();
        var client = app.GetTestClient();

        async Task<HttpStatusCode> Post(string origin)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/_equantic/actions")
            {
                Content = new StringContent(Body, Encoding.UTF8, "text/plain"),
            };
            request.Headers.Add("Origin", origin);
            return (await client.SendAsync(request)).StatusCode;
        }

        (await Post("https://evil.example")).Should().Be(HttpStatusCode.Forbidden);
        (await Post("https://admin.example")).Should().Be(HttpStatusCode.OK);
        app.Services.GetRequiredService<ActionLog>().Calls.Should().Be(1);
    }

    private static Task<(int Status, int Calls)> Invoke(string host, params (string Name, string Value)[] headers) =>
        Invoke(host, [], headers);

    private static async Task<(int Status, int Calls)> Invoke(
        string host, string[] allowedOrigins, params (string Name, string Value)[] headers)
    {
        var registry = new ServerActionRegistry();
        registry.ScanAssembly(typeof(ServerActionOriginTests).Assembly);
        var log = new ActionLog();
        var root = new ServiceCollection().AddSingleton(log).BuildServiceProvider(validateScopes: true);
        var actions = new ServerActionsOptions();
        foreach (var origin in allowedOrigins) actions.AllowedOrigins.Add(origin);

        var middleware = new ServerActionsMiddleware(
            next: _ => Task.CompletedTask,
            registry: registry,
            serviceProvider: root,
            authorizationService: new AlwaysAllowed(),
            options: new UIOptions(),
            actions: Options.Create(actions),
            logger: NullLogger<ServerActionsMiddleware>.Instance);

        using var scope = root.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Path = "/api/_equantic/actions";
        context.Request.Method = "POST";
        context.Request.Host = new HostString(host);
        foreach (var (name, value) in headers) context.Request.Headers[name] = value;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(Body));
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        return (context.Response.StatusCode, log.Calls);
    }
}
