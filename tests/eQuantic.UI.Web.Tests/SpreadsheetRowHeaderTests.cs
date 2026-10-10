using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The spreadsheet's row numbers keep their strip on the web (#613). The strip was a Column that hugged
/// its row headers, and a row header is a Stack whose layers may not grow past it (<c>min-width: 0</c>),
/// so the strip's own minimum was zero. Beside a grid wider than the window the browser shrank it to
/// nothing, measured in Chromium at 0px: every cell moved left by the strip's 44px, A1 over row 1's
/// number and column B under the "A". Photon never shrinks a rigid child, and its cells start after the
/// strip (<c>SpreadsheetComponentTests</c> presses A1 there). The strip is the header width it always
/// was, and says so: a Fixed size, which shrinks on neither target.
/// </summary>
public class SpreadsheetRowHeaderTests
{
    [Fact]
    public void TheRowNumberStrip_HoldsItsWidthBesideAWideGrid()
    {
        var root = WebRealizer.Lower(new Spreadsheet(new SheetController()), PhotonTheme.Instance).Render();

        var strip = FirstSiblingOf(root, "grid");

        strip.Should().NotBeNull("the row numbers sit beside the grid they number");
        strip!.Attributes.GetValueOrDefault("style", "").Should()
            .Contain($"width: {TokenCss.Px(Spreadsheet.HeaderWidth)}")
            .And.Contain("flex-shrink: 0", "a strip that shrinks takes its numbers out from under the cells");
    }

    /// <summary>The first child of the element that holds the one carrying <paramref name="role"/>.</summary>
    private static HtmlNode? FirstSiblingOf(HtmlNode node, string role)
    {
        foreach (var child in node.Children)
        {
            if (child.Attributes.GetValueOrDefault("role") == role) return node.Children[0];
            if (FirstSiblingOf(child, role) is { } found) return found;
        }
        return null;
    }
}
