using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The numbers of the control ladder a sized control reads, each resolved in the order of
/// <c>docs/ELEMENT-TREE-PLAN.md</c> §3.10, the first that answers winning: the control's family slot (a
/// Button's <see cref="ButtonTheme.Controls"/>), then the theme's <see cref="IAppTheme.Controls"/>, then
/// <see cref="Sizing"/>, the component's own default. A value that varies by density is read at
/// <c>context.Density</c> at every step, so a slot never bypasses the compact layout the fallback gives.
/// <para>
/// The instance's own argument, the first step of the order, is the caller's: none of the controls that
/// read this has one for these numbers. The theme is the one <c>context.Theme</c> answers; a theme a
/// subtree declares joins with the positional theme (#1387).
/// </para>
/// </summary>
internal static class ControlMetrics
{
    public static float Height(ComponentContext context, SizeVariant size, ControlsTheme? family = null) =>
        RungAt(family?.Height, size, context.Density)
        ?? RungAt(context.Theme.Controls?.Height, size, context.Density)
        ?? Sizing.Height(size, context.Density);

    public static float PaddingX(ComponentContext context, SizeVariant size, ControlsTheme? family = null) =>
        RungAt(family?.PaddingX, size, context.Density)
        ?? RungAt(context.Theme.Controls?.PaddingX, size, context.Density)
        ?? Sizing.PaddingX(size, context.Density);

    public static float LabelSize(ComponentContext context, SizeVariant size, ControlsTheme? family = null) =>
        RungAt(family?.LabelSize, size, context.Density)
        ?? RungAt(context.Theme.Controls?.LabelSize, size, context.Density)
        ?? Sizing.LabelSize(size, context.Density);

    public static float Gap(ComponentContext context, SizeVariant size, ControlsTheme? family = null) =>
        Rung(family?.Gap, size) ?? Rung(context.Theme.Controls?.Gap, size) ?? Sizing.Gap(size);

    public static float Icon(ComponentContext context, SizeVariant size, ControlsTheme? family = null) =>
        Rung(family?.Icon, size) ?? Rung(context.Theme.Controls?.Icon, size) ?? Sizing.Icon(size);

    /// <summary>A control's corner radius, falling back to <paramref name="byDefault"/> where the control
    /// shapes itself from the theme's shape scale rather than from <see cref="Sizing.Radius"/>.</summary>
    public static float Radius(ComponentContext context, SizeVariant size, ControlsTheme? family = null,
        float? byDefault = null) =>
        Rung(family?.Radius, size) ?? Rung(context.Theme.Controls?.Radius, size) ?? byDefault ?? Sizing.Radius(size);

    /// <summary>The rung of <paramref name="ladder"/> for <paramref name="size"/>, or null for none.</summary>
    private static float? Rung(SizeLadder? ladder, SizeVariant size) => size switch
    {
        SizeVariant.Small => ladder?.Small,
        SizeVariant.Medium => ladder?.Medium,
        SizeVariant.Large => ladder?.Large,
        _ => ladder?.XLarge,
    };

    /// <summary>The rung for <paramref name="size"/> on <paramref name="density"/>'s side of the ladder.</summary>
    private static float? RungAt(DensityLadder? ladder, SizeVariant size, Density density) =>
        Rung(density == Density.Compact ? ladder?.Compact : ladder?.Comfortable, size);

    /// <summary>The rung of a family's own ladder for <paramref name="size"/>, or null for none: a value
    /// a control varies by size on a ladder of its own rather than the theme's (an icon button's glyph).</summary>
    public static float? AtSize(SizeLadder? ladder, SizeVariant size) => Rung(ladder, size);
}
