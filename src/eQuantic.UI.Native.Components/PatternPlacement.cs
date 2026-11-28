using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// The three nested areas of a box a <see cref="Pattern"/> is placed in and clipped to (#1937, #1939),
/// each a rounded rectangle, as CSS computes a background's origin and clip boxes:
/// <list type="bullet">
/// <item>the BORDER area: the bounds, with the box's corner radii;</item>
/// <item>the PADDING area: those less the border on each side that draws one;</item>
/// <item>the CONTENT area: those less the box's padding too.</item>
/// </list>
/// Each area's corners are the box's less what was taken off beside them, never below zero. CSS shrinks
/// a corner's two radii by the two insets beside it, an ellipse; one radius per corner here takes the
/// larger inset, so the inner corner never reaches past the inner edge, the rule the inset highlight
/// already draws by (#1036).
/// </summary>
/// <param name="Border">The whole box.</param>
/// <param name="Padding">The box inside its border.</param>
/// <param name="Content">The box inside its border and its padding, where its child is laid out.</param>
internal readonly record struct PatternPlacement(RRect Border, RRect Padding, RRect Content)
{
    /// <summary>The areas of a box at <paramref name="bounds"/>, as the layout insets its child
    /// (<c>MeasureVisitor.InsetsOf</c>): the border on the sides it is drawn on, then the padding.</summary>
    public static PatternPlacement Of(Rect bounds, CornerRadii radius, float borderWidth, BorderSides sides, EdgeInsets padding)
    {
        var width = Math.Max(borderWidth, 0);
        var border = new EdgeInsets(
            (sides & BorderSides.Start) != 0 ? width : 0,
            (sides & BorderSides.Top) != 0 ? width : 0,
            (sides & BorderSides.End) != 0 ? width : 0,
            (sides & BorderSides.Bottom) != 0 ? width : 0);
        var whole = new RRect(bounds, radius);
        var inside = Inset(whole, border);
        return new PatternPlacement(whole, inside, Inset(inside, padding));
    }

    /// <summary>The area <paramref name="area"/> names.</summary>
    public RRect this[BoxArea area] => area switch
    {
        BoxArea.Border => Border,
        BoxArea.Content => Content,
        _ => Padding,
    };

    /// <summary>
    /// Where <paramref name="pattern"/> is placed: its <see cref="Pattern.Area"/>, or the window when it
    /// is attached to the window and nothing above it transforms. <paramref name="window"/> is the window
    /// in the box's local space; a box under a transform places a window-attached pattern in its own
    /// area, as CSS Transforms 1 §6 paints a fixed background under a transform as a scrolling one.
    /// </summary>
    public Rect PlacingArea(Pattern pattern, Rect? window) =>
        pattern.Attachment == TileAttachment.Window && window is { } against ? against : this[pattern.Area].Rect;

    /// <summary><paramref name="area"/> less <paramref name="insets"/>, its corners less the larger inset beside each.</summary>
    private static RRect Inset(RRect area, EdgeInsets insets)
    {
        var rect = area.Rect;
        var inner = new Rect(rect.X + insets.Start, rect.Y + insets.Top,
            Math.Max(0, rect.Width - insets.Start - insets.End), Math.Max(0, rect.Height - insets.Top - insets.Bottom));
        var radii = area.Radii;
        return new RRect(inner, new CornerRadii(
            Math.Max(0, radii.TopLeft - Math.Max(insets.Start, insets.Top)),
            Math.Max(0, radii.TopRight - Math.Max(insets.End, insets.Top)),
            Math.Max(0, radii.BottomRight - Math.Max(insets.End, insets.Bottom)),
            Math.Max(0, radii.BottomLeft - Math.Max(insets.Start, insets.Bottom))));
    }
}
