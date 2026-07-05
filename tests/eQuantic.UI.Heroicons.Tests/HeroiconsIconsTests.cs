using eQuantic.UI.Heroicons;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Heroicons.Tests;

/// <summary>The Heroicons catalog on the write-once architecture.</summary>
public class HeroiconsIconsTests
{
    [Fact]
    public void Catalog_CarriesGlyphData()
    {
        HeroIcons.Check.Path.Should().NotBeNullOrEmpty();
        HeroIcons.Check.Name.Should().Be("check");
    }

    [Fact]
    public void PackGlyphs_ComposeWithTheIconNode()
    {
        var icon = new Icon(HeroIcons.Check, IconSize.Dense);
        icon.Glyph.Path.Should().Be(HeroIcons.Check.Path);
    }
}
