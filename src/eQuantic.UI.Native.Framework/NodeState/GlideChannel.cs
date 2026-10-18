namespace eQuantic.UI.Native.Framework;

/// <summary>
/// What a transition track glides: one number a node lays out or paints with, under its
/// <see cref="Primitives.TransitionSpec"/>. A colour is one channel and four tracks (red, green, blue
/// and alpha, each retargeting on its own, as CSS interpolates a colour); a member of a list is one
/// channel at an index (<see cref="TrackKey"/>).
/// </summary>
public enum GlideChannel
{
    /// <summary>A box's fixed width, glided in layout.</summary>
    Width,

    /// <summary>A box's fixed height, glided in layout.</summary>
    Height,

    /// <summary>A <see cref="Primitives.Flexible"/>'s or a <see cref="Primitives.Spacer"/>'s weight.</summary>
    Flex,

    Opacity,

    TranslateX,

    TranslateY,

    Rotation,

    ScaleX,

    ScaleY,

    /// <summary>The elevation's shadow, resolved from the theme: its offset, blur, spread and colour.</summary>
    ElevationOffsetY,

    ElevationBlur,

    ElevationSpread,

    ElevationColor,

    /// <summary>A custom shadow, at its index among the shadows the box draws.</summary>
    ShadowOffsetY,

    ShadowBlur,

    ShadowSpread,

    ShadowColor,

    Background,

    Border,
}
