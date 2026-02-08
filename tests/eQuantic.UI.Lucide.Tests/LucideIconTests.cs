using eQuantic.UI.Core;
using eQuantic.UI.Components;
using eQuantic.UI.Lucide;
using FluentAssertions;

namespace eQuantic.UI.Lucide.Tests;

public class LucideIconTests
{
    [Fact]
    public void Lucide_Check_ShouldReturnIconWithCorrectContent()
    {
        var icon = Lucide.Check();
        
        icon.Name.Should().Be("check");
        icon.Content.Should().HaveCount(1);
        var element = icon.Content[0] as DynamicElement;
        element.Should().NotBeNull();
        element!.TagName.Should().Be("path");
        element.CustomAttributes["d"].Should().Be("M20 6 9 17l-5-5");
    }

    [Fact]
    public void LucideIcon_Build_ShouldReturnSvgElement()
    {
        var icon = Lucide.Activity(size: 32, color: "red");
        var context = new RenderContext();
        
        var built = icon.Build(context) as DynamicElement;
        
        built.Should().NotBeNull();
        built!.TagName.Should().Be("svg");
        built.CustomAttributes["width"].Should().Be("32");
        built.CustomAttributes["stroke"].Should().Be("red");
        built.Children.Should().HaveCount(1);
    }
}
