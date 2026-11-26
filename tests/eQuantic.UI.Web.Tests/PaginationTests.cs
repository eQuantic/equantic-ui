using System.Globalization;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A pager speaks the reader's language (#1874): its Previous and Next steps and each page's accessible name
/// come from <see cref="SdkStrings"/> and follow the UI culture, as every other label of the SDK does, where
/// they were English literals in every culture.
/// </summary>
public class PaginationTests
{
    private static HtmlNode Render(VisualNode node, string culture)
    {
        var previousFormat = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            return WebRealizer.Lower(node, PhotonTheme.Instance).Render();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousFormat;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }

    private static IEnumerable<HtmlNode> Walk(HtmlNode node) =>
        new[] { node }.Concat(node.Children.SelectMany(Walk));

    private static string TextOf(HtmlNode node) =>
        node.TextContent is { Length: > 0 } own ? own : string.Concat(node.Children.Select(TextOf));

    private static IReadOnlyList<string> Steps(HtmlNode tree) =>
        Walk(tree).Where(n => n.Tag == "button").Select(TextOf).Where(text => text.Length > 0 && !text.All(char.IsDigit)).ToList();

    private static IReadOnlyList<string?> PageNames(HtmlNode tree) =>
        Walk(tree).Select(n => n.Attributes.GetValueOrDefault("aria-label")).Where(label => label is not null).ToList();

    [Theory]
    [InlineData("en-US", "Previous", "Next", "Page 2")]
    [InlineData("pt-BR", "Anterior", "Próxima", "Página 2")]
    [InlineData("pt-PT", "Anterior", "Seguinte", "Página 2")]
    [InlineData("es-ES", "Anterior", "Siguiente", "Página 2")]
    public void ThePagersStepsAndPages_SpeakTheUiCulture(string culture, string previous, string next, string page)
    {
        var tree = Render(new Pagination(5, 2), culture);

        Steps(tree).Should().Equal(previous, next);
        PageNames(tree).Should().Contain(page);
    }
}
