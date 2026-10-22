using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;
using eQuantic.UI.Web;
using FluentAssertions;
using eQuantic.UI.Spreadsheets;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The spreadsheet's row numbers keep their place on the web (#613). They were a strip beside the grid
/// that hugged its headers, and beside a grid wider than the window the browser shrank it to nothing,
/// measured in Chromium at 0px: every cell moved left by the strip's 44px, A1 over row 1's number and
/// column B under the "A". The numbers are now the table's pinned first column, so the browser lays out
/// none of it: each cell is placed by the column's offset, and the first column of cells starts where
/// the numbers end, by construction.
/// </summary>
public class SpreadsheetRowHeaderTests
{
    [Fact]
    public void TheFirstColumnOfCells_StartsWhereTheRowNumbersEnd()
    {
        var root = WebRealizer.Lower(new Spreadsheet(new SheetController()), PhotonTheme.Instance).Render();

        var placed = Placed(root).ToList();
        var number = placed.First(p => p.Text == "1");
        var a1 = placed.First(p => p.Text == "A");

        number.Left.Should().Be("0", "the numbers are the pinned column, at the band's edge");
        number.Width.Should().Be(TokenCss.Px(Spreadsheet.HeaderWidth));
        a1.Width.Should().Be(TokenCss.Px(96), "column A's header is as wide as column A");
        Bands(root).Should().Contain(TokenCss.Px(Spreadsheet.HeaderWidth),
            "the band the cells scroll in starts where the numbers end");
    }

    private sealed record PlacedCell(string? Left, string? Width, string Text);

    /// <summary>Every absolutely placed cell, with the text it holds.</summary>
    private static IEnumerable<PlacedCell> Placed(HtmlNode node)
    {
        var style = node.Attributes.GetValueOrDefault("style") ?? "";
        if (style.Contains("position: absolute") && style.Contains("display: grid"))
            yield return new PlacedCell(Read(style, "left"), Read(style, "width"), TextOf(node));
        foreach (var child in node.Children)
            foreach (var cell in Placed(child))
                yield return cell;
    }

    /// <summary>The left edges of the bands that clip the cells.</summary>
    private static IEnumerable<string?> Bands(HtmlNode node)
    {
        var style = node.Attributes.GetValueOrDefault("style") ?? "";
        if (style.Contains("overflow: hidden") && style.Contains("position: absolute")) yield return Read(style, "left");
        foreach (var child in node.Children)
            foreach (var band in Bands(child))
                yield return band;
    }

    private static string? Read(string style, string property)
    {
        foreach (var declaration in style.Split(';'))
        {
            var pair = declaration.Split(':', 2);
            if (pair.Length == 2 && pair[0].Trim() == property) return pair[1].Trim();
        }
        return null;
    }

    private static string TextOf(HtmlNode node) =>
        node.TextContent ?? string.Concat(node.Children.Select(TextOf));
}
