namespace eQuantic.UI.Primitives;

/// <summary>
/// The Button's METRICS (spec A12): the size table every realizer measures against.
/// <para>
/// This used to carry a second, parallel appearance path too — an <c>InteractionState</c> enum, a
/// resolved <c>ButtonStyle</c> struct and a <c>ButtonRenderer</c> that drew it. Nothing rendered
/// through it: the real Button builds a tree of Box and Text like every other component, and the
/// realizers draw that. Its golden images pinned a picture no app ever showed, which is worse than
/// having none — a suite that stays green while the thing it claims to guard drifts.
/// </para>
/// </summary>
public static class ButtonStyles
{
    /// <summary>Buttons hug their label but never shrink below this (spec A12).</summary>
    public const float MinWidth = 64;

    /// <summary>
    /// The size table (spec A12), read from the shared <see cref="Sizing"/> ladder — a Button of a
    /// given size must measure exactly like any other control of that size, so the numbers live in
    /// the token layer and this is the tuple view of them.
    /// </summary>
    public static (float Height, float PadX, float Gap, float LabelSize, float IconSize, float Radius, float Hit) Metrics(
        SizeVariant size, Density density = Density.Comfortable) =>
        (Sizing.Height(size, density), Sizing.PaddingX(size, density), Sizing.Gap(size),
         Sizing.LabelSize(size, density), Sizing.Icon(size), Sizing.Radius(size),
         Sizing.HitTarget(size, density));
}
