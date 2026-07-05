using eQuantic.UI.Lucide;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Lucide.Tests;

/// <summary>The Lucide catalog on the write-once architecture: glyphs are target-neutral
/// <see cref="IconGlyph"/> data consumed by the Icon node on BOTH targets.</summary>
public class LucideIconTests
{
    [Fact]
    public void Check_IsAStrokeGlyphWithPathData()
    {
        LucideIcons.Check.Name.Should().Be("check");
        LucideIcons.Check.Path.Should().Be("M20 6L9 17l-5-5");
        LucideIcons.Check.Style.Should().Be(IconGlyphStyle.Stroke, "Lucide is the outline family");
        LucideIcons.Check.ViewBox.Should().Be("0 0 24 24");
        LucideIcons.Check.StrokeWidth.Should().Be(2);
    }

    [Fact]
    public void ShapeIcons_ConvertToPathData()
    {
        // alarm-clock is a <circle> + <path> upstream — the generator flattens shapes to path data.
        LucideIcons.AlarmClock.Path.Should().StartWith("M4 13a8 8 0 1 0 16 0a8 8 0 1 0 -16 0",
            "the circle converts to two arcs");
        LucideIcons.AlarmClock.Path.Should().Contain("M12 9v4l2 2");
    }

    [Fact]
    public void PackGlyphs_ComposeWithTheIconNode()
    {
        var icon = new Icon(LucideIcons.Camera, IconSize.Md);
        icon.Glyph.Name.Should().Be("camera");
        icon.Glyph.Style.Should().Be(IconGlyphStyle.Stroke);
        icon.Size.Should().Be(24);
    }
}
