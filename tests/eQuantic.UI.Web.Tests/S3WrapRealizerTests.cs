using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>Spec S3 on the web realizer: flex-wrap + the "row-gap column-gap" pair when RunGap
/// differs — literal strings the TS lowering mirrors.</summary>
public class S3WrapRealizerTests
{
    private static string StyleOf(FlexNode flex) =>
        WebRealizer.Lower(flex, PhotonTheme.Instance).Style!.ToCssString();

    [Fact]
    public void Wrap_EmitsFlexWrap_AndKeepsTheSingleGap()
    {
        var row = new Row(gap: 8) { Wrap = true };
        StyleOf(row).Should().Contain("flex-wrap: wrap").And.Contain("gap: 8px");
    }

    [Fact]
    public void RunGap_EmitsThePair_InTheStackingOrderOfTheAxis()
    {
        StyleOf(new Row(gap: 8) { Wrap = true, RunGap = 12 })
            .Should().Contain("gap: 12px 8px", "rows stack vertically: row-gap = run");
        StyleOf(new Column(gap: 8) { Wrap = true, RunGap = 12 })
            .Should().Contain("gap: 8px 12px", "columns stack horizontally: column-gap = run");
    }

    [Fact]
    public void NoWrap_NeverEmitsFlexWrap()
    {
        StyleOf(new Row(gap: 8)).Should().NotContain("flex-wrap");
    }
}
