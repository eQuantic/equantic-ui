using System.Reflection;
using System.Text.RegularExpressions;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The document a served page draws, read from the page itself (#1038): the theme's stylesheet on
/// every page, before the app's own CSS, and the app's own CSS before its head tags.
/// <para>
/// Before, the theme's stylesheet was appended after the app's head tags, so the SDK's rule won a
/// tie with the app's, and a page whose app chose no theme carried none at all, so the variables its
/// markup names were defined by nothing.
/// </para>
/// </summary>
public class DocumentShellTests
{
    [Page("/document")]
    public sealed class DocumentPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("document", TypeRole.BodyM);
    }

    [Page("/document-client-only", DisableSsr = true)]
    public sealed class ClientOnlyDocumentPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("client", TypeRole.BodyM);
    }

    private const string ThemeSheet = "/* GENERATED from eQuantic.UI.Primitives design tokens";

    private static async Task<string> PageOf(Action<UIOptions> configure)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options =>
        {
            options.ScanAssembly(Assembly.GetExecutingAssembly());
            configure(options);
        });
        await using var app = builder.Build();
        app.MapUI();
        await app.StartAsync();
        return await app.GetTestClient().GetStringAsync("/document");
    }

    private static string HeadOf(string page) => page[..page.IndexOf("</head>", StringComparison.Ordinal)];

    [Fact]
    public async Task APageWhoseAppChoseNoTheme_CarriesPhotonsStylesheet()
    {
        var page = await PageOf(_ => { });
        var head = HeadOf(page);

        head.Should().Contain(ThemeSheet, "every page is drawn from a theme");
        head.Should().Contain($"--eq-color-background: {TokenCss.Value(PhotonTheme.Instance.Background)};",
            "the variables the lowered markup names are defined on every page");
        head.Should().Contain("body { margin: 0; background: var(--eq-color-background);",
            "the document is the theme's, not the browser's");
        page.Should().NotContain("window.__EQ_THEME__",
            "the runtime already carries Photon's theme, so no data crosses for it");
    }

    [Fact]
    public async Task TheThemesStylesheet_ComesBeforeTheAppsOwnCss_AndTheHeadTagsComeLast()
    {
        var page = await PageOf(options => options
            .UseTheme(PhotonTheme.Instance)
            .ConfigureHtmlShell(shell => shell
                .AddStyles("body { background: white; }")
                .AddStylesheet(new Uri("https://example.com/app.css"))));
        var head = HeadOf(page);

        var invariants = head.IndexOf("#app { height", StringComparison.Ordinal);
        var theme = head.IndexOf(ThemeSheet, StringComparison.Ordinal);
        var own = head.IndexOf("body { background: white; }", StringComparison.Ordinal);
        var tags = head.IndexOf("https://example.com/app.css", StringComparison.Ordinal);

        new[] { invariants, theme, own, tags }.Should().OnlyContain(at => at >= 0);
        invariants.Should().BeLessThan(theme);
        theme.Should().BeLessThan(own, "the app's rule wins a tie with the SDK's by coming later");
        own.Should().BeLessThan(tags);
        page.Should().Contain("window.__EQ_THEME__", "a chosen theme still crosses as data");
    }

    [Fact]
    public async Task AddStyles_Appends_InTheOrderItWasCalled()
    {
        var head = HeadOf(await PageOf(options => options.ConfigureHtmlShell(shell => shell
            .AddStyles(".first { color: red; }")
            .AddStyles(".second { color: blue; }"))));

        head.IndexOf(".first", StringComparison.Ordinal).Should().BeGreaterThan(head.IndexOf(ThemeSheet, StringComparison.Ordinal))
            .And.BeLessThan(head.IndexOf(".second", StringComparison.Ordinal));
        head.Should().Contain("<style>.first { color: red; }</style>").And.Contain("<style>.second { color: blue; }</style>",
            "each call is an element of its own, so a malformed chunk cannot swallow the next one");
    }

    [Fact]
    public async Task AnAppThatAddsNoCss_WritesNoBlockForIt()
    {
        var head = HeadOf(await PageOf(_ => { }));

        Regex.IsMatch(head, @"<style>\s*</style>").Should().BeFalse("an empty style element is a block for nothing");
    }

    /// <summary>A page the server does not render shows a placeholder until its module mounts, and the
    /// theme's stylesheet centres it: the rule lived in the base block AddStyles replaced. And it
    /// answers 200: the error page used to answer for it in production whenever the app declared one,
    /// because the server told a page that opted out from one that broke by the placeholder's text.</summary>
    [Fact]
    public async Task APageTheServerDoesNotRender_ShowsAStyledPlaceholder()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options => options.ScanAssembly(Assembly.GetExecutingAssembly()));
        await using var app = builder.Build();
        app.MapUI();
        await app.StartAsync();
        var response = await app.GetTestClient().GetAsync("/document-client-only");
        var page = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK,
            "a page that opted out of server rendering did not fail, though this assembly declares a /500 page");
        page.Should().Contain("<div class=\"eq-loading\">", "the class boot.ts writes too");
        HeadOf(page).Should().Contain(".eq-loading { display: flex; justify-content: center; align-items: center;");
    }

    [Fact]
    public void AddStyles_RefusesNull() =>
        new Action(() => new HtmlShellOptions().AddStyles(null!)).Should().Throw<ArgumentNullException>();
}
