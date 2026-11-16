namespace eQuantic.UI.Native.Engine;

/// <summary>
/// A run of recorded draw commands that can be drawn again in a later frame, with the textures they draw:
/// Flutter's <c>Picture</c>, which a <c>PictureRecorder</c> makes and <c>Canvas.drawPicture</c> draws.
/// Made by <see cref="DisplayListBuilder.PictureFrom"/> and drawn by <see cref="DisplayListBuilder.DrawPicture"/>.
/// <para>
/// A command alone is not enough to draw again. A text's or an icon's command names its texture by its index
/// in the table of the frame that recorded it (<see cref="DrawCommand.TextureId"/>), and the next frame's table
/// starts empty, so the same index names another texture there, or none. A picture keeps each texture beside
/// the command that draws it and registers it again in the frame it is drawn in.
/// </para>
/// </summary>
public sealed class Picture
{
    internal Picture(DrawCommand[] commands, TextureData?[] textures)
    {
        Commands = commands;
        Textures = textures;
    }

    /// <summary>The commands, transforms and clips baked as they were recorded.</summary>
    internal DrawCommand[] Commands { get; }

    /// <summary>The texture each command draws, index for index, or null for a command that draws none.</summary>
    internal TextureData?[] Textures { get; }

    /// <summary>How many commands the picture holds.</summary>
    public int Count => Commands.Length;
}
