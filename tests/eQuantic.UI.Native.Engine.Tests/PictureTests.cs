using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A <see cref="Picture"/> drawn in a later frame draws its own textures: a text's command names its texture by
/// its index in the frame that recorded it, and the next frame's table starts empty.
/// </summary>
public class PictureTests
{
    [Fact]
    public void APictureDrawnInAnotherFrame_DrawsItsOwnTexture()
    {
        var glyph = new TextureData(1, 1, [255]);
        var other = new TextureData(1, 1, [128]);
        var recorded = new DisplayListBuilder();
        recorded.Texture(new Rect(0, 0, 10, 10), Color.FromRgb(0, 0, 0), glyph);
        var picture = recorded.PictureFrom(0);

        var later = new DisplayListBuilder();
        later.Texture(new Rect(0, 0, 10, 10), Color.FromRgb(0, 0, 0), other);
        later.DrawPicture(picture, Matrix2D.Translation(5, 0));
        var list = later.Build();

        var drawn = list.Commands[1];
        list.Textures[drawn.TextureId].Should().BeSameAs(glyph,
            "replayed by its index alone, the glyph would have drawn the texture the later frame registered first");
        drawn.Transform.M31.Should().Be(5);
    }
}
