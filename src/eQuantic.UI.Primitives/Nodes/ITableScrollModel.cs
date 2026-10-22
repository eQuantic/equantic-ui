namespace eQuantic.UI.Primitives;

/// <summary>
/// The scroll behind a <see cref="TableViewport"/>, seen from a realizer: the whole of what a host may
/// TELL it, and the little it reads back. A realizer hands over what the platform reported (the window
/// it laid the viewport out in, a wheel, a pointer, the time) and positions the cells at the
/// <see cref="Offset"/> it answers; every decision about how far a movement goes, where it stops, how a
/// fling decays and what a scrollbar press means is made behind this door, once, for every target.
/// <para>
/// <em>How does Flutter solve it?</em> With a <c>ScrollPosition</c> per axis that the viewport reads
/// and the gestures drive. This door is the two of them together, as <see cref="ISheetSurfaceModel"/> is
/// a sheet's protocol: the vocabulary declares it, a component owns the implementation and keeps it
/// across rebuilds, and the node carries it.
/// </para>
/// </summary>
public interface ITableScrollModel
{
    /// <summary>How far the grid has scrolled under the window, across and down.</summary>
    TableOffset Offset { get; }

    /// <summary>The part of the window the cells scroll in, as the latest <see cref="Arrange"/> answered:
    /// the window less the scrollbars' strips.</summary>
    Size Viewport { get; }

    /// <summary>
    /// What the latest layout found: the <paramref name="window"/> the viewport was given, how far its
    /// grid reaches across and down, how much of the window its pinned columns and rows hold still, and
    /// whether the platform asked for reduced motion. Answers the part of the window the cells scroll
    /// in, which the scrollbars' strips are taken from, and clamps the offset into the new extent.
    /// </summary>
    Size Arrange(Size window, double gridAcross, double gridDown, double pinnedAcross, double pinnedDown,
        bool reduceMotion);

    /// <summary>
    /// A wheel or a trackpad moved by <paramref name="across"/> and <paramref name="down"/> points, a
    /// <paramref name="stepped"/> movement being a mouse wheel's notch, which glides rather than jumps.
    /// Shift turns a movement that is only vertical into a horizontal one. Answers what the viewport
    /// could not take, for the scroller around it.
    /// </summary>
    TableOffset Wheel(double across, double down, bool stepped, KeyModifiers modifiers);

    /// <summary>
    /// A pointer over the viewport, at <paramref name="position"/> in its own coordinates, at
    /// <paramref name="time"/> in milliseconds. A press on a scrollbar drags its thumb or pages from its
    /// track; a finger or a stylus pans the cells and flings them when it lifts moving. Answers whether
    /// the scroll took the press: a mouse press on the cells is theirs.
    /// </summary>
    bool Pointer(PointerPhase phase, Point position, PointerKind kind, double time);

    /// <summary>Moves a glide or a fling on to <paramref name="time"/>, in milliseconds. Answers whether it
    /// is still moving, which is the host's cue to ask for another frame.</summary>
    bool Advance(double time);

    /// <summary>The track of the scrollbar on an <paramref name="axis"/>, in the viewport's own
    /// coordinates, or nothing when the grid does not overflow it.</summary>
    Rect? TrackOf(ScrollAxis axis);

    /// <summary>The thumb on that track, or nothing.</summary>
    Rect? ThumbOf(ScrollAxis axis);

    /// <summary>
    /// The <paramref name="request"/> the tree carries, with the axes it is read against, after every
    /// <see cref="Arrange"/>. A version is held until the person moves the grid, and applied against each
    /// layout until then, so a viewport measured twice settles where the last measure puts it; a version
    /// the person moved past is not applied again. Answers whether it moved the offset.
    /// </summary>
    bool Apply(TableScrollRequest? request, TableSpans rows, TableSpans columns, int pinnedRows,
        int pinnedColumns);

    /// <summary>
    /// Brings an <paramref name="area"/> of the viewport, in its own coordinates at the current offset,
    /// into the part the pinned bands leave uncovered, at once and on the axes it moves along there
    /// (<paramref name="across"/>, <paramref name="down"/>): what a host does when the focus lands on a
    /// control in a cell out of view. Answers whether it moved the offset.
    /// </summary>
    bool Reveal(Rect area, bool across, bool down);

    /// <summary>
    /// A key pressed while the viewport itself has the focus, in the DOM's names: the arrows move a step,
    /// PageUp and PageDown a page of the part that scrolls, Home and End to the start and the end of the
    /// rows (with Control or Command, of the columns too). Answers whether it took the key, which it
    /// does not where nothing moved, so the key travels on.
    /// </summary>
    bool Key(string key, KeyModifiers modifiers);

    /// <summary>
    /// A drag the composing component holds, at <paramref name="pointer"/> in the viewport's own
    /// coordinates, or null when it ends: while the pointer is past an edge of the part where the cells
    /// scroll (the pinned bands count as outside it), every <see cref="Advance"/> scrolls toward it, faster
    /// the further past it is, until the grid's end. Under reduced motion it scrolls all the same: the
    /// person is holding it there. <em>How does Flutter solve it?</em> <c>EdgeDraggingAutoScroller</c>.
    /// </summary>
    void AutoScroll(Point? pointer);
}
