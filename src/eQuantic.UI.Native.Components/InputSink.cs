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
/// only be added through this sink, and this sink cannot add one it would not show. <see cref="Under(Rect)"/>
/// returns the sink for a nested clip; the lists are shared (<see cref="FrameRegions"/>), only the
/// rectangle narrows. A VALUE for that reason: narrowing happens at every clipping Box and every
/// composite control, so the scoping has to be free.
/// </para>
/// </summary>
internal readonly struct InputSink(FrameRegions regions, Rect? clip = null, bool suppressFocusStops = false,
    Matrix2D? transform = null)
{
    /// <summary>The visible rectangle, or null at the top level where nothing is clipped.</summary>
    public Rect? Clip { get; } = clip;

    /// <summary>Where the subtree's layout is drawn: the transforms of the boxes around it, composed,
    /// or null where none is.</summary>
    public Matrix2D? Transform { get; } = transform;

    /// <summary>The same sink, narrowed to a nested clip. Clips INTERSECT: a scroll view inside a
    /// scroll view shows only what both agree on, and so does its input.</summary>
    public InputSink Under(Rect rect)
    {
        var placed = Place(rect);
        return new(regions, Clip is { } outer ? Intersect(outer, placed) : placed, suppressFocusStops, Transform);
    }

    /// <summary>
    /// The same sink, for a subtree drawn under <paramref name="matrix"/>: a box's transform, applied
    /// inside the ones around it. A region registers where it is DRAWN, as CSS hit-tests a transformed
    /// element and Flutter's <c>Transform</c> does: laid out at its rect and drawn 20 lower, a box took
    /// the presses and the hover over the strip it had left and none over itself (#513).
    /// </summary>
    public InputSink Under(Matrix2D matrix) =>
        new(regions, Clip, suppressFocusStops, Transform is { } outer ? matrix * outer : matrix);

    /// <summary>
    /// The same sink, with Tab stops suppressed — a control that is ONE stop for what it holds
    /// descends with this, so the controls inside it stay pointer-only.
    /// </summary>
    public InputSink WithoutFocusStops() => new(regions, Clip, suppressFocusStops: true, Transform);

    /// <summary>The box <paramref name="rect"/>, laid out in this subtree, is drawn in on screen: the
    /// box around its four corners under the transform, which is the rect itself where none is.</summary>
    private Rect Place(Rect rect) => Transform is { } m ? m.TransformBounds(rect) : rect;

    /// <summary>
    /// Records what turns a point on screen back into the space of the region about to be the
    /// <paramref name="index"/>th of <paramref name="list"/>, when a transform drew it: the box it is
    /// drawn in is exact for a translation and a scale, and a tilted region is tested against its
    /// own shape through this.
    /// </summary>
    private void Note(object list, int index, Rect local, Rect localDrawn)
    {
        if (Transform is not { } m || m.Invert() is not { } inverse) return;
        regions.Transformed.Add(new TransformedRegion(list, index, inverse, local, localDrawn));
    }

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
        if (!suppressFocusStops) regions.Stops.Add(stop with { Bounds = Place(stop.Bounds) });
    }

    public void Add(HitRegion region)
    {
        // A control with nothing to DO is not somewhere Tab should ever land — disabled, or a
        // handler-less pressable that only exists as another control's visual — and neither is one
        // that may not take the keyboard (Pressable.CanRequestFocus). Scrolled out of sight is NOT
        // the same thing: see FocusStop.
        var placed = region with { Bounds = Place(region.Bounds), Drawn = Place(region.Drawn) };
        if (!suppressFocusStops && !region.Node.Disabled && region.Node.OnPressed is not null
            && region.Node.CanRequestFocus)
            regions.Stops.Add(new FocusStop(region.Path, region.Node, null, placed.Bounds));
        if (!Visible(placed.Bounds)) return;
        Note(regions.Hits, regions.Hits.Count, region.Bounds, region.Drawn);
        regions.Hits.Add(Clipped(placed));
    }

    public void Add(HoverRegion region)
    {
        var placed = region with { Bounds = Place(region.Bounds) };
        if (!Visible(placed.Bounds)) return;
        Note(regions.Hovers, regions.Hovers.Count, region.Bounds, region.Bounds);
        regions.Hovers.Add(placed);
    }

    public void Add(CursorRegion region)
    {
        var placed = region with { Bounds = Place(region.Bounds) };
        if (!Visible(placed.Bounds)) return;
        Note(regions.Cursors, regions.Cursors.Count, region.Bounds, region.Bounds);
        regions.Cursors.Add(placed);
    }

    public void Add(CanvasRegion region)
    {
        var placed = region with { Bounds = Place(region.Bounds) };
        if (!Visible(placed.Bounds)) return;
        Note(regions.Canvases, regions.Canvases.Count, region.Bounds, region.Bounds);
        regions.Canvases.Add(placed);
    }

    public void Add(ScrollRegion region)
    {
        var placed = region with { Bounds = Place(region.Bounds) };
        if (!Visible(placed.Bounds)) return;
        Note(regions.Scrolls, regions.Scrolls.Count, region.Bounds, region.Bounds);
        regions.Scrolls.Add(placed);
    }

    public void Add(DragRegion region)
    {
        var placed = region with { Bounds = Place(region.Bounds) };
        if (!Visible(placed.Bounds)) return;
        Note(regions.Drags, regions.Drags.Count, region.Bounds, region.Bounds);
        regions.Drags.Add(placed);
    }

    /// <summary>
    /// A link is registered WHEREVER it is, clipped to what shows — the one region that is not
    /// dropped when it scrolls out of sight.
    /// <para>
    /// Following a link is not a pointer act. The keyboard and a reader's activate both find it by
    /// PATH (<c>PhotonHost.ActivatePath</c>), and a focus stop deliberately survives being scrolled
    /// away — so a region dropped off-screen made activate return TRUE and navigate nowhere:
    /// 20 stops, 5 regions, and the reader's activate on the twentieth silently did nothing.
    /// </para>
    /// <para>
    /// The RECTANGLE still obeys the clip, which is what keeps the pointer honest: a link entirely
    /// outside intersects to zero area, and <c>Rect.Contains</c> is false for every point of a rect
    /// whose left edge is its right one. That is the same trade <see cref="Clipped(HitRegion)"/>
    /// makes for a half-scrolled row, taken one step further because the identity has to outlive
    /// the visibility.
    /// </para>
    /// </summary>
    public void Add(LinkRegion region)
    {
        Note(regions.Links, regions.Links.Count, region.Bounds, region.Bounds);
        regions.Links.Add(Clipped(region with { Bounds = Place(region.Bounds) }));
    }

    public void Add(TextRegion region)
    {
        var placed = region with { Bounds = Place(region.Bounds) };
        if (!suppressFocusStops && !region.Entry.Disabled)
            regions.Stops.Add(new FocusStop(region.Path, null, region.Entry, placed.Bounds));
        if (!Visible(placed.Bounds)) return;
        Note(regions.Texts, regions.Texts.Count, region.Bounds, region.Bounds);
        regions.Texts.Add(Clipped(placed));
    }

    public void Add(CodeRegion region)
    {
        var placed = region with
        {
            Bounds = Place(region.Bounds),
            Offered = region.Offered is { } offered ? Place(offered) : null,
        };
        if (!suppressFocusStops)
            regions.Stops.Add(new FocusStop(region.Path, null, null, placed.Bounds, null, region.Surface));
        if (!Visible(placed.Bounds)) return;
        Note(regions.Codes, regions.Codes.Count, region.Bounds, region.Bounds);
        regions.Codes.Add(Clipped(placed));
    }

    /// <summary>A chord is not a place — being on screen is the whole subscription (spec S8), and a
    /// clip has nothing to say about it.</summary>
    public void Add(ShortcutBinding binding) => regions.Shortcuts.Add(binding);

    /// <summary>
    /// A live region is registered WHEREVER it is, like a link and for the same reason: being seen
    /// is not the test. A status line scrolled below the fold still changed, and a reader that only
    /// hears about what happens to be on screen is a reader that misses the upload finishing.
    /// Announcing is not a pointer act, so the clip has nothing to say about it.
    /// </summary>
    public void Add(LiveRegionMark mark) => regions.Lives.Add(mark);

    public void Add(SheetRegion region)
    {
        // The stop carries the SURFACE, like a text entry and a code surface carry theirs. It used
        // to carry nothing but a path, and every reader then had to work out what it was by
        // ELIMINATION — which is how a Navigable stop, added later and also carrying neither, fell
        // through a branch meant for editing surfaces and put a calendar into text mode.
        var placed = region with { Bounds = Place(region.Bounds) };
        if (!suppressFocusStops)
            regions.Stops.Add(new FocusStop(region.Path, null, null, placed.Bounds, Sheet: region.Surface));
        if (!Visible(placed.Bounds)) return;
        Note(regions.Sheets, regions.Sheets.Count, region.Bounds, region.Bounds);
        regions.Sheets.Add(Clipped(placed));
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
        Clip is { } clip
            ? region with { Bounds = Intersect(clip, region.Bounds), Drawn = Intersect(clip, region.Drawn) }
            : region;

    /// <summary>
    /// A code surface keeps its whole bounds, which place its caret and turn a point into a position,
    /// and carries the part of them on screen, which is all a press can land on: unclipped, a long
    /// file's surface took the presses aimed at whatever stands below the editor that shows it. What it
    /// offers at its caret is clipped the same way, so a list that left the view with its line takes
    /// no press outside it.
    /// </summary>
    private CodeRegion Clipped(CodeRegion region) =>
        Clip is { } clip
            ? region with
            {
                Visible = Intersect(clip, region.Bounds),
                Offered = region.Offered is { } offered ? Intersect(clip, offered) : null,
            }
            : region;

    /// <summary>
    /// A field and a sheet keep their whole bounds, which place the caret and the cells, and carry
    /// the part on screen, which is all a press can land on: as a code surface did until #297, one
    /// that ran past its scroll view took the presses aimed at whatever stands outside it (#635).
    /// </summary>
    private TextRegion Clipped(TextRegion region) =>
        Clip is { } clip ? region with { Visible = Intersect(clip, region.Bounds) } : region;

    /// <inheritdoc cref="Clipped(TextRegion)"/>
    private SheetRegion Clipped(SheetRegion region) =>
        Clip is { } clip ? region with { Visible = Intersect(clip, region.Bounds) } : region;

    /// <inheritdoc cref="Clipped(HitRegion)"/>
    private LinkRegion Clipped(LinkRegion region) =>
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
