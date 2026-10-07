using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ui = eQuantic.UI.Primitives;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// A page that requires authorization says so itself (#673): <c>[Authorize]</c> on a <c>[Page]</c> held
/// for its Server Actions only, so its route served anyone and its <c>IServerPrefetch</c> ran for an
/// anonymous visitor, writing its fields into the HTML. The page's route carries the requirement as
/// endpoint metadata now, so ASP.NET Core's own authorization decides: an anonymous full load is
/// challenged by the default scheme, a signed-in visitor without the policy gets 403, a client
/// navigation gets a 401 or a 403 it can act on, and a refused request never reaches the page.
/// </summary>
public class PageAuthorizationTests
{
    private static int _prefetches;

    [Ui.Page("/backoffice/queue")]
    [Ui.Authorize(Policy = "Backoffice")]
    public sealed class BackofficeQueuePage : Ui.StatelessComponent, Ui.IServerPrefetch
    {
        public string Row = "";

        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _prefetches);
            Row = "TOP-SECRET-ROW";
            return Task.CompletedTask;
        }

        public override Ui.VisualNode Build(Ui.ComponentContext context) =>
            new Ui.Text(Row.Length > 0 ? Row : "nothing loaded", Ui.TypeRole.BodyM);
    }

    /// <summary>Signed in is enough: an <c>[Authorize]</c> with no policy.</summary>
    [Ui.Page("/account/home")]
    [Ui.Authorize]
    public sealed class AccountPage : Ui.StatelessComponent
    {
        public override Ui.VisualNode Build(Ui.ComponentContext context) =>
            new Ui.Text("ACCOUNT-HOME", Ui.TypeRole.BodyM);
    }

    /// <summary>A page an app routes from Program.cs, which carries its own requirement too.</summary>
    [Ui.Authorize(Policy = "Backoffice")]
    public sealed class DeclaredBackofficePage : Ui.StatelessComponent
    {
        public override Ui.VisualNode Build(Ui.ComponentContext context) =>
            new Ui.Text("DECLARED-SECRET", Ui.TypeRole.BodyM);
    }

    /// <summary>The page every app with a fallback policy needs: the one a visitor signs in on.</summary>
    [Ui.Page("/open/sign-in")]
    [Ui.AllowAnonymous]
    public sealed class SignInPage : Ui.StatelessComponent
    {
        public override Ui.VisualNode Build(Ui.ComponentContext context) =>
            new Ui.Text("SIGN-IN", Ui.TypeRole.BodyM);
    }

    /// <summary>A scheme that signs a request in from a header, and challenges by redirecting to sign
    /// in, as an OpenID Connect scheme does.</summary>
    private sealed class TestScheme(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-User", out var user)) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new(ClaimTypes.Name, user.ToString()) };
            if (Request.Headers.TryGetValue("X-Test-Role", out var role)) claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Name)));
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.Redirect("/signin-oidc?returnUrl=" + Uri.EscapeDataString(Request.Path));
            return Task.CompletedTask;
        }
    }

    /// <summary>A 404 page that only the backoffice may see, with data of its own.</summary>
    [Ui.Authorize(Policy = "Backoffice")]
    public sealed class ProtectedNotFoundPage : Ui.StatelessComponent, Ui.IServerPrefetch
    {
        public string Row = "";

        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            Row = "NOT-FOUND-SECRET";
            return Task.CompletedTask;
        }

        public override Ui.VisualNode Build(Ui.ComponentContext context) =>
            new Ui.Text(Row.Length > 0 ? Row : "nothing loaded", Ui.TypeRole.BodyM);
    }

    /// <summary>A 500 page that only the backoffice may see.</summary>
    [Ui.Authorize(Policy = "Backoffice")]
    public sealed class ProtectedErrorPage : Ui.StatelessComponent, Ui.IServerPrefetch
    {
        public string Row = "";

        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            Row = "ERROR-SECRET";
            return Task.CompletedTask;
        }

        public override Ui.VisualNode Build(Ui.ComponentContext context) =>
            new Ui.Text(Row.Length > 0 ? Row : "nothing loaded", Ui.TypeRole.BodyM);
    }

    /// <summary>A public page whose server data fails to load, so the 500 page stands in for it.</summary>
    public sealed class FailingPage : Ui.StatelessComponent, Ui.IServerPrefetch
    {
        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("the database is down");

        public override Ui.VisualNode Build(Ui.ComponentContext context) => new Ui.Text("failing", Ui.TypeRole.BodyM);
    }

    /// <summary>A request-scoped service, the kind a scoped result handler depends on.</summary>
    private sealed class RequestClock
    {
        public string Stamp { get; } = Guid.NewGuid().ToString("N");
    }

    /// <summary>An app's own result handler registered SCOPED, with a scoped dependency.</summary>
    private sealed class ScopedTeapotHandler(RequestClock clock) : IAuthorizationMiddlewareResultHandler
    {
        public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy,
            Microsoft.AspNetCore.Authorization.Policy.PolicyAuthorizationResult authorizeResult)
        {
            if (authorizeResult.Succeeded) return next(context);
            context.Response.StatusCode = 418;
            context.Response.Headers["X-Clock"] = clock.Stamp;
            return Task.CompletedTask;
        }
    }

    /// <summary>An app's own result handler, which answers a refusal with a teapot.</summary>
    private sealed class TeapotHandler : IAuthorizationMiddlewareResultHandler
    {
        public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy,
            Microsoft.AspNetCore.Authorization.Policy.PolicyAuthorizationResult authorizeResult)
        {
            if (authorizeResult.Succeeded) return next(context);
            context.Response.StatusCode = 418;
            return Task.CompletedTask;
        }
    }

    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(bool fallbackPolicy = false,
        bool teapot = false, bool cultures = false, bool protectedErrorPages = false, bool scopedTeapot = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        // Scope validation on, as Development turns it on: a captive scoped dependency throws here.
        builder.Host.UseDefaultServiceProvider(provider => provider.ValidateScopes = true);
        if (teapot) builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, TeapotHandler>();
        if (scopedTeapot)
        {
            builder.Services.AddScoped<RequestClock>();
            builder.Services.AddScoped<IAuthorizationMiddlewareResultHandler, ScopedTeapotHandler>();
        }
        builder.Services.AddAuthentication(TestScheme.Name)
            .AddScheme<AuthenticationSchemeOptions, TestScheme>(TestScheme.Name, null);
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("Backoffice", policy => policy.RequireRole("backoffice"));
            if (fallbackPolicy) options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        });
        builder.Services.AddUI(options =>
        {
            options.ScanAssembly(Assembly.GetExecutingAssembly());
            if (cultures) options.UseCultureRoutes("en", "pt-BR");
            if (protectedErrorPages)
            {
                options.RegisterErrorPage(typeof(ProtectedNotFoundPage), "/404");
                options.RegisterErrorPage(typeof(ProtectedErrorPage), "/500");
            }
        });
        var app = builder.Build();
        if (cultures) app.UseRequestLocalization();
        app.MapUI();
        app.MapPage<DeclaredBackofficePage>("/declared/backoffice");
        app.MapPage<FailingPage>("/failing");
        if (protectedErrorPages)
        {
            // Routed too, as an app's /404 and /500 pages are, so the server knows how to draw them.
            app.MapPage<ProtectedNotFoundPage>("/protected-404");
            app.MapPage<ProtectedErrorPage>("/protected-500");
        }
        await app.StartAsync();
        var client = app.GetTestClient();
        return (app, client);
    }

    private static HttpRequestMessage Get(string path, string? user = null, string? role = null, bool navigate = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (user is not null) request.Headers.Add("X-Test-User", user);
        if (role is not null) request.Headers.Add("X-Test-Role", role);
        if (navigate) request.Headers.Add("X-EQ-Navigate", "1");
        return request;
    }

    [Fact]
    public async Task AnAnonymousFullLoad_IsChallengedByTheDefaultScheme()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;
        Interlocked.Exchange(ref _prefetches, 0);

        var response = await client.SendAsync(Get("/backoffice/queue"));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect, "the scheme's challenge sends the visitor to sign in");
        response.Headers.Location!.ToString().Should().StartWith("/signin-oidc");
        (await response.Content.ReadAsStringAsync()).Should().NotContain("TOP-SECRET-ROW");
        _prefetches.Should().Be(0, "a refused request never reaches the page");
    }

    [Fact]
    public async Task ASignedInVisitorWithoutThePolicy_Gets403()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;
        Interlocked.Exchange(ref _prefetches, 0);

        var response = await client.SendAsync(Get("/backoffice/queue", user: "ana"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("TOP-SECRET-ROW");
        _prefetches.Should().Be(0);
    }

    [Fact]
    public async Task AVisitorWithThePolicy_GetsThePageAndItsData()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        var response = await client.SendAsync(Get("/backoffice/queue", user: "ana", role: "backoffice"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("TOP-SECRET-ROW");
    }

    [Fact]
    public async Task AnAnonymousClientNavigation_Gets401_NeverThePage()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;
        Interlocked.Exchange(ref _prefetches, 0);

        var response = await client.SendAsync(Get("/backoffice/queue", navigate: true));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "a fetch cannot follow a sign-in redirect; the router falls back to a full load on a 401");
        response.Headers.GetValues("X-EQ-Navigate").Should().Equal("1");
        (await response.Content.ReadAsStringAsync()).Should().NotContain("TOP-SECRET-ROW");
        _prefetches.Should().Be(0);
    }

    [Fact]
    public async Task AForbiddenClientNavigation_Gets403()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        var response = await client.SendAsync(Get("/backoffice/queue", user: "ana", navigate: true));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Headers.GetValues("X-EQ-Navigate").Should().Equal("1");
    }

    [Fact]
    public async Task AnAuthorizeWithNoPolicy_AsksOnlyForASignedInVisitor()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        (await client.SendAsync(Get("/account/home"))).StatusCode.Should().Be(HttpStatusCode.Redirect);
        var signedIn = await client.SendAsync(Get("/account/home", user: "ana"));
        signedIn.StatusCode.Should().Be(HttpStatusCode.OK);
        (await signedIn.Content.ReadAsStringAsync()).Should().Contain("ACCOUNT-HOME");
    }

    [Fact]
    public async Task ARouteDeclaredInProgram_CarriesThePagesRequirement()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        (await client.SendAsync(Get("/declared/backoffice", user: "ana"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var allowed = await client.SendAsync(Get("/declared/backoffice", user: "ana", role: "backoffice"));
        (await allowed.Content.ReadAsStringAsync()).Should().Contain("DECLARED-SECRET");
    }

    /// <summary>An app's own result handler, registered before AddUI, keeps answering every refusal that
    /// is not a navigation.</summary>
    [Fact]
    public async Task AnAppsOwnResultHandler_KeepsAnsweringFullLoads()
    {
        var (app, client) = await StartAsync(teapot: true);
        await using var _ = app;

        ((int)(await client.SendAsync(Get("/backoffice/queue"))).StatusCode).Should().Be(418);
        (await client.SendAsync(Get("/backoffice/queue", navigate: true))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A SCOPED handler of the app's, with a scoped dependency, is still built from the
    /// request's scope once it is wrapped (found by Copilot on #686).</summary>
    [Fact]
    public async Task AnAppsScopedResultHandler_IsResolvedFromTheRequestsScope()
    {
        var (app, client) = await StartAsync(scopedTeapot: true);
        await using var _ = app;

        var first = await client.SendAsync(Get("/backoffice/queue"));
        var second = await client.SendAsync(Get("/backoffice/queue"));

        ((int)first.StatusCode).Should().Be(418);
        first.Headers.GetValues("X-Clock").Single().Should().NotBe(second.Headers.GetValues("X-Clock").Single(),
            "each request builds the handler's scoped dependency of its own");
        (await client.SendAsync(Get("/backoffice/queue", navigate: true))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>The 404 page a URL that matches nothing reaches through the fallback carries no endpoint
    /// of its own: its requirement is asked before it is built (found by Copilot on #686).</summary>
    [Fact]
    public async Task AProtected404Page_IsNotDrawnForAVisitorItRefuses()
    {
        var (app, client) = await StartAsync(protectedErrorPages: true);
        await using var _ = app;

        var anonymous = await client.SendAsync(Get("/no/such/route"));
        anonymous.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await anonymous.Content.ReadAsStringAsync()).Should().NotContain("NOT-FOUND-SECRET");

        var allowed = await client.SendAsync(Get("/no/such/route", user: "ana", role: "backoffice"));
        allowed.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await allowed.Content.ReadAsStringAsync()).Should().Contain("NOT-FOUND-SECRET");
    }

    [Fact]
    public async Task AProtected500Page_IsNotDrawnForAVisitorItRefuses()
    {
        var (app, client) = await StartAsync(protectedErrorPages: true);
        await using var _ = app;

        (await (await client.SendAsync(Get("/failing"))).Content.ReadAsStringAsync()).Should().NotContain("ERROR-SECRET");
        (await (await client.SendAsync(Get("/failing", user: "ana", role: "backoffice"))).Content.ReadAsStringAsync())
            .Should().Contain("ERROR-SECRET");
    }

    [Fact]
    public async Task ALanguagePrefixedRoute_CarriesTheSameRequirement()
    {
        var (app, client) = await StartAsync(cultures: true);
        await using var _ = app;

        (await client.SendAsync(Get("/pt-BR/backoffice/queue"))).StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await client.SendAsync(Get("/pt-BR/backoffice/queue", user: "ana"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Under an app's fallback policy, the page that allows anonymous visitors still serves,
    /// and so does the runtime it needs to come alive.</summary>
    [Fact]
    public async Task UnderAFallbackPolicy_AnAnonymousPageAndItsRuntimeServe()
    {
        var (app, client) = await StartAsync(fallbackPolicy: true);
        await using var _ = app;

        var page = await client.SendAsync(Get("/open/sign-in"));
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        (await page.Content.ReadAsStringAsync()).Should().Contain("SIGN-IN");
        (await client.SendAsync(Get("/_equantic/runtime.js"))).StatusCode.Should().Be(HttpStatusCode.OK,
            "a sign-in page that cannot load the runtime is a page nobody can use");
        (await client.SendAsync(Get("/account/home"))).StatusCode.Should().Be(HttpStatusCode.Redirect);
    }
}
