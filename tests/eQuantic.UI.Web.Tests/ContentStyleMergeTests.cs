using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// <see cref="ContentStyle.Merge"/>, the one rule every producer lays a box's look over the look around
/// it by (#1577), as Flutter's <c>DefaultTextStyle.merge</c>: what the inner look names wins, what it
/// leaves null is the outer one's, and a type style rides the role it was written for.
/// </summary>
public class ContentStyleMergeTests
{
    private static readonly TypeStyle ButtonLabel = TypeStyle.OfSize(15, FontWeight.SemiBold);

    [Fact]
    public void WithNothingOnOneSide_TheOtherIsTheLook()
    {
        var look = new ContentStyle { Role = TypeRole.Label };

        ContentStyle.Merge(null, null).Should().BeNull();
        ContentStyle.Merge(null, look).Should().BeSameAs(look, "nothing is made where nothing is laid over");
        ContentStyle.Merge(look, null).Should().BeSameAs(look);
    }

    [Fact]
    public void WhatTheInnerLookNames_Wins_AndWhatItLeavesIsTheOuters()
    {
        var outer = new ContentStyle
        {
            Role = TypeRole.Label, Type = ButtonLabel, MaxLines = 2, Align = TextAlignment.Center, IconSize = IconSize.Lg,
        };
        var inner = new ContentStyle { MaxLines = 1, IconSize = IconSize.Sm };

        ContentStyle.Merge(outer, inner).Should().Be(new ContentStyle
        {
            Role = TypeRole.Label, Type = ButtonLabel, MaxLines = 1, Align = TextAlignment.Center, IconSize = IconSize.Sm,
        });
    }

    [Fact]
    public void AnInnerRole_DropsTheOuterTypeStyle()
    {
        var outer = new ContentStyle { Role = TypeRole.Label, Type = ButtonLabel };

        ContentStyle.Merge(outer, new ContentStyle { Role = TypeRole.Caption }).Should()
            .Be(new ContentStyle { Role = TypeRole.Caption },
                "a style cut for the outer role must not reach a text set in the inner one");
        ContentStyle.Merge(outer, new ContentStyle { Role = TypeRole.Caption, Type = ButtonLabel }).Should()
            .Be(new ContentStyle { Role = TypeRole.Caption, Type = ButtonLabel }, "unless the inner look names its own");
    }
}
