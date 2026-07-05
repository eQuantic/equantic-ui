using eQuantic.UI.Core;
using eQuantic.UI.Web.Components;
using eQuantic.UI.Heroicons;
using FluentAssertions;

namespace eQuantic.UI.Heroicons.Tests;

public class HeroiconsIconsTests
{
    [Fact]
    public void Heroicons_AcademicCap_ShouldReturnIconWithCorrectContent()
    {
        var icon = HeroIcons.AcademicCap();
        
        icon.Name.Should().Be("academic-cap");
        icon.Content.Should().NotBeEmpty();
        
        var firstElement = icon.Content[0] as DynamicElement;
        firstElement.Should().NotBeNull();
        firstElement!.TagName.Should().Be("path");
    }

    [Fact]
    public void HeroiconsIcon_Build_ShouldHaveCorrectRootAttributes()
    {
        var icon = HeroIcons.AcademicCap(size: 32, color: "blue");
        var context = new RenderContext();
        
        var built = icon.Build(context) as DynamicElement;
        
        built.Should().NotBeNull();
        built!.TagName.Should().Be("svg");
        built.CustomAttributes["width"].Should().Be("32");
        built.CustomAttributes["style"].Should().Contain("color: blue");
        built.CustomAttributes["stroke"].Should().Be("currentColor");
    }
}
