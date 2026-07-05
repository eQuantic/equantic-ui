using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>Divider inset (spec A7): full-bleed, leading 16 (lists), middle 16 (card interiors).</summary>
public enum DividerInset : byte
{
    None = 0,
    Leading = 1,
    Middle = 2,
}

public enum DividerAxis : byte
{
    Horizontal = 0,
    Vertical = 1,
}

/// <summary>
/// The design system's Divider (spec A7): a 1dp hairline in the Border token that consumes exactly
/// its thickness — vertical rhythm comes from the parent's gap, never from Divider spacing props
/// (it has none). Insets realize as padding on the wrapper so the LINE is inset, not the slot.
/// </summary>
public sealed class Divider : StatelessComponent
{
    public Divider(DividerInset inset = DividerInset.None, DividerAxis axis = DividerAxis.Horizontal)
    {
        Inset = inset;
        Axis = axis;
    }

    public DividerInset Inset { get; init; }
    public DividerAxis Axis { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var line = new Box(new BoxStyle
        {
            Width = Axis == DividerAxis.Vertical ? SizeValue.Fixed(1) : SizeValue.Fill,
            Height = Axis == DividerAxis.Vertical ? SizeValue.Fill : SizeValue.Fixed(1),
            Background = context.Theme.Border,
        });

        if (Inset == DividerInset.None) return line;

        var padding = Inset == DividerInset.Leading
            ? new EdgeInsets(Space.S4, 0, 0, 0)
            : EdgeInsets.Symmetric(Space.S4, 0);
        return new Box(new BoxStyle { Width = SizeValue.Fill, Padding = padding }, line);
    }
}
