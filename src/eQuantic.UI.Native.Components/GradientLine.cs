using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// The line a box's linear gradient runs along on Photon, placed as the web's CSS places it, so a
/// gradient sits on a box the same way on both targets (#1955).
/// <para>
/// A side runs edge to edge. A corner does not run corner to corner: CSS's corner keywords (CSS
/// Images, "magic corners") turn the line until it is perpendicular to the diagonal through the
/// OTHER two corners, so the gradient's middle passes through those two corners, and its ends are
/// where the lines through the named corners meet it. On a square box the two rules agree. On a
/// 400 by 240 box they do not: corner to corner put the top-right corner 73.5% of the way along,
/// where Chromium 156 draws it at 50%, and reads (300, 0) at 37.5%, which is this line's answer.
/// </para>
/// </summary>
internal static class GradientLine
{
    /// <summary>The start and the end of <paramref name="direction"/>'s line across <paramref name="box"/>.</summary>
    public static (Point Start, Point End) Across(Rect box, GradientDirection direction)
    {
        var (w, h) = (box.Width, box.Height);
        return direction switch
        {
            GradientDirection.ToBottom => (new Point(box.X, box.Y), new Point(box.X, box.Y + h)),
            GradientDirection.ToBottomRight => Corner(box, towardRight: true),
            GradientDirection.ToBottomLeft => Corner(box, towardRight: false),
            _ => (new Point(box.X, box.Y), new Point(box.X + w, box.Y)),
        };
    }

    /// <summary>
    /// Toward the bottom corner on one side. The line runs through the centre, along (h, w) toward the
    /// bottom right, the perpendicular of the diagonal from the bottom-left corner to the top-right
    /// one, and half of it is the half-diagonal's projection on it: w·h / √(w² + h²). Toward the
    /// bottom left, it is the same line mirrored.
    /// </summary>
    private static (Point Start, Point End) Corner(Rect box, bool towardRight)
    {
        var (w, h) = (box.Width, box.Height);
        var center = new Point(box.X + w / 2, box.Y + h / 2);
        var squared = w * w + h * h;
        // A box with no area has no diagonal: the line is a point, and the gradient its first stop.
        if (squared <= 0) return (center, center);

        // (h, w) scaled by w·h / (w² + h²): the half-line from the centre to the end.
        var dx = w * h * h / squared;
        var dy = w * w * h / squared;
        if (!towardRight) dx = -dx;
        return (new Point(center.X - dx, center.Y - dy), new Point(center.X + dx, center.Y + dy));
    }
}
