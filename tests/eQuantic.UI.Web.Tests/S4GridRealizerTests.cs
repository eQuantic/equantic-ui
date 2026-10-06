using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>Spec S4 on the web realizer — CSS Grid literals the TS lowering mirrors.</summary>
public class S4GridRealizerTests
{
    [Fact]
    public void Grid_LowersToCssGrid_WithTracksGapAndSpans()
    {
        var grid = new Grid([GridTrack.Flex(1), GridTrack.Flex(3), GridTrack.Fixed(240)], gap: 12, rowGap: 16)
        {
            Width = SizeValue.Fill,
        };
        grid.Add(new Primitives.Box(new BoxStyle { Height = 40 }) { GridSpan = 2 });
        grid.Add(new Primitives.Box(new BoxStyle { Height = 40 }));

        var element = WebRealizer.Lower(grid, PhotonTheme.Instance);
        var style = element.Style!.ToCssString();

        style.Should().Contain("display: grid")
            .And.Contain("grid-template-columns: 1fr 3fr 240px")
            .And.Contain("gap: 16px 12px", "row-gap then column-gap");
        ((HtmlElement)element.Children[0]).Style!.ToCssString().Should().Contain("grid-column: span 2");
        ((HtmlElement)element.Children[1]).Style!.ToCssString().Should().NotContain("grid-column");
    }

    /// <summary>An auto-fill track is the whole column list: as many as fit the minimum, sharing the rest.</summary>
    [Fact]
    public void AutoFill_LowersToARepeatOfAMinmax()
    {
        var grid = new Grid([GridTrack.AutoFill(210)], gap: 10) { Width = SizeValue.Fill };

        var style = WebRealizer.Lower(grid, PhotonTheme.Instance).Style!.ToCssString();

        style.Should().Contain("grid-template-columns: repeat(auto-fill, minmax(210px, 1fr))");
    }

    /// <summary>Beside another track an auto-fill track would need a solver: refused at construction.</summary>
    [Fact]
    public void AutoFill_BesideAnotherTrack_IsRefused()
    {
        var act = () => new Grid([GridTrack.AutoFill(210), GridTrack.Fixed(40)]);

        act.Should().Throw<ArgumentException>().WithMessage("*whole column list*");
    }

    [Fact]
    public void AutoTrack_LowersToAuto_AndSingleGapCollapses()
    {
        var grid = new Grid([GridTrack.Auto, GridTrack.Flex()], gap: 8);
        WebRealizer.Lower(grid, PhotonTheme.Instance).Style!.ToCssString()
            .Should().Contain("grid-template-columns: auto 1fr").And.Contain("gap: 8px");
    }
}
