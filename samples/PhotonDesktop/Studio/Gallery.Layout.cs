using eQuantic.UI.Components;
using eQuantic.UI.Primitives;

namespace eQuantic.Studio;

/// <summary>
/// Children laid out by delegates written in C# (#1556): a masonry grid whose height follows its tiles and a radial
/// menu, the same two the web dashboard's <c>/layout</c> page lays out, run here in Photon's layout pass.
/// </summary>
public static partial class Gallery
{
    private static readonly float[] TileHeights = [120, 64, 96, 140, 72, 88, 110];
    private static readonly string[] RadialActions = ["copy", "move", "share", "pin", "tag", "drop"];

    private static VisualNode CustomLayouts(IAppTheme theme)
    {
        var column = Column(Space.S3);
        column.Add(Caption(theme,
            "A masonry grid puts each tile under the shortest column and is as tall as the tallest one; "
            + "a radial menu sets its actions on a circle. Each is one C# class, a MultiChildLayoutDelegate."));
        column.Add(Box(new BoxStyle { Width = 600 }, CustomMultiChildLayout(new MasonryLayout(columns: 3, gap: 12),
            children: [.. TileHeights.Select((height, i) => LayoutId(i, Tile(theme, $"tile {i + 1}", height)))])));
        column.Add(Box(new BoxStyle { Width = 240, Height = 240 }, CustomMultiChildLayout(new RadialLayout(radius: 88),
            children: [.. RadialActions.Select((action, i) => LayoutId(i, Tile(theme, action, 48)))])));
        return Section(theme, "Custom layout", column);
    }

    private static VisualNode Tile(IAppTheme theme, string name, float height) =>
        Box(new BoxStyle
        {
            Height = height,
            MinWidth = 48,
            Padding = EdgeInsets.All(Space.S2),
            Background = theme.SurfaceSubtle,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
        }, Text(name, TypeRole.Label));
}
