namespace eQuantic.UI.Primitives;

/// <summary>
/// Repeating artwork a box paints behind its content (#1937): one layer of
/// <see cref="BoxStyle.Patterns"/>, above the background colour and below the glow and the gradient.
/// Flutter's <c>DecorationImage</c> on a <c>BoxDecoration</c>, with everything the CSS backgrounds
/// model adds to it: a list, repetition per axis, a placing area, a clip area and an attachment.
/// <para>
/// A CLOSED set: <see cref="GridPattern"/>, <see cref="DrawingPattern"/> and
/// <see cref="ImagePattern"/>, and nothing else. A realizer draws them through
/// <see cref="IPatternVisitor{TState, TResult}"/>, so a kind added here is a build error in every
/// realizer that does not draw it. The set is closed by the <c>private protected</c> members below,
/// which no type outside this assembly can reach: a record's own copy constructor must stay
/// <c>protected</c>, so the constructor alone would not close it.
/// </para>
/// </summary>
public abstract record Pattern
{
    private protected Pattern() { }

    /// <summary>
    /// The area the pattern is placed in: where its origin, its fractions and its fit are measured
    /// from. The padding area by default, as CSS places a background.
    /// </summary>
    public BoxArea Area { get; init; } = BoxArea.Padding;

    /// <summary>
    /// The area the pattern is clipped to, its rounded corners included. The whole box by default, so
    /// the pattern runs under the border, as CSS clips a background.
    /// </summary>
    public BoxArea ClipTo { get; init; } = BoxArea.Border;

    /// <summary>What the pattern is placed against: its box (the default), or the window.</summary>
    public TileAttachment Attachment { get; init; }

    /// <summary>Hands this pattern to the visitor's method for its kind.</summary>
    public TResult Accept<TState, TResult>(IPatternVisitor<TState, TResult> visitor, TState state) =>
        Dispatch(visitor, state);

    /// <summary>The kind's own call of <see cref="Accept"/>: private protected, so a kind can only be
    /// declared in this assembly.</summary>
    private protected abstract TResult Dispatch<TState, TResult>(IPatternVisitor<TState, TResult> visitor, TState state);
}
