using System.Reflection;
using System.Text.RegularExpressions;
using eQuantic.UI.Charts.ApexCharts;
using eQuantic.UI.Gtm;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The Content-Security-Policy is a builder setting (#1267): the app turns it on in C#, states only
/// the origins its own code connects to or is framed by, reads what varies per environment from
/// <c>EQuantic:ContentSecurityPolicy</c>, and the SDK adds what its own features need.
/// <para>
/// Before, there was no way to state one: an app writing the header by hand had to know what the
/// shell, its scripts and its integrations needed, and could only admit the shell's inline scripts
/// with <c>'unsafe-inline'</c>, which admits an injected script as readily.
/// </para>
/// </summary>
public class ContentSecurityPolicyTests
{
    private const string Enforcing = "Content-Security-Policy";
    private const string Reporting = "Content-Security-Policy-Report-Only";

    [Page("/csp")]
    public sealed class PolicyPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("csp", TypeRole.BodyM);
    }

    private static async Task<WebApplication> StartAsync(
        Action<UIOptions> configure, IDictionary<string, string?>? configuration = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (configuration is not null) builder.Configuration.AddInMemoryCollection(configuration);
        builder.Services.AddUI(options =>
        {
            options.ScanAssembly(Assembly.GetExecutingAssembly());
            configure(options);
        });
        var app = builder.Build();
        app.MapUI();
        await app.StartAsync();
        return app;
    }

    private static string NonceOf(string document) =>
        Regex.Match(document, "<script[^>]*\\snonce=\"([^\"]+)\"").Groups[1].Value;

    [Fact]
    public async Task AnAppThatTurnedNoPolicyOn_IsServedWithout()
    {
        await using var app = await StartAsync(_ => { });

        var response = await app.GetTestClient().GetAsync("/csp");

        response.Headers.Contains(Enforcing).Should().BeFalse("the policy is the app's choice");
        response.Headers.Contains("X-Content-Type-Options").Should().BeFalse("its headers come with it");
    }

    [Fact]
    public async Task TheStrictPolicy_AdmitsThePagesOwnNonce_AndNothingElse()
    {
        await using var app = await StartAsync(ui => ui.UseContentSecurityPolicy());

        var response = await app.GetTestClient().GetAsync("/csp");
        var document = await response.Content.ReadAsStringAsync();

        response.Headers.GetValues(Enforcing).Should().ContainSingle().Which.Should().Be(
            $"script-src 'nonce-{NonceOf(document)}' 'strict-dynamic'; style-src 'nonce-{NonceOf(document)}'; "
            + "style-src-attr 'unsafe-inline'; object-src 'none'; base-uri 'none'; frame-ancestors 'self'; connect-src 'self'",
            "the page's scripts carry the nonce the policy names, and nothing else is admitted");
        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        response.Headers.GetValues("Referrer-Policy").Should().Equal("strict-origin-when-cross-origin");
    }

    [Fact]
    public async Task TheAppsOwnSources_AndItsConfiguration_MakeOnePolicy()
    {
        await using var app = await StartAsync(
            ui => ui.UseContentSecurityPolicy(csp => csp
                .AllowConnectionsTo("https://api.example.com")
                .AllowFramingBy("https://partner.example.com")
                .ReportViolationsTo(new Uri("/csp-reports", UriKind.Relative))),
            new Dictionary<string, string?>
            {
                [$"{ContentSecurityPolicyOptions.SectionName}:ReportOnly"] = "true",
                [$"{ContentSecurityPolicyOptions.SectionName}:ConnectSources:0"] = "wss://*.live.example.com",
                [$"{ContentSecurityPolicyOptions.SectionName}:ReportUri"] = "https://reports.example.com/csp",
            });

        var response = await app.GetTestClient().GetAsync("/csp");

        response.Headers.Contains(Enforcing).Should().BeFalse("the environment asked for report-only");
        var policy = response.Headers.GetValues(Reporting).Should().ContainSingle().Subject;
        policy.Should().Contain("connect-src 'self' wss://*.live.example.com https://api.example.com")
            .And.Contain("frame-ancestors 'self' https://partner.example.com")
            .And.EndWith("report-uri https://reports.example.com/csp", "the environment's report URI wins over Program.cs's");
    }

    [Fact]
    public async Task UseGtm_AddsTheHostsTheContainerConnectsTo_AndHandsItTheNonce()
    {
        await using var app = await StartAsync(ui => ui
            .UseGtm("GTM-ABC1234")
            .UseContentSecurityPolicy());

        var response = await app.GetTestClient().GetAsync("/csp");
        var document = await response.Content.ReadAsStringAsync();

        response.Headers.GetValues(Enforcing).Single().Should().Contain(
            "connect-src 'self' https://*.googletagmanager.com https://*.google-analytics.com https://*.analytics.google.com",
            "the app wrote no host for GTM: the SDK knows what its own integration connects to");
        document.Should().Contain("j.setAttribute('nonce',n.nonce||n.getAttribute('nonce'))",
            "the container script carries the page's nonce, which GTM hands the tags it writes");
    }

    [Fact]
    public void ApexCharts_IsHandedThePagesNonce_ForTheStyleElementItCreates()
    {
        var options = new UIOptions().UseApexCharts();

        options.HtmlShell.HeadTags.Should().ContainSingle(tag => tag.Content != null && tag.Content.Contains("data-apex-init"))
            .Which.Content.Should().Contain("cfg.chart.nonce=n.nonce",
                "ApexCharts writes its CSS in a style element it creates, which a strict style-src refuses without the nonce");
    }

    [Fact]
    public async Task AFeatureDeclaredAfterThePolicy_IsStillInIt()
    {
        await using var app = await StartAsync(ui => ui
            .UseContentSecurityPolicy()
            .UseGtm("GTM-ABC1234"));

        var response = await app.GetTestClient().GetAsync("/csp");

        response.Headers.GetValues(Enforcing).Single().Should().Contain("https://*.google-analytics.com");
    }

    [Theory]
    [InlineData("ConnectSources:0", "api.example.com")]
    [InlineData("ConnectSources:0", "https://api.example.com; script-src *")]
    [InlineData("FrameAncestors:0", "'unsafe-inline'")]
    [InlineData("ReportUri", "/r; script-src *")]
    [InlineData("ReportUri", "javascript:alert(1)")]
    public async Task ASourceThePolicyCouldNotWrite_StopsTheApp(string key, string value)
    {
        var start = () => StartAsync(
            ui => ui.UseContentSecurityPolicy(),
            new Dictionary<string, string?> { [$"{ContentSecurityPolicyOptions.SectionName}:{key}"] = value });

        await start.Should().ThrowAsync<OptionsValidationException>()
            .WithMessage($"*{value}*", "a malformed source would reach the header, where the browser drops it in silence");
    }

    [Fact]
    public async Task EveryResponse_CarriesTheHeadersThatComeWithThePolicy()
    {
        await using var app = await StartAsync(ui => ui.UseContentSecurityPolicy());

        var response = await app.GetTestClient().GetAsync("/_equantic/runtime.js");

        response.Headers.GetValues("X-Content-Type-Options").Should().Equal(
            new[] { "nosniff" }, "a script is read only as the type it was sent as");
        response.Headers.Contains(Enforcing).Should().BeFalse("a policy governs a document, and this is a script");
    }

    [Fact]
    public void UnderDotnetWatch_ThePolicyOnlyReports()
    {
        var options = new ContentSecurityPolicyOptions();

        new ContentSecurityPolicy(options, dotnetWatch: "1").For("n0nce").Name.Should().Be(Reporting,
            "dotnet watch injects a script of its own, which no nonce admits");
        new ContentSecurityPolicy(options, dotnetWatch: null).For("n0nce").Name.Should().Be(Enforcing);
    }
}
