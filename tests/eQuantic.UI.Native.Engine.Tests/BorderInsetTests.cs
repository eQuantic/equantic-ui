using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A bordered box keeps its child inside its border, on every side the border is drawn on, and a
/// hugging box counts the border in its size (#629). The web lowers a box <c>border-box</c> and
/// Flutter's <c>Container</c> insets its child by its decoration's border; Photon read the padding
/// alone, laid the child over the border, and a hugging box came out two borders smaller than its web
/// twin.
/// </summary>
public class BorderInsetTests
{
    private static LayoutNode Layout(BoxStyle style)
    {
        var box = new Box(style, new Box(new BoxStyle { Width = 10, Height = 10 }));
        var page = new Column(gap: 0) { Width = SizeValue.Fill };
        page.Add(box);
        var root = PhotonRealizer.Realize(page, 200, 200, PhotonTheme.Instance, ThemeMode.Light,
            new DisplayListBuilder()).Root;
        return root.Children[0];
    }

    [Fact]
    public void ABorderOnEverySide_InsetsTheChild_AndTheBoxHugsItAndTheBorder()
    {
        var box = Layout(new BoxStyle
        {
            Padding = EdgeInsets.All(4),
            BorderWidth = 2,
            BorderColor = new ColorToken(Color.FromRgb(0, 0, 0)),
        });
        var child = box.Children[0];

        (child.Bounds.X - box.Bounds.X).Should().Be(6, "the padding and the border stand before the child");
        (child.Bounds.Y - box.Bounds.Y).Should().Be(6);
        box.Bounds.Width.Should().Be(22, "the child, the padding twice and the border twice");
        box.Bounds.Height.Should().Be(22);
    }

    [Fact]
    public void ABorderOnOneSide_InsetsTheChildOnThatSideAlone()
    {
        var box = Layout(new BoxStyle
        {
            Padding = EdgeInsets.All(4),
            BorderWidth = 3,
            BorderColor = new ColorToken(Color.FromRgb(0, 0, 0)),
            BorderSides = BorderSides.Start,
        });
        var child = box.Children[0];

        (child.Bounds.X - box.Bounds.X).Should().Be(7, "the start border stands before the child");
        (child.Bounds.Y - box.Bounds.Y).Should().Be(4, "and no border is drawn on the top");
        box.Bounds.Width.Should().Be(21);
        box.Bounds.Height.Should().Be(18);
    }
}
