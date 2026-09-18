namespace eQuantic.UI.Primitives;

/// <summary>
/// THE ONE STATEMENT of what layout-transparent means: a wrapper is transparent unless it carries
/// geometry of its own, and a parent must treat a transparent one exactly as it treats what it
/// wraps.
///
/// <para>
/// It is one switch because it was four, and the four disagreed. Three questions in the layout
/// engine — a flex item's min-content floor, its cross-axis size kind, and whether it may shrink —
/// each kept its own hand-written list of wrappers to look through, and a fourth reader, the
/// truncation contract, looked through none of them. The lists differed in EIGHT of the twenty
/// wrappers, and nobody had decided any of the eight: they were simply not named.
/// </para>
///
/// <para>
/// The DEFAULT IS TRANSPARENT, and that is the half of the design that stops this recurring. A
/// twenty-first wrapper added to the vocabulary is transparent without joining anything, because
/// carrying no geometry is what a wrapper normally does; only a node that establishes something of
/// its own has to say so here, where the reason is written next to it. The hand-kept lists failed
/// the other way round — a wrapper left out of one of them did not fail, it quietly answered as if
/// it were opaque.
/// </para>
/// </summary>
/// <remarks>
/// HOST ONLY, for the reason <see cref="SingleChildNode"/> carries the same attribute: every public
/// type in this namespace silently promises a runtime export of the same name, and this one has
/// none. It is a question a realizer asks about a node, never something a page names.
/// </remarks>
[ServerOnly]
public static class LayoutTransparency
{
    /// <summary>
    /// Whether <paramref name="wrapper"/> carries NO geometry of its own — so a parent sizing it,
    /// flooring it or shrinking it must ask the same question of <see cref="SingleChildNode.Child"/>
    /// and use that answer unchanged.
    ///
    /// <para>
    /// THE THREE THAT ARE NOT, each because it establishes something the child cannot speak for:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <see cref="ScrollView"/> — its own viewport. A scroller's floor is not its content's: it
    /// scrolls instead of growing, which is the whole point of it.
    /// </description></item>
    /// <item><description>
    /// <see cref="Overlay"/> — a viewport layer. It takes no space in the flow at all, so a row
    /// that sized itself from an overlay's content would reserve space for something drawn over it.
    /// </description></item>
    /// <item><description>
    /// <see cref="Positioned"/> — a contract with a <see cref="Stack"/>, the way a flex weight is
    /// one with a row. Read anywhere else it means nothing, and taking its child's size would make
    /// a corner button size the stack.
    /// </description></item>
    /// </list>
    /// </summary>
    public static bool IsLayoutTransparent(this SingleChildNode wrapper) => wrapper switch
    {
        ScrollView or Overlay or Positioned => false,
        _ => true,
    };
}
