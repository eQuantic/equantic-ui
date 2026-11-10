using System.Reflection;
using System.Text.RegularExpressions;
using eQuantic.UI.Gtm;
using eQuantic.UI.Primitives;
using eQuantic.UI.Server.Assets;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// Every script and style the shell writes carries the request's nonce, so a Content-Security-Policy
/// can admit exactly those (#1266).
/// <para>
/// The shell wrote seven inline elements of its own (the framework's and the theme's styles, the
/// import map, the configuration and state, the boot module, two scripts in Development), the app's
/// CSS, the head tags of its integrations (GTM's four scripts, a chart library's CDN script and its
/// initializer) and each page's assets (the atomic sheet among them), and none carried a nonce. A
/// strict policy refused every one of them, and an app could only add <c>'unsafe-inline'</c>, which
/// admits an injected script as readily as the SDK's.
/// </para>
/// <para>
/// The document is read the way a browser's policy reads it: every <c>&lt;script&gt;</c> and every
/// <c>&lt;style&gt;</c> start tag, wherever it is, and no exception for one the test expects.
/// </para>
/// </summary>
public class ShellNonceTests
{
    private static readonly Regex Element = new(@"<(script|style)\b[^>]*>", RegexOptions.IgnoreCase);
    private static readonly Regex Nonce = new("\\snonce=\"([^\"]+)\"");

    [Page("/nonce")]
    public sealed class NoncePage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("nonce", TypeRole.BodyM);
    }

    [Page("/nonce-client-only", DisableSsr = true)]
    public sealed class ClientOnlyNoncePage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("client", TypeRole.BodyM);
    }

    private static async Task<WebApplication> StartAsync(string environment, Action<UIOptions> configure)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
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

    /// <summary>Every script and style start tag of the document, with the nonce each carries.</summary>
    private static List<(string Tag, string? Nonce)> ElementsOf(string document) =>
        Element.Matches(document)
            .Select(element => (element.Value, Nonce.Match(element.Value) is { Success: true } n ? n.Groups[1].Value : null))
            .ToList();

    public static TheoryData<string, string> Pages() => new()
    {
        { Environments.Production, "/nonce" },
        { Environments.Production, "/nonce-client-only" },
        { Environments.Development, "/nonce" },
    };

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task EveryScriptAndStyle_CarriesTheRequestsNonce(string environment, string path)
    {
        await using var app = await StartAsync(environment, options =>
        {
            options.UseGtm("GTM-ABC1234", gtm => gtm.WithConsent());
            options.ConfigureHtmlShell(shell => shell
                .AddStyles("body { margin: 0; }")
                .AddScript(new Uri("https://cdn.example/app.js"))
                .AddHeadTag(HtmlTag.InlineScript("window.__appHead = true;", defer: false)));
        });

        var document = await app.GetTestClient().GetStringAsync(path);
        var elements = ElementsOf(document);

        elements.Should().HaveCountGreaterThan(8, "the shell, the app and GTM write scripts and styles on every page");
        elements.Where(element => element.Nonce is null).Select(element => element.Tag).Should().BeEmpty(
            "a strict policy refuses every script and style without the request's nonce");
        elements.Select(element => element.Nonce).Distinct().Should().ContainSingle(
            "one request has one nonce");
    }

    [Fact]
    public async Task APagesAtomicSheet_CarriesTheNonce()
    {
        await using var app = await StartAsync(Environments.Production, _ => { });

        var document = await app.GetTestClient().GetStringAsync("/nonce");

        ElementsOf(document).Should().ContainSingle(element => element.Tag.Contains("id=\"eq-atomic\""))
            .Which.Nonce.Should().NotBeNull("the page's atomic sheet is one of its assets, written by the shell");
    }

    [Fact]
    public void EveryInlineAsset_IsRenderedWithTheNonce()
    {
        var assets = new AssetCollection();
        new AssetBuilder(assets)
            .AddScript("https://cdn.example/lib.js")
            .AddInlineScript("window.__asset = true;")
            .AddStylesheet("https://cdn.example/lib.css")
            .AddInlineStyle(".asset{color:red}");

        var rendered = string.Join("\n", assets.RenderTags("n0nce"));

        ElementsOf(rendered).Should().HaveCount(3).And.OnlyContain(element => element.Nonce == "n0nce",
            "an external script, an inline script and an inline style are all admitted by the nonce");
        rendered.Should().Contain("<link rel=\"stylesheet\" href=\"https://cdn.example/lib.css\">",
            "a stylesheet link is not an inline style, and carries nothing");
    }

    [Fact]
    public async Task TwoRequests_CarryTwoNonces()
    {
        await using var app = await StartAsync(Environments.Production, _ => { });
        var client = app.GetTestClient();

        var first = ElementsOf(await client.GetStringAsync("/nonce")).Select(element => element.Nonce).Distinct().Single();
        var second = ElementsOf(await client.GetStringAsync("/nonce")).Select(element => element.Nonce).Distinct().Single();

        first.Should().NotBe(second, "a nonce is never served twice");
        Convert.FromBase64String(first!).Should().HaveCount(16, "a nonce is 128 random bits");
    }

    [Fact]
    public void AHeadTag_StampsTheNonceOnlyOnAScriptOrAStyle()
    {
        HtmlTag.InlineScript("x()", defer: false).Render("n0nce").Should().Be("<script nonce=\"n0nce\">x()</script>");
        new HtmlTag("style") { Content = "a{}" }.Render("n0nce").Should().Be("<style nonce=\"n0nce\">a{}</style>");
        HtmlTag.Link("preconnect", "https://cdn.example/").Render("n0nce")
            .Should().Be("<link rel=\"preconnect\" href=\"https://cdn.example/\">");
    }
}
