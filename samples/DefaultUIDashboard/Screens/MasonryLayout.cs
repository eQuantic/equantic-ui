using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// A masonry grid, written in C# (#1556): columns of one width, each child under the shortest column, and the grid
/// as tall as its tallest column. Its size follows its children, so it overrides <c>Layout</c> rather than
/// <c>GetSize</c>.
/// </summary>
public sealed class MasonryLayout(int columns, float gap) : MultiChildLayoutDelegate
{
    public override Size Layout(BoxConstraints constraints)
    {
        var width = (constraints.MaxWidth - gap * (columns - 1)) / columns;
        var heights = new float[columns];
        for (var i = 0; HasChild(i); i++)
        {
            var into = Shortest(heights);
            var child = LayoutChild(i, new BoxConstraints(width, width, 0, float.PositiveInfinity));
            PositionChild(i, new Point(into * (width + gap), heights[into]));
            heights[into] += child.Height + gap;
        }
        return new Size(constraints.MaxWidth, MathF.Max(0, heights.Max() - gap));
    }

    /// <summary>The column the next child goes under: the shortest, the first of those that tie.</summary>
    private static int Shortest(float[] heights)
    {
        var at = 0;
        for (var i = 1; i < heights.Length; i++)
            if (heights[i] < heights[at]) at = i;
        return at;
    }
}
