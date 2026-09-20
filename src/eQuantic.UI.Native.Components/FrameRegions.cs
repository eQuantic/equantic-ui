namespace eQuantic.UI.Native.Components;

/// <summary>
/// The lists one frame collects its input into, in ONE object: what an <see cref="InputSink"/>
/// writes through, and what <see cref="RealizeResult"/> reads back when the walk is done.
/// <para>
/// They are grouped so the SINK can be a value. Everything a sink knows beyond these lists is two
/// facts about a place in the walk — the clip, and whether Tab stops are being collected — and a
/// walk narrows one or the other at every clipping Box and every composite control. While the sink
/// was a class, each narrowing was an object, on every frame, and
/// <c>SteadyMotion_WithRecycledFrames_AllocatesFarLess</c> charges those by the byte. Holding the
/// lists once makes the frame allocate this instead, and the narrowings cost nothing.
/// </para>
/// </summary>
internal sealed class FrameRegions
{
    public List<HitRegion> Hits { get; } = [];
    public List<HoverRegion> Hovers { get; } = [];
    public List<ScrollRegion> Scrolls { get; } = [];
    public List<DragRegion> Drags { get; } = [];
    public List<LinkRegion> Links { get; } = [];
    public List<ShortcutBinding> Shortcuts { get; } = [];
    public List<TextRegion> Texts { get; } = [];
    public List<FocusStop> Stops { get; } = [];
    public List<CodeRegion> Codes { get; } = [];
    public List<SheetRegion> Sheets { get; } = [];
    public List<CursorRegion> Cursors { get; } = [];
    public List<CanvasRegion> Canvases { get; } = [];

    /// <summary>
    /// The live regions this frame holds — LAZY, alone among the thirteen, and measured rather than
    /// assumed. Created eagerly beside the others it cost two bytes a frame, and
    /// <c>SteadyMotion_WithRecycledFrames_AllocatesFarLess</c> refused the frame at 75,778 against
    /// its 75,776 ceiling. That ceiling may only ever come down, and an accessibility feature that
    /// almost no frame uses is the last thing that should raise it: a page with no live region now
    /// allocates nothing for one, which is what <see cref="LivesOrEmpty"/> reads back.
    /// </summary>
    public List<LiveRegionMark> Lives => _lives ??= [];

    /// <summary>What the frame reads back, without creating the list to find out it is empty.</summary>
    public IReadOnlyList<LiveRegionMark> LivesOrEmpty =>
        (IReadOnlyList<LiveRegionMark>?)_lives ?? Array.Empty<LiveRegionMark>();

    private List<LiveRegionMark>? _lives;
}
