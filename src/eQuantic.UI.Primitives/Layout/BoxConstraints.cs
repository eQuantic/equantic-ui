namespace eQuantic.UI.Primitives;

/// <summary>
/// The room a box may take: a minimum and a maximum width, and a minimum and a maximum height, in
/// device-independent points. A maximum that is <see cref="float.PositiveInfinity"/> is unbounded, the
/// room an axis has when its size is left to what it holds. What a <see cref="LayoutBuilder"/> is
/// handed, and what an author holds, computes with and applies (<c>ConstrainedBox</c>).
/// <para>
/// Flutter's <c>BoxConstraints</c>, by name and by shape, which is the question this vocabulary asks
/// first. The layout engine measures with a record of its own, <c>LayoutConstraints</c>, whose flags
/// say which parent decided a size and whether a stretch survives an inline boundary: that is the
/// engine's policy, and the web, whose layout is the browser's, could answer none of it. This value
/// is what both targets CAN answer, and the engine's record is projected onto it in one place.
/// </para>
/// <para>
/// A class rather than a struct, because Flutter's default is UNBOUNDED, and a struct's default is
/// the zero instance: <c>default(BoxConstraints)</c> would have been a box tight at 0 by 0, the one
/// value nobody means by "no constraints".
/// </para>
/// </summary>
public sealed record BoxConstraints
{
    /// <summary>No limit on either axis.</summary>
    public static readonly BoxConstraints Unbounded = new();

    /// <summary>
    /// A box between the given bounds. Every argument defaults to what leaves its axis free: no
    /// minimum and an unbounded maximum.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A minimum is negative, infinite or not a number,
    /// a maximum is not a number, or a minimum is above its maximum.</exception>
    public BoxConstraints(float minWidth = 0, float maxWidth = float.PositiveInfinity,
        float minHeight = 0, float maxHeight = float.PositiveInfinity)
    {
        Validate(minWidth, maxWidth, nameof(minWidth), nameof(maxWidth));
        Validate(minHeight, maxHeight, nameof(minHeight), nameof(maxHeight));
        MinWidth = minWidth;
        MaxWidth = maxWidth;
        MinHeight = minHeight;
        MaxHeight = maxHeight;
    }

    /// <summary>The narrowest the box may be.</summary>
    public float MinWidth { get; }

    /// <summary>The widest the box may be; <see cref="float.PositiveInfinity"/> when unbounded.</summary>
    public float MaxWidth { get; }

    /// <summary>The shortest the box may be.</summary>
    public float MinHeight { get; }

    /// <summary>The tallest the box may be; <see cref="float.PositiveInfinity"/> when unbounded.</summary>
    public float MaxHeight { get; }

    /// <summary>Exactly <paramref name="size"/>: the minimum and the maximum are the same on each axis.</summary>
    public static BoxConstraints Tight(Size size) => new(size.Width, size.Width, size.Height, size.Height);

    /// <summary>Anything up to <paramref name="size"/>, from nothing.</summary>
    public static BoxConstraints Loose(Size size) => new(0, size.Width, 0, size.Height);

    /// <summary>Whether exactly one size satisfies these constraints.</summary>
    public bool IsTight => MinWidth >= MaxWidth && MinHeight >= MaxHeight;

    /// <summary>Whether the width has a limit.</summary>
    public bool HasBoundedWidth => MaxWidth < float.PositiveInfinity;

    /// <summary>Whether the height has a limit.</summary>
    public bool HasBoundedHeight => MaxHeight < float.PositiveInfinity;

    /// <summary>The largest size these constraints allow; an unbounded axis answers
    /// <see cref="float.PositiveInfinity"/>.</summary>
    public Size Biggest => new(MaxWidth, MaxHeight);

    /// <summary>The smallest size these constraints allow.</summary>
    public Size Smallest => new(MinWidth, MinHeight);

    /// <summary>The size nearest to <paramref name="size"/> that these constraints allow.</summary>
    public Size Constrain(Size size) =>
        new(Clamp(size.Width, MinWidth, MaxWidth), Clamp(size.Height, MinHeight, MaxHeight));

    /// <summary>
    /// These constraints, kept inside <paramref name="parent"/>'s: each bound moved into the parent's
    /// range on its axis, so what the parent allows always wins.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="parent"/> is null.</exception>
    public BoxConstraints Enforce(BoxConstraints parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        return new BoxConstraints(
            Clamp(MinWidth, parent.MinWidth, parent.MaxWidth),
            Clamp(MaxWidth, parent.MinWidth, parent.MaxWidth),
            Clamp(MinHeight, parent.MinHeight, parent.MaxHeight),
            Clamp(MaxHeight, parent.MinHeight, parent.MaxHeight));
    }

    /// <summary>The same maximums, with no minimum.</summary>
    public BoxConstraints Loosen() => new(0, MaxWidth, 0, MaxHeight);

    /// <summary>
    /// The room left inside <paramref name="insets"/>: each bound less the insets on its axis, never
    /// below zero, and a maximum never below its minimum.
    /// </summary>
    public BoxConstraints Deflate(EdgeInsets insets)
    {
        var minWidth = MathF.Max(0, MinWidth - insets.Horizontal);
        var minHeight = MathF.Max(0, MinHeight - insets.Vertical);
        return new BoxConstraints(
            minWidth, MathF.Max(minWidth, MaxWidth - insets.Horizontal),
            minHeight, MathF.Max(minHeight, MaxHeight - insets.Vertical));
    }

    // Not Math.Clamp, which throws when the range is empty: the ranges here come from constraints
    // that are valid by construction, and the clamp is read as Flutter's `num.clamp`.
    private static float Clamp(float value, float min, float max) => MathF.Min(MathF.Max(value, min), max);

    private static void Validate(float min, float max, string minName, string maxName)
    {
        if (float.IsNaN(min) || float.IsInfinity(min) || min < 0)
            throw new ArgumentOutOfRangeException(minName, min, "A minimum is a finite number of points, zero or more.");
        if (float.IsNaN(max) || max < min)
            throw new ArgumentOutOfRangeException(maxName, max, "A maximum is a number of points no smaller than its minimum.");
    }
}
