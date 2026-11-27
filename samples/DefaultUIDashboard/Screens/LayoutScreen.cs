using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// A child built for the room its box has (#1555). Each card holds a <c>LayoutBuilder</c> that
/// writes the room it was told and lays its two parts side by side from 480 points, stacking them below.
/// The two fixed cards are told the same room on every target, which the e2e suite reads in a browser and
/// the Photon suite lays out (<c>LayoutBuilderTests</c>); the last card fills the width the window leaves
/// it, so narrowing the window builds it again.
/// <para>
/// Below them, children laid out by delegates written in C# (#1556): a masonry grid whose height follows its tiles,
/// and a radial menu. Both run in Photon's layout pass and, transpiled, right after the browser's.
/// </para>
/// </summary>
[Page("/layout", Title = "Layout — eQuantic Console")]
public sealed class LayoutScreen : StatelessComponent
{
    public override VisualNode Build(ComponentContext context) => Column(gap: Space.S6, children: [
        Text("A child built for the room its box has", TypeRole.Heading),
        Card("fixed", new BoxStyle { Width = 600, Height = 200 }),
        Card("narrow", new BoxStyle { Width = 360, Height = 200 }),
        Card("fill", new BoxStyle { Width = SizeValue.Fill }),
        Text("Children laid out by a delegate written in C#", TypeRole.Heading),
        Box(new BoxStyle { Width = 600 }, CustomMultiChildLayout(new MasonryLayout(columns: 3, gap: 12),
            children: [.. TileHeights.Select((height, i) => LayoutId(i, Tile(context, $"tile {i + 1}", height)))])),
        Box(new BoxStyle { Width = 240, Height = 240 }, CustomMultiChildLayout(new RadialLayout(radius: 88),
            children: [.. Actions.Select((action, i) => LayoutId(i, Tile(context, action, 48)))])),
    ]);

    /// <summary>The masonry's tiles, each a height of its own.</summary>
    public static readonly float[] TileHeights = [120, 64, 96, 140, 72, 88, 110];

    /// <summary>The radial menu's actions, clockwise from the top.</summary>
    public static readonly string[] Actions = ["copy", "move", "share", "pin", "tag", "drop"];

    /// <summary>A tile of <paramref name="height"/> that names itself.</summary>
    public static VisualNode Tile(ComponentContext context, string name, float height) =>
        Box(new BoxStyle
        {
            Height = height,
            MinWidth = 48,
            Padding = EdgeInsets.All(Space.S2),
            Background = context.Theme.SurfaceSubtle,
            CornerRadius = new CornerRadii(context.Theme.Shape(ShapeScale.Medium)),
        }, Text(name, TypeRole.Label));

    /// <summary>A card of <paramref name="style"/> whose content is built for the room it has.</summary>
    public static VisualNode Card(string name, BoxStyle style) => Box(style, LayoutBuilder(room =>
        Column(gap: Space.S2, children: [
            Text($"{name}: {Extent(room.MaxWidth)} × {Extent(room.MaxHeight)}", TypeRole.Label),
            room.MaxWidth >= 480
                ? Row(gap: Space.S4, children: [Text("side by side", TypeRole.BodyM), Text("amount", TypeRole.BodyM)])
                : Column(gap: Space.S2, children: [Text("stacked", TypeRole.BodyM), Text("amount", TypeRole.BodyM)]),
        ])));

    /// <summary>A maximum as a reader says it: its points, or unbounded.</summary>
    public static string Extent(float value) => float.IsPositiveInfinity(value) ? "unbounded" : $"{value}";
}
