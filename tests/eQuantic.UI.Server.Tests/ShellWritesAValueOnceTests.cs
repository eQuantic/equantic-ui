using System.Reflection;
using System.Text.Json;
using System.Web;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The shell fills each placeholder of its template once, and never reads what it wrote as template
/// (#1284).
/// <para>
/// It used to replace variable after variable over the whole document, so a placeholder inside a
/// value already written was replaced again by a later variable. The server-rendered markup is
/// written before the page's state, and HTML encoding leaves <c>{</c> and <c>}</c> alone, so a
/// visitor's <c>{{InitialState}}</c> in an input became the state's JSON there, and the JSON's first
/// quote closed the attribute: <c>"{{InitialState}} onmouseover=alert(1) x"</c> gave the input a
/// real <c>onmouseover</c>. One stored or reflected value, drawn in an attribute and kept in the
/// page's state, was enough.
/// </para>
/// </summary>
public class ShellWritesAValueOnceTests
{
    /// <summary>What a visitor typed: a placeholder of the shell, then the attribute it smuggles.</summary>
    private const string Typed = "{{InitialState}} onmouseover=alert(1) x";

    [Fact]
    public void AValueHoldingOtherPlaceholders_IsWrittenAsItCame()
    {
        var engine = new HtmlTemplateEngine("<p>{{A}}</p><i title=\"{{B}}\"></i>{{C}}{{Missing}}");

        var html = engine.Render((TemplateContext ctx) => ctx.Set("A", "{{B}}{{C}}").Set("B", "{{A}}").Set("C", "c"));

        html.Should().Be("<p>{{B}}{{C}}</p><i title=\"{{A}}\"></i>c{{Missing}}",
            "a value is written once, and a placeholder of the template with no variable stays as it is");
    }

    [Fact]
    public void TheShellsOwnTemplate_KeepsAVisitorsPlaceholderInsideItsAttribute()
    {
        // The markup and the state as the shell writes them: the attribute encoded as HtmlRenderer
        // encodes one, the state serialized by System.Text.Json's default encoder.
        var markup = $"<input value=\"{HttpUtility.HtmlAttributeEncode(Typed)}\">";
        var state = JsonSerializer.Serialize(new Dictionary<string, object> { ["Page:0"] = new { q = Typed } });
        var engine = HtmlTemplateEngine.FromResource(
            "eQuantic.UI.Server.Templates.app-shell.html", typeof(HtmlTemplateEngine).Assembly);

        var html = engine.Render((TemplateContext ctx) =>
        {
            ctx.Set("HtmlClass", "").Set("HtmlLang", "en").Set("Title", "t").Set("MetadataTags", "")
               .Set("BuildId", "b").Set("ThemeStyles", "").Set("HeadTags", "").Set("SsrEnabled", "true")
               .Set("SsrContent", markup).Set("ConfigJson", "{}").Set("IsDevelopmentBool", "false")
               .SetOrEmpty("InitialState", $"window.__INITIAL_STATE__ = {state};");
            ctx.When("SsrEnabled", true).When("HasInitialState", true);
        });

        InputOf(html).Should().Be($"<input value=\"{Typed}\">",
            "the visitor's text is written in the attribute as it came, and the state stays in its script");
    }

    /// <summary>A page whose server data holds what a visitor once typed, drawn in a field.</summary>
    [Page("/one-pass")]
    public sealed class StoredTextPage : StatelessComponent, IServerPrefetch
    {
        public string Stored = "";

        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            Stored = Typed;
            return Task.CompletedTask;
        }

        public override VisualNode Build(ComponentContext context) => new TextEntry(Stored, null);
    }

    [Fact]
    public async Task AServedPage_KeepsAStoredPlaceholderInsideItsField()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options => options.ScanAssembly(Assembly.GetExecutingAssembly()));
        await using var app = builder.Build();
        app.MapUI();
        await app.StartAsync();

        var page = await app.GetTestClient().GetStringAsync("/one-pass");

        page.Should().Contain("window.__INITIAL_STATE__", "the stored text is part of the page's state");
        var input = InputOf(page);
        input.Should().Contain($"value=\"{Typed}\"", "the field draws the stored text as it was typed");
        input.Should().NotContain("__INITIAL_STATE__", "the state is written in its script, never in the field");
    }

    private static string InputOf(string html)
    {
        var start = html.IndexOf("<input", StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, "the page draws a field");
        return html[start..(html.IndexOf('>', start) + 1)];
    }
}
