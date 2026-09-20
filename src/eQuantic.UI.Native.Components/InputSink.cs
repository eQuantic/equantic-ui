using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Where a frame's POINTER-REACHABLE regions are collected, and the clip they are collected under.
/// <para>
/// The clip is the point. A ScrollView (or a clipping Box) confines its subtree's PIXELS to a
/// viewport; the regions a finger is routed to have to obey the same rectangle, or a control that
/// scrolled out of sight keeps taking taps meant for whatever is drawn there now. That is not a
/// subtle failure: with a toolbar above a list, the toolbar stops responding the moment the list
/// scrolls, and the tap lands on a button nobody can see.
/// </para>
/// <para>
/// Carrying the clip HERE rather than beside the lists is what makes it structural — a region can
/// only be added through this sink, and this sink cannot add one it would not show. <see cref="Under"/>
/// returns the sink for a nested clip; the lists are shared (<see cref="FrameRegions"/>), only the
/// rectangle narrows. A VALUE for that reason: narrowing happens at every clipping Box and every
/// composite control, so the scoping has to be free.
/// </para>
/// </summary>
internal readonly struct InputSink(FrameRegions regions, Rect? clip = null, bool suppressFocusStops = false)
{
    /// <summary>The visible rectangle, or null at the top level where nothing is clipped.</summary>
    public Rect? Clip { get; } = clip;

    /// <summary>The same sink, narrowed to a nested clip. Clips INTERSECT: a scroll view inside a
    /// scroll view shows only what both agree on, and so does its input.</summary>
    public InputSink Under(Rect rect) =>
        new(regions, Clip is { } outer ? Intersect(outer, rect) : rect, suppressFocusStops);

    /// <summary>
    /// The same sink, with Tab stops suppressed — a control that is ONE stop for what it holds
    /// descends with this, so the controls inside it stay pointer-only.
    /// </summary>
    public InputSink WithoutFocusStops() => new(regions, Clip, suppressFocusStops: true);

    /// <summary>
    /// A stop that belongs to no region of its own: a COMPOSITE's (an Adjustable, a Navigable — one
    /// Tab stop with a keyboard of its own) or a LINK's. Every other stop is registered by the
    /// region that implies it, one for one; these two are where the counts differ — a link is
    /// reached per RECTANGLE by the pointer (both lines of a wrapped one) and once by the keyboard,
    /// and a composite replaces the stops inside it rather than adding to them.
    /// <para>
    /// SUPPRESSED like every other, and a composite's is no exception — which it used to be, on the
    /// reasoning that a replacement cannot be suppressed. That is true of a composite's OWN
    /// suppression and of nothing else: it registers here through the sink it was handed and only
    /// then descends with <see cref="WithoutFocusStops"/>, so the bypass never bought the case it
    /// was written for. What it bought was the defect this PR removed twice already, once more:
    /// an Adjustable inside a Pressable announced <c>Button@r/0</c> and stopped at <c>r/0</c> AND
    /// <c>r/0/0</c> — a Tab stop no reader names, because the Pressable consumed the subtree in
    /// the semantics walk.
    /// </para>
    /// </summary>
    public void Add(FocusStop stop)
    {
        if (!suppressFocusStops) regions.Stops.Add(stop);
    }

    public void Add(HitRegion region)
    {
        // A control with nothing to DO is not somewhere Tab should ever land — disabled, or a
        // handler-less pressable that only exists as another control's visual. Scrolled out of
        // sight is NOT the same thing: see FocusStop.
        if (!suppressFocusStops && !region.Node.Disabled && region.Node.OnPressed is not null)
            regions.Stops.Add(new FocusStop(region.Path, region.Node, null, region.Bounds));
        if (!Visible(region.Bounds)) return;
        regions.Hits.Add(Clipped(region));
    }

    public void Add(HoverRegion region) { if (Visible(region.Bounds)) regions.Hovers.Add(region); }

    public void Add(CursorRegion region) { if (Visible(region.Bounds)) regions.Cursors.Add(region); }

    public void Add(CanvasRegion region) { if (Visible(region.Bounds)) regions.Canvases.Add(region); }

    public void Add(ScrollRegion region) { if (Visible(region.Bounds)) regions.Scrolls.Add(region); }

    public void Add(DragRegion region) { if (Visible(region.Bounds)) regions.Drags.Add(region); }

    public void Add(LinkRegion region) { if (Visible(region.Bounds)) regions.Links.Add(region); }

    public void Add(TextRegion region)
    {
        if (!suppressFocusStops && !region.Entry.Disabled)
            regions.Stops.Add(new FocusStop(region.Path, null, region.Entry, region.Bounds));
        if (!Visible(region.Bounds)) return;
        regions.Texts.Add(region);
    }

    public void Add(CodeRegion region)
    {
        if (!suppressFocusStops)
            regions.Stops.Add(new FocusStop(region.Path, null, null, region.Bounds, null, region.Surface));
        if (!Visible(region.Bounds)) return;
        regions.Codes.Add(region);
    }

    /// <summary>A chord is not a place — being on screen is the whole subscription (spec S8), and a
    /// clip has nothing to say about it.</summary>
    public void Add(ShortcutBinding binding) => regions.Shortcuts.Add(binding);

    public void Add(SheetRegion region)
    {
        // The stop carries the SURFACE, like a text entry and a code surface carry theirs. It used
        // to carry nothing but a path, and every reader then had to work out what it was by
        // ELIMINATION — which is how a Navigable stop, added later and also carrying neither, fell
        // through a branch meant for editing surfaces and put a calendar into text mode.
        if (!suppressFocusStops)
            regions.Stops.Add(new FocusStop(region.Path, null, null, region.Bounds, Sheet: region.Surface));
        if (!Visible(region.Bounds)) return;
        regions.Sheets.Add(region);
    }

    /// <summary>Whether any of the region survives the clip. A region entirely outside it is drawn
    /// nowhere, so it is touched nowhere.</summary>
    private bool Visible(Rect bounds) =>
        Clip is not { } clip
        || (bounds.Right > clip.Left && bounds.Left < clip.Right
            && bounds.Bottom > clip.Top && bounds.Top < clip.Bottom);

    /// <summary>A region straddling the clip edge keeps only the part on screen — the half-scrolled
    /// row takes a tap on the half you can see, and none on the half you cannot.</summary>
    private HitRegion Clipped(HitRegion region) =>
        Clip is { } clip ? region with { Bounds = Intersect(clip, region.Bounds) } : region;

    private static Rect Intersect(Rect a, Rect b)
    {
        var left = Math.Max(a.Left, b.Left);
        var top = Math.Max(a.Top, b.Top);
        return new Rect(left, top,
            Math.Max(0, Math.Min(a.Right, b.Right) - left),
            Math.Max(0, Math.Min(a.Bottom, b.Bottom) - top));
    }
}
