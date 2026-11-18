using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// A child built for the room its box has (#1555). Each card holds a <see cref="Primitives.LayoutBuilder"/> that
/// writes the room it was told and lays its two parts side by side from 480 points, stacking them below.
/// The two fixed cards are told the same room on every target, which the e2e suite reads in a browser and
/// the Photon suite lays out (<c>LayoutBuilderTests</c>); the last card fills the width the window leaves
/// it, so narrowing the window builds it again.
/// </summary>
[Page("/layout", Title = "Layout — eQuantic Console")]
public sealed class LayoutScreen : StatelessComponent
{
    public override VisualNode Build(ComponentContext context) => Column(gap: Space.S6, children: [
        Text("A child built for the room its box has", TypeRole.Heading),
        Card("fixed", new BoxStyle { Width = 600, Height = 200 }),
        Card("narrow", new BoxStyle { Width = 360, Height = 200 }),
        Card("fill", new BoxStyle { Width = SizeValue.Fill }),
    ]);

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
