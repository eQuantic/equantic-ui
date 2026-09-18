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

    /// <summary>
    /// Whether this child is being CUT TO FIT rather than measured for its own sake — the truncation
    /// contract (spec A2) asking a text to yield an ellipsis instead of wrapping.
    ///
    /// <para>
    /// It is here, and not a parameter on the text measurement, because the item the contract cuts
    /// is not always the text: <c>Pressable(Text(...))</c> is a text as far as the row is concerned,
    /// and the cut has to travel through the wrapper to reach it. That is the same journey
    /// <see cref="AxisConstraint.Stretch"/> makes, for the same reason — a fact about what the
    /// PARENT decided, which only the node at the bottom can act on.
    /// </para>
    ///
    /// <para>
    /// It travels exactly as far as a layout-transparent wrapper does: every other door measures its
    /// children through <see cref="ForChild"/>, which clears it, so a Row nested inside a cut
    /// wrapper is measured normally and its own texts wrap as they always did.
    /// </para>
    /// </summary>
    public bool Truncating { get; init; }

    public float MaxWidth => Width.Max;
    public float MaxHeight => Height.Max;

    public LayoutConstraints WithMax(float maxWidth, float maxHeight) =>
        this with { Width = Width.WithMax(maxWidth), Height = Height.WithMax(maxHeight) };

    public LayoutConstraints WithMaxWidth(float maxWidth) => this with { Width = Width.WithMax(maxWidth) };

    public LayoutConstraints WithMaxHeight(float maxHeight) => this with { Height = Height.WithMax(maxHeight) };

    /// <summary>
    /// What a CHILD is measured under: this room, and none of the stretch. A parent that means to
    /// stretch a child says so with <see cref="AxisConstraint.Stretched"/> on top; the default is
    /// that it does not, because stretch belongs to the node it was set for and to nothing under
    /// it. One door, so "released for the children" cannot be forgotten at the fourteenth call
    /// site — which is what it WAS, when the release happened by clearing two fields on a shared
    /// context and trusting every reader to have already looked.
    /// </summary>
    public LayoutConstraints ForChild(float maxWidth, float maxHeight) =>
        new(Width.WithMax(maxWidth).Released(), Height.WithMax(maxHeight).Released())
        { Truncating = false };

    /// <summary>Being cut to fit. See <see cref="Truncating"/>.</summary>
    public LayoutConstraints Truncated() => this with { Truncating = true };

    // The rest RESTATE one axis and keep everything else, `Truncating` included: a transparent
    // wrapper is precisely what a cut travels through, and FOUR of them — Pressable, Adjustable,
    // Link, Progress — reach their child through Inline(). They are the four that put an ELEMENT
    // between parent and child and give it a width; the rest either emit no host at all, so the
    // child's own element takes the stretch, or own their geometry on purpose.
    /// <inheritdoc cref="AxisConstraint.Released"/>
    public LayoutConstraints Released() => this with { Width = Width.Released(), Height = Height.Released() };

    /// <inheritdoc cref="AxisConstraint.Inline"/>
    public LayoutConstraints Inline() => this with { Width = Width.Inline(), Height = Height.Inline() };

    /// <inheritdoc cref="AxisConstraint.Stretched"/>
    public LayoutConstraints Stretched(StretchKind width, StretchKind height) =>
        this with { Width = Width.Stretched(width), Height = Height.Stretched(height) };

    /// <inheritdoc cref="AxisConstraint.DecidedByContent"/>
    public LayoutConstraints DecidedByContent(bool width, bool height) =>
        this with { Width = Width.DecidedByContent(width), Height = Height.DecidedByContent(height) };
}
