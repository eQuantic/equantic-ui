using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using eQuantic.UI.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// A value a page receives from the container crosses as what the browser reads of it, read off the
/// document the server sends.
/// <para>
/// A service registered as a class crossed whole, because it was not an interface: a login page put its
/// identity provider's authority into its HTML that way (#510). The browser could only agree with the
/// server through that leak, since the router builds a page with no arguments. What crosses now is the
/// projection the build wrote into the manifest, and the container has the last word on a value the
/// build could not see the type of.
/// </para>
/// </summary>
public class ServerValueCrossingTests
{
    private const string Authority = "https://login.example.test/secret-tenant";
    private const string ApiKey = "sk-live-0123456789";

    public sealed class SiteIdentity
    {
        public string Authority { get; init; } = "";
        public string DisplayName { get; init; } = "";
    }

    public sealed class SiteOptions
    {
        public string Title { get; init; } = "";
        public string ApiKey { get; init; } = "";
    }

    /// <summary>Reads only whether someone is signed in.</summary>
    [Page("/identity")]
    public sealed class IdentityPage(SiteIdentity? identity = null) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            new Text(identity is null ? "sign in" : "your account", TypeRole.BodyM);
    }

    /// <summary>Keeps the options in a field and reads one member of them.</summary>
    [Page("/options")]
    public sealed class OptionsPage : StatelessComponent
    {
        private readonly SiteOptions _options;

        public OptionsPage(SiteOptions options)
        {
            _options = options;
        }

        public override VisualNode Build(ComponentContext context) => new Text(_options.Title, TypeRole.BodyM);
    }

    public class BaseOptions
    {
        public string Title { get; init; } = "";
    }

    /// <summary>Hides the base's title with a member of its own, which C# reading a BaseOptions never binds.</summary>
    public sealed class BrandedOptions : BaseOptions
    {
        public new string Title { get; init; } = "";
    }

    /// <summary>Reads the title of options declared as the base type.</summary>
    [Page("/based")]
    public sealed class BasedPage(BaseOptions options) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text(options.Title, TypeRole.BodyM);
    }

    /// <summary>A contract an app registers its service under, as the container keys it.</summary>
    public interface IAccountView
    {
        string Name { get; }
    }

    public sealed class AccountSecrets : IAccountView
    {
        public string Name => "Ada";
        public string Token { get; init; } = "";
    }

    /// <summary>Keeps a service registered under its interface behind a member typed object.</summary>
    [Page("/held-view")]
    public sealed class HeldViewPage : StatelessComponent, IServerPrefetch
    {
        private object? _held;
        private string _status = "";

        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            _held = services.GetRequiredService<IAccountView>();
            _status = "ready";
            return Task.CompletedTask;
        }

        public override VisualNode Build(ComponentContext context) => new Text(_status, TypeRole.BodyM);
    }

    /// <summary>A prefetch that keeps a service behind a member typed object, which the build cannot see into.</summary>
    [Page("/held-service")]
    public sealed class HeldServicePage : StatelessComponent, IServerPrefetch
    {
        private object? _held;
        private string _status = "";

        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            _held = services.GetRequiredService<SiteIdentity>();
            _status = "ready";
            return Task.CompletedTask;
        }

        public override VisualNode Build(ComponentContext context) => new Text(_status, TypeRole.BodyM);
    }

    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(bool signedIn = true)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (signedIn) builder.Services.AddSingleton(new SiteIdentity { Authority = Authority, DisplayName = "Ada" });
        builder.Services.AddSingleton(new SiteOptions { Title = "The Docs", ApiKey = ApiKey });
        builder.Services.AddSingleton<BaseOptions>(new BrandedOptions { Title = "the brand's" });
        builder.Services.AddSingleton<IAccountView>(new AccountSecrets { Token = "tok-0123456789" });
        builder.Services.AddUI(options => options.ScanAssembly(Assembly.GetExecutingAssembly()));
        var app = builder.Build();
        app.MapUI();
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    private static JsonElement PayloadIn(string html)
    {
        var payload = Regex.Match(html, @"window\.__INITIAL_STATE__\s*=\s*(\{.*?\});", RegexOptions.Singleline);
        payload.Success.Should().BeTrue("a page holding a value from the container carries what the browser reads of it");
        return JsonDocument.Parse(payload.Groups[1].Value).RootElement.Clone();
    }

    private static JsonElement EntryOf(JsonElement state, Type component) =>
        state.GetProperty(eQuantic.UI.Web.ComponentIdentity.Key(component, 0));

    [Fact]
    public async Task APresenceTest_SendsWhetherTheValueIsThere_AndNothingOfIt()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        var html = await client.GetStringAsync("/identity");

        html.Should().Contain("your account").And.NotContain("secret-tenant").And.NotContain("Ada");
        var identity = EntryOf(PayloadIn(html), typeof(IdentityPage)).GetProperty("identity");
        identity.ValueKind.Should().Be(JsonValueKind.Object);
        identity.EnumerateObject().Should().BeEmpty();
    }

    [Fact]
    public async Task ANullService_CrossesAsNull()
    {
        var (app, client) = await StartAsync(signedIn: false);
        await using var _ = app;

        var html = await client.GetStringAsync("/identity");

        html.Should().Contain("sign in");
        EntryOf(PayloadIn(html), typeof(IdentityPage)).GetProperty("identity").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task AMemberReadThroughAField_CrossesAlone_UnderTheTwinsName()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        var html = await client.GetStringAsync("/options");

        html.Should().Contain("The Docs").And.NotContain(ApiKey);
        var options = EntryOf(PayloadIn(html), typeof(OptionsPage)).GetProperty("_options");
        options.EnumerateObject().Select(member => member.Name).Should().Equal(["title"]);
        options.GetProperty("title").GetString().Should().Be("The Docs");
    }

    [Fact]
    public async Task ANavigation_CarriesTheProjectionToo()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/options");
        request.Headers.Add("X-EQ-Navigate", "1");
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var text = await response.Content.ReadAsStringAsync();

        text.Should().NotContain(ApiKey);
        var state = JsonDocument.Parse(text).RootElement.GetProperty("state");
        EntryOf(state, typeof(OptionsPage)).GetProperty("_options").GetProperty("title").GetString().Should().Be("The Docs");
    }

    [Fact]
    public async Task AProjectedRead_IsTheMemberCSharpBound_NotOneTheRuntimeTypeHides()
    {
        // The page reads BaseOptions.Title, which the container's BrandedOptions hides with a title of
        // its own. The server draws the base's, so that is the one the browser must be sent.
        var (app, client) = await StartAsync();
        await using var _ = app;

        var html = await client.GetStringAsync("/based");

        html.Should().NotContain("the brand's");
        EntryOf(PayloadIn(html), typeof(BasedPage)).GetProperty("options").GetProperty("title").GetString()
            .Should().Be("");
    }

    [Fact]
    public async Task AServiceRegisteredUnderItsInterface_IsLeftOut_TooWhateverTheBuildSaid()
    {
        // The container keys the registration by IAccountView, so asking it about AccountSecrets alone
        // answers no.
        var (app, client) = await StartAsync();
        await using var _ = app;

        var html = await client.GetStringAsync("/held-view");

        html.Should().NotContain("tok-0123456789");
        var entry = EntryOf(PayloadIn(html), typeof(HeldViewPage));
        entry.TryGetProperty("_held", out var _).Should().BeFalse("the container hands it out under an interface it implements");
        entry.GetProperty("_status").GetString().Should().Be("ready");
    }

    [Fact]
    public async Task AServiceBehindAMemberTypedObject_IsLeftOut_WhateverTheBuildSaid()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        var html = await client.GetStringAsync("/held-service");

        html.Should().NotContain("secret-tenant");
        var entry = EntryOf(PayloadIn(html), typeof(HeldServicePage));
        entry.TryGetProperty("_held", out var _).Should().BeFalse("the container registers its type as a service");
        entry.GetProperty("_status").GetString().Should().Be("ready");
    }
}
