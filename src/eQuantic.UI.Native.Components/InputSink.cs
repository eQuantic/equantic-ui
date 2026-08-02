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
/// returns the sink for a nested clip; the lists are shared, only the rectangle narrows.
/// </para>
/// </summary>
internal sealed class InputSink(
    List<HitRegion> hits,
    List<HoverRegion> hovers,
    List<ScrollRegion> scrolls,
    List<DragRegion> drags,
    List<LinkRegion> links,
    List<ShortcutBinding> shortcuts,
    Rect? clip = null)
{
    /// <summary>The visible rectangle, or null at the top level where nothing is clipped.</summary>
    public Rect? Clip { get; } = clip;

    /// <summary>The same sink, narrowed to a nested clip. Clips INTERSECT: a scroll view inside a
    /// scroll view shows only what both agree on, and so does its input.</summary>
    public InputSink Under(Rect rect) =>
        new(hits, hovers, scrolls, drags, links, shortcuts,
            Clip is { } outer ? Intersect(outer, rect) : rect);

    public void Add(HitRegion region) { if (Visible(region.Bounds)) hits.Add(Clipped(region)); }

    public void Add(HoverRegion region) { if (Visible(region.Bounds)) hovers.Add(region); }

    public void Add(ScrollRegion region) { if (Visible(region.Bounds)) scrolls.Add(region); }

    public void Add(DragRegion region) { if (Visible(region.Bounds)) drags.Add(region); }

    public void Add(LinkRegion region) { if (Visible(region.Bounds)) links.Add(region); }

    /// <summary>A chord is not a place — being on screen is the whole subscription (spec S8), and a
    /// clip has nothing to say about it.</summary>
    public void Add(ShortcutBinding binding) => shortcuts.Add(binding);

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
