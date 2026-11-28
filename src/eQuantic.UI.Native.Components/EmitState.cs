using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Everything the paint walk carries. ALL of it, which is the point: <see cref="EmitVisitor"/> holds
/// no state at all, so one instance serves every frame and every layer and the walk allocates
/// nothing to dispatch.
///
/// <para>
/// The first arrangement gave the visitor six fields and this struct three. It cost ONE object per
/// frame — 64 bytes — and <c>PerfHarnessTests</c> refused it at 75,778 against a 75,776 ceiling, by
/// two bytes. The budget was right to refuse: a visitor with fields is a visitor with identity, and
/// this one has none. Moving the six here made it a singleton and the cost zero rather than small.
/// </para>
///
/// <para>
/// A struct, so a node's visit costs a copy on the stack and nothing on the heap. It is passed by
/// value because <c>IVisualNodeVisitor</c> takes its state that way; a door that changes something
/// for its children spells the change (<c>s with { Node = child, Input = scrolled }</c>) instead of
/// mutating what its siblings will read.
/// </para>
///
/// <para>
/// <see cref="Press"/> is the exception that proves it: a mutable scope, ONE PER LAYER, pushed and
/// restored down a subtree. <see cref="PhotonRealizer"/> builds a fresh one for the page and for
/// each overlay, so a leftover cannot cross between them.
/// </para>
/// </summary>
internal readonly record struct EmitState(
    LayoutNode Node,
    InputSink Input,
    PressScope Press,
    IAppTheme Theme,
    ThemeMode Mode,
    ContrastLevel Contrast,
    DisplayListBuilder Builder,
    MotionScope Motion,
    List<QueuedLayer> Overlays)
{
    /// <summary>The foreground of the nearest box above that states one, resolved for its state: what
    /// a text, an icon or a vector with no colour of its own is drawn in (#614), Flutter's
    /// <c>DefaultTextStyle</c> and <c>IconTheme</c>. Null = none stated, the theme's TextPrimary.</summary>
    public ColorToken? Foreground { get; init; }

    /// <summary>The look the boxes above state, each laid over the one around it
    /// (<see cref="BoxStyle.Content"/>, <see cref="ContentStyle.Merge"/>, #1577): the role, the type style,
    /// the line limit and the alignment of a text that names none, and the size of an icon that names
    /// none, beside <see cref="Foreground"/>'s colour. The measure pass carried the same look down the same
    /// tree, so a node is painted in the look it was measured in. Null = none stated.</summary>
    public ContentStyle? Content { get; init; }

    /// <summary>
    /// Whether a transform is drawn around this node (#1937): a box's <c>Transform</c>, or a motion that
    /// moves its subtree by one (a loop, an entrance's rise, a drag, a carried item's copy). Under one, a
    /// window-attached pattern is placed in its own box, as CSS Transforms 1 §6 paints a fixed background
    /// under a transform as a scrolling one. A scroll is no transform here, as in a browser: layout moves
    /// the scrolled boxes, and the window stays where it is.
    /// </summary>
    public bool UnderTransform { get; init; }

    /// <summary>What a node that names <paramref name="own"/> (or no colour) is drawn in.</summary>
    public ColorToken Ink(ColorToken? own) => own ?? Foreground ?? Theme.TextPrimary;
}
