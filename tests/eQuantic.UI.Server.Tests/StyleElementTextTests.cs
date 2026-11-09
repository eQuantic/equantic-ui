using System.Reflection;
using eQuantic.UI.Primitives;
using eQuantic.UI.Server.Assets;
using eQuantic.UI.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// #1295: a style element's text can never end the element. An HTML parser ends it at the first
/// <c>&lt;/style</c>, so a value that held one ran what followed as markup.
/// </summary>
public class StyleElementTextTests
{
    private const string Breakout = "</style><script>alert(1)</script>";

    [Page("/style-text")]
    public sealed class StyleTextPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("styled", TypeRole.BodyM);
    }

    /// <summary>How many elements the text closes: each <c>&lt;/style</c> a parser meets, case folded.</summary>
    private static int Closes(string html) =>
        System.Text.RegularExpressions.Regex.Matches(html, "</style", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;

    [Fact]
    public void AnAtomicValueThatWouldCloseItsElement_StaysInIt()
    {
        var sink = new StyleSink();
        StyleAtomizer.Atomize(new HtmlStyle { BackgroundImage = $"url(\"{Breakout}\")" },
            ThemeVarMap.For(PhotonTheme.Instance), sink);

        var html = new InlineStyleAsset(sink.Css, "eq-atomic").Render();

        sink.Css.Should().Contain(Breakout, "the value reached the sheet as it came");
        Closes(html).Should().Be(1, "only the element's own end closes it");
        html.Should().EndWith("</style>").And.Contain(@"\3c /style><script>alert(1)\3c /script>");
    }

    [Fact]
    public void ACaseFoldedEndTag_StaysInItsElementToo() =>
        Closes(new InlineStyleAsset("a{content:\"</STYLE ><script>alert(1)</script>\"}").Render()).Should().Be(1);

    [Fact]
    public async Task AnAppsOwnStyles_CannotEndTheirElement()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options => options
            .ScanAssembly(Assembly.GetExecutingAssembly())
            .ConfigureHtmlShell(shell => shell.AddStyles($"a::after {{ content: \"{Breakout}\"; }}")));
        await using var app = builder.Build();
        app.MapUI();
        await app.StartAsync();

        var page = await app.GetTestClient().GetStringAsync("/style-text");

        page.Should().NotContain(Breakout).And.Contain(@"a::after { content: ""\3c /style><script>alert(1)\3c /script>""; }</style>");
    }
}
