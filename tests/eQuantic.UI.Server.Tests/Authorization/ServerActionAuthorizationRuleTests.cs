using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using eQuantic.UI.Primitives;
using eQuantic.UI.Server.Authorization;
using eQuantic.UI.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using AspNetAllowAnonymous = Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute;
using AspNetAuthorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using SdkAllowAnonymous = eQuantic.UI.Primitives.AllowAnonymousAttribute;
using SdkAuthorize = eQuantic.UI.Primitives.AuthorizeAttribute;

namespace eQuantic.UI.Server.Tests.Authorization;

/// <summary>
/// A Server Action is authorized by the rule a page is (#1285): each row of the issue, each of which the
/// service let through, or refused, by a rule of its own.
/// </summary>
public class ServerActionAuthorizationRuleTests
{
    [AspNetAuthorize(Roles = "Admin")]
    public sealed class AdminPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text(string.Empty);

        [ServerAction]
        public string Delete() => "deleted";
    }

    [SdkAuthorize(AuthenticationSchemes = "Staff")]
    public sealed class StaffPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text(string.Empty);

        [ServerAction]
        public void Approve() { }
    }

    public sealed class Unmarked : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text(string.Empty);

        [ServerAction]
        public void Save() { }

        [ServerAction]
        [AspNetAllowAnonymous]
        public void Ping() { }

        [ServerAction]
        [SdkAuthorize(Policy = "FromThisRequest")]
        public void ReadRequest() { }

        [ServerAction]
        [SdkAuthorize(Roles = "Reader")]
        [SdkAuthorize(Roles = "Writer")]
        public void Edit() { }
    }

    [SdkAllowAnonymous]
    public sealed class OpenPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text(string.Empty);

        [ServerAction]
        [AspNetAuthorize]
        public void Close() { }
    }

    [Fact]
    public async Task AnAspNetAuthorizeOnThePage_RefusesAnAnonymousCall_With401()
    {
        var (status, body) = await Call<AdminPage>(nameof(AdminPage.Delete), Anonymous());

        status.Should().Be(StatusCodes.Status401Unauthorized);
        body.Should().NotContain("deleted");
    }

    [Fact]
    public async Task AnAspNetAuthorizeOnThePage_RefusesAUserWithoutItsRole_AndDoesNotNameTheRole()
    {
        var (status, body) = await Call<AdminPage>(nameof(AdminPage.Delete), User("Reader"));

        status.Should().Be(StatusCodes.Status403Forbidden);
        body.Should().NotContain("Admin");
    }

    [Fact]
    public async Task AnAspNetAuthorizeOnThePage_LetsItsRoleIn()
    {
        var (status, body) = await Call<AdminPage>(nameof(AdminPage.Delete), User("Admin"));

        status.Should().Be(StatusCodes.Status200OK);
        body.Should().Contain("deleted");
    }

    [Fact]
    public async Task ThePagesScheme_IsAuthenticated_SoTheDefaultSchemesUserIsChallenged()
    {
        var result = await Authorize<StaffPage>(nameof(StaffPage.Approve), User("Customer"), staffSignedIn: false);

        result.IsUnauthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task ThePagesScheme_LetsItsOwnUserIn()
    {
        var result = await Authorize<StaffPage>(nameof(StaffPage.Approve), Anonymous(), staffSignedIn: true);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task AnActionThatNamesNoRequirement_IsUnderTheAppsFallbackPolicy()
    {
        var result = await Authorize<Unmarked>(nameof(Unmarked.Save), Anonymous(), requireSignedInUsers: true);

        result.IsUnauthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task AnAllowAnonymousAction_StaysOpenUnderTheFallbackPolicy()
    {
        var result = await Authorize<Unmarked>(nameof(Unmarked.Ping), Anonymous(), requireSignedInUsers: true);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task APolicy_IsAskedWithTheRequestAsItsResource()
    {
        var result = await Authorize<Unmarked>(nameof(Unmarked.ReadRequest), User("Reader"));

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task TwoAuthorizeOnOneMethod_AreBothMet()
    {
        (await Authorize<Unmarked>(nameof(Unmarked.Edit), User("Reader"))).IsForbidden.Should().BeTrue();
        (await Authorize<Unmarked>(nameof(Unmarked.Edit), User("Reader", "Writer"))).Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task AnAuthorizeOnTheMethod_ClosesWhatTheComponentsAllowAnonymousOpens()
    {
        var result = await Authorize<OpenPage>(nameof(OpenPage.Close), Anonymous());

        result.IsUnauthenticated.Should().BeTrue();
    }

    private static async Task<ServerActionAuthorizationResult> Authorize<T>(
        string method, ClaimsPrincipal user, bool staffSignedIn = false, bool requireSignedInUsers = false)
    {
        var context = Context(user, staffSignedIn, requireSignedInUsers);
        return await new ServerActionAuthorizationService(NullLogger<ServerActionAuthorizationService>.Instance)
            .AuthorizeAsync(context, Descriptor<T>(method));
    }

    private static async Task<(int Status, string Body)> Call<T>(string method, ClaimsPrincipal user)
    {
        var descriptor = Descriptor<T>(method);
        var registry = new ServerActionRegistry();
        registry.RegisterAction(descriptor.ActionId, descriptor);
        var middleware = new ServerActionsMiddleware(
            next: _ => Task.CompletedTask,
            registry: registry,
            serviceProvider: new ServiceCollection().BuildServiceProvider(),
            authorizationService: new ServerActionAuthorizationService(NullLogger<ServerActionAuthorizationService>.Instance),
            options: new UIOptions(),
            actions: new FixedOptions<ServerActionsOptions>(new ServerActionsOptions()),
            logger: NullLogger<ServerActionsMiddleware>.Instance);

        var context = Context(user, staffSignedIn: false, requireSignedInUsers: false);
        context.Request.Path = "/api/_equantic/actions";
        context.Request.Method = "POST";
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(
            $$"""{"actionId":"{{descriptor.ActionId}}","arguments":[]}"""));
        var response = new MemoryStream();
        context.Response.Body = response;

        await middleware.InvokeAsync(context);

        return (context.Response.StatusCode, Encoding.UTF8.GetString(response.ToArray()));
    }

    /// <summary>A request to an app whose default scheme signed <paramref name="user"/> in, with a "Staff"
    /// scheme of its own, and the policies the components above name.</summary>
    private static DefaultHttpContext Context(ClaimsPrincipal user, bool staffSignedIn, bool requireSignedInUsers)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, StaffHandler>("Staff", options => options.ClaimsIssuer = staffSignedIn ? "in" : "out");
        services.AddAuthorization(options =>
        {
            options.AddPolicy("FromThisRequest", policy => policy.RequireAssertion(check => check.Resource is HttpContext));
            if (requireSignedInUsers)
                options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        });
        return new DefaultHttpContext { RequestServices = services.BuildServiceProvider(), User = user };
    }

    private static ServerActionDescriptor Descriptor<T>(string method) => new()
    {
        ActionId = $"{typeof(T).FullName}/{method}",
        ComponentType = typeof(T),
        Method = typeof(T).GetMethod(method)!,
        ParameterTypes = [],
        IsAsync = false,
    };

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static ClaimsPrincipal User(params string[] roles) =>
        new(new ClaimsIdentity(
            roles.Select(role => new Claim(ClaimTypes.Role, role)).Prepend(new Claim(ClaimTypes.Name, "someone")),
            authenticationType: "Default"));

    /// <summary>The "Staff" scheme: it signs a staff member in when the test says one is, and answers
    /// nothing otherwise, whoever the default scheme signed in.</summary>
    private sealed class StaffHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(Options.ClaimsIssuer == "in"
                ? AuthenticateResult.Success(new AuthenticationTicket(
                    new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "staff")], Scheme.Name)), Scheme.Name))
                : AuthenticateResult.NoResult());
    }
}
