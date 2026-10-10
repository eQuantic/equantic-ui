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
using Microsoft.Extensions.Logging;
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

    /// <summary>
    /// A monitor whose configuration reloads just as a listener subscribes, before that listener is in
    /// place: what a reload between reading the value and subscribing looks like from the subscriber.
    /// </summary>
    private sealed class ReloadAsItSubscribes(ServerActionsOptions first, ServerActionsOptions reloaded)
        : IOptionsMonitor<ServerActionsOptions>
    {
        private readonly List<Action<ServerActionsOptions, string?>> _listeners = [];

        public ServerActionsOptions CurrentValue { get; private set; } = first;

        public ServerActionsOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<ServerActionsOptions, string?> listener)
        {
            CurrentValue = reloaded;
            foreach (var heard in _listeners.ToArray()) heard(reloaded, null);
            _listeners.Add(listener);
            return null;
        }
    }

    private sealed class RecordingLogger : ILogger<ServerActionsMiddleware>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
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
    [InlineData("https://[::1]:8443", "[::1]:8443")]
    [InlineData("http://[::1]", "[::1]")]
    [InlineData("https://[::1]:8443", "[0:0:0:0:0:0:0:1]:8443")]
    [InlineData("https://app.example", "app.example:443")]
    [InlineData("http://app.example:443", "app.example:443")]
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
    [InlineData("https://app.example", "app.example:80")]
    [InlineData("http://app.example", "app.example:443")]
    [InlineData("https://app.example:8443", "app.example")]
    public async Task TheSameHostOnAnotherPort_IsAnotherSite(string origin, string host)
    {
        // A Host that names a port is compared with the origin's own, and one without stands for the
        // origin scheme's default: https://app.example is not the app at app.example:80.
        var (status, calls) = await Invoke(host, ("Origin", origin));

        status.Should().Be(StatusCodes.Status403Forbidden);
        calls.Should().Be(0);
    }

    [Fact]
    public async Task AnotherIPv6Address_IsRefused()
    {
        var (status, calls) = await Invoke("[::1]:8443", ("Origin", "https://[::2]:8443"));

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

    [Fact]
    public async Task ARawForwardedHost_IsNotTheAppsOwn()
    {
        // A site the app's CORS policy lets through could write its own host into the header. Behind a
        // proxy, UseForwardedHeaders restores Request.Host from the proxies the app trusts.
        var (status, calls) = await Invoke("app.example",
            ("Origin", "https://evil.example"), ("X-Forwarded-Host", "evil.example"));

        status.Should().Be(StatusCodes.Status403Forbidden);
        calls.Should().Be(0);
    }

    [Theory]
    [InlineData("https://admin.example")]
    [InlineData("https://Admin.Example/")]
    [InlineData("https://admin.example:443")]
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

        (await PostFrom(client, "https://evil.example")).Should().Be(HttpStatusCode.Forbidden);
        (await PostFrom(client, "https://admin.example")).Should().Be(HttpStatusCode.OK);
        app.Services.GetRequiredService<ActionLog>().Calls.Should().Be(1);
    }

    [Fact]
    public async Task AReloadedConfiguration_ChangesTheAllowedOrigins_WithoutARestart()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{ServerActionsOptions.SectionName}:AllowedOrigins:0"] = "https://staging.example",
        });
        builder.Services.AddSingleton<ActionLog>();
        builder.Services.AddSingleton<IServerActionAuthorizationService, AlwaysAllowed>();
        builder.Services.AddUI(options => options.ScanAssembly(typeof(ServerActionOriginTests).Assembly));
        await using var app = builder.Build();
        app.UseServerActions();
        await app.StartAsync();
        var client = app.GetTestClient();
        (await PostFrom(client, "https://admin.example")).Should().Be(HttpStatusCode.Forbidden);

        app.Configuration[$"{ServerActionsOptions.SectionName}:AllowedOrigins:0"] = "https://admin.example";
        ((IConfigurationRoot)app.Configuration).Reload();

        (await PostFrom(client, "https://admin.example")).Should().Be(HttpStatusCode.OK);
        (await PostFrom(client, "https://staging.example")).Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A reload in the window between reading the allowed origins and subscribing to their changes is
    /// not lost (Copilot's third review of #737): the middleware subscribes first and reads the latest
    /// value after, so it ends on the reloaded list rather than the one a read before the reload saw.
    /// </summary>
    [Fact]
    public async Task AReloadAsTheMiddlewareSubscribes_IsNotLost()
    {
        var actions = new ReloadAsItSubscribes(Allowing(), Allowing("https://admin.example"));

        var (status, calls) = await Invoke("api.example", actions, NullLogger<ServerActionsMiddleware>.Instance,
            [("Origin", "https://admin.example")]);

        status.Should().Be(StatusCodes.Status200OK);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task ARefusal_LogsTheRequestsHeadersOnOneLine()
    {
        // The headers are the request's own text: a CR or an LF in them would start a line of its own
        // in a plain-text log.
        var logger = new RecordingLogger();

        var (status, _) = await Invoke("app.example", [], logger,
            [("Origin", "https://evil.example\r\nForged entry"), ("Sec-Fetch-Site", "cross-site\nForged")]);

        status.Should().Be(StatusCodes.Status403Forbidden);
        logger.Messages.Should().ContainSingle()
            .Which.Should().Contain("https://evil.exampleForged entry").And.NotContainAny("\r", "\n");
    }

    private static async Task<HttpStatusCode> PostFrom(HttpClient client, string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/_equantic/actions")
        {
            Content = new StringContent(Body, Encoding.UTF8, "text/plain"),
        };
        request.Headers.Add("Origin", origin);
        return (await client.SendAsync(request)).StatusCode;
    }

    private static Task<(int Status, int Calls)> Invoke(string host, params (string Name, string Value)[] headers) =>
        Invoke(host, [], headers);

    private static Task<(int Status, int Calls)> Invoke(
        string host, string[] allowedOrigins, params (string Name, string Value)[] headers) =>
        Invoke(host, allowedOrigins, NullLogger<ServerActionsMiddleware>.Instance, headers);

    private static Task<(int Status, int Calls)> Invoke(
        string host, string[] allowedOrigins, ILogger<ServerActionsMiddleware> logger,
        (string Name, string Value)[] headers) =>
        Invoke(host, new FixedOptions<ServerActionsOptions>(Allowing(allowedOrigins)), logger, headers);

    private static ServerActionsOptions Allowing(params string[] origins)
    {
        var actions = new ServerActionsOptions();
        foreach (var origin in origins) actions.AllowedOrigins.Add(origin);
        return actions;
    }

    private static async Task<(int Status, int Calls)> Invoke(
        string host, IOptionsMonitor<ServerActionsOptions> actions, ILogger<ServerActionsMiddleware> logger,
        (string Name, string Value)[] headers)
    {
        var registry = new ServerActionRegistry();
        registry.ScanAssembly(typeof(ServerActionOriginTests).Assembly);
        var log = new ActionLog();
        var root = new ServiceCollection().AddSingleton(log).BuildServiceProvider(validateScopes: true);

        var middleware = new ServerActionsMiddleware(
            next: _ => Task.CompletedTask,
            registry: registry,
            serviceProvider: root,
            authorizationService: new AlwaysAllowed(),
            options: new UIOptions(),
            actions: actions,
            logger: logger);

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
