namespace eQuantic.UI.Native.Framework;

/// <summary>
/// What a parent offers a child on ONE axis: how much room there is, whether the parent's own size
/// on that axis is still being decided by its content, and whether the parent has already decided
/// the child's size for it.
///
/// <para>
/// Those three facts were always here and they travelled apart — <c>maxW</c> as a parameter,
/// <c>Indeterminate*</c> as mutable state on the shared <see cref="LayoutContext"/> with a
/// save/restore around every child measurement, and <c>Stretch*</c> as mutable state read and
/// cleared on the way in and then passed on as a parameter. Three shapes for one fact, two of them
/// living on an object every node in the tree can see, kept correct by remembering to restore them.
/// An invariant maintained by remembering is one that breaks at the fourteenth call site, which is
/// the same argument that made the layout tree's parent link a single door.
/// </para>
///
/// <para>
/// WHY NOT FLUTTER'S SHAPE. <c>BoxConstraints</c> needs no flags: unbounded IS
/// <c>maxWidth == infinity</c> and stretched IS <c>minWidth == maxWidth</c>. That is the tidier
/// model and it was the first thing tried here. It does not fit, and the reason is worth writing
/// down rather than rediscovering: <see cref="StretchKind"/> distinguishes FLEX stretch from BLOCK
/// stretch, and the difference is whether the stretch survives an inline boundary — a
/// <c>Pressable</c>, an <c>Adjustable</c>, a <c>Link</c>. Flutter has no inline boundary to survive,
/// so min-equals-max carries everything it needs. We have one because the web realizer answers to
/// CSS's inline/block model, and a component has to lay out the same on both targets. Collapsing to
/// min/max would lose exactly that, silently.
/// </para>
/// </summary>
/// <param name="Max">Room available, in dp. <see cref="float.PositiveInfinity"/> when unbounded.</param>
/// <param name="Indeterminate">
/// Whether the PARENT's size on this axis is being decided by its content — a Hug. Fill has nothing
/// to fill against there: a child taking the maximum would decide the size of the very thing that
/// was supposed to measure it, which is how a 16dp badge once came out as wide as the window. On an
/// indeterminate axis Fill resolves to the intrinsic size, the rule CSS shrink-to-fit and Flutter's
/// min-size rows both follow.
/// </param>
/// <param name="Stretch">
/// The MIRROR of <paramref name="Indeterminate"/>: an axis the parent has already decided FOR this
/// child, because it aligns stretch. There a Hug is not a hug — a stretched flex item with an auto
/// cross size IS the line's size, which is what CSS does and what gives <c>Main = Center</c> inside
/// it any room to centre in.
/// </param>
public readonly record struct AxisConstraint(float Max, bool Indeterminate, StretchKind Stretch)
{
    /// <summary>No limit on this axis — the child may be as large as it likes.</summary>
    public static readonly AxisConstraint Unbounded =
        new(float.PositiveInfinity, false, StretchKind.None);

    /// <summary>Room, with nothing else said about it.</summary>
    public static AxisConstraint Of(float max) => new(max, false, StretchKind.None);

    public bool IsUnbounded => float.IsPositiveInfinity(Max);

    /// <summary>Whether the parent decided this child's size on this axis.</summary>
    public bool IsStretched => Stretch != StretchKind.None;

    /// <summary>The same constraint with different room.</summary>
    public AxisConstraint WithMax(float max) => this with { Max = max };

    /// <summary>
    /// What a child inherits: the room, and nothing about who decided it. Stretch belongs to the
    /// node it was set for and to nothing under it — a stretched row does not go on stretching every
    /// box inside it — and this is where that used to be a read-and-clear on shared state.
    /// </summary>
    public AxisConstraint Released() => this with { Stretch = StretchKind.None };

    /// <summary>
    /// Crossing an INLINE boundary: block stretch stops here, flex stretch passes through. The one
    /// place the two kinds of stretch behave differently, and the reason they are two.
    /// </summary>
    public AxisConstraint Inline() =>
        Stretch == StretchKind.Block ? this with { Stretch = StretchKind.None } : this;

    /// <summary>Whether the size on this axis is being decided by what goes in it.</summary>
    public AxisConstraint DecidedByContent(bool indeterminate) =>
        Indeterminate == indeterminate ? this : this with { Indeterminate = indeterminate };

    /// <summary>The parent decided this child's size, and how.</summary>
    public AxisConstraint Stretched(StretchKind kind) =>
        Stretch == kind ? this : this with { Stretch = kind };
}

/// <summary>
/// Both axes of what a parent offers a child. The value the measurement pass hands down, in place
/// of two floats plus four fields on a shared context.
/// </summary>
public readonly record struct LayoutConstraints(AxisConstraint Width, AxisConstraint Height)
{
    /// <summary>Room on both axes, with nothing else said.</summary>
    public static LayoutConstraints Of(float maxWidth, float maxHeight) =>
        new(AxisConstraint.Of(maxWidth), AxisConstraint.Of(maxHeight));

    /// <summary>Neither axis limited.</summary>
    public static readonly LayoutConstraints Unbounded =
        new(AxisConstraint.Unbounded, AxisConstraint.Unbounded);

    public float MaxWidth => Width.Max;
    public float MaxHeight => Height.Max;

    public LayoutConstraints WithMax(float maxWidth, float maxHeight) =>
        new(Width.WithMax(maxWidth), Height.WithMax(maxHeight));

    public LayoutConstraints WithMaxWidth(float maxWidth) => this with { Width = Width.WithMax(maxWidth) };

    public LayoutConstraints WithMaxHeight(float maxHeight) => this with { Height = Height.WithMax(maxHeight) };

    /// <inheritdoc cref="AxisConstraint.Released"/>
    public LayoutConstraints Released() => new(Width.Released(), Height.Released());

    /// <inheritdoc cref="AxisConstraint.Inline"/>
    public LayoutConstraints Inline() => new(Width.Inline(), Height.Inline());

    /// <inheritdoc cref="AxisConstraint.DecidedByContent"/>
    public LayoutConstraints DecidedByContent(bool width, bool height) =>
        new(Width.DecidedByContent(width), Height.DecidedByContent(height));
}
