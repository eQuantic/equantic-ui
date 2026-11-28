namespace eQuantic.UI.Primitives;

/// <summary>
/// One of the three nested areas of a box (#1937): its bounds, those less its border, and those less
/// its padding too. A <see cref="Pattern"/> is placed in one of them and clipped to another, and each
/// area keeps the box's rounded corners, less what was taken off, never below zero.
/// </summary>
public enum BoxArea : byte
{
    /// <summary>The whole box, border included.</summary>
    Border = 0,

    /// <summary>The box inside its border, on the sides that draw one.</summary>
    Padding = 1,

    /// <summary>The box inside its border and its padding: where its child is laid out.</summary>
    Content = 2,
}
