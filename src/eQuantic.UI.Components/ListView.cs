using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// A list that only BUILDS the rows you can see (plan M3, "list with recycling"). The author gives
/// a count, a fixed per-item extent, and a builder by index; the component materializes the
/// visible window plus an overscan margin, holds the rest as two spacers, and moves the window as
/// the <see cref="ScrollView"/> reports where it is. Ten thousand rows cost what a screenful
/// costs — on both targets, from this one source.
/// <para>
/// The window converges rather than blocks: the first frame builds against a viewport guess, the
/// realized frame reports the true viewport and offset through the ScrollView's out-channels, and
/// the next frame builds the corrected window. State only changes when the WINDOW does — pixels
/// scroll without a rebuild; rebuilds happen when a row enters or leaves the margin.
/// </para>
/// <para>
/// v1 fence: vertical, fixed <see cref="ItemExtent"/> (the fast path every platform ships;
/// variable extents need incremental measurement and join later).
/// </para>
/// </summary>
public sealed class ListView : StatefulComponent
{
    private float _offset;
    private float _viewport;
    private int _first;
    private int _last = -1;

    public ListView(int count, float itemExtent, Func<int, VisualNode> itemBuilder)
    {
        Count = count;
        ItemExtent = itemExtent;
        ItemBuilder = itemBuilder;
    }

    public int Count { get; private set; }
    public float ItemExtent { get; private set; }
    public Func<int, VisualNode> ItemBuilder { get; private set; }

    /// <summary>Rows built beyond each edge of the viewport, so a scroll shows content instead of
    /// a blank margin while the next window builds.
    /// <para>
    /// Backed by a field, so <see cref="AdoptConfig"/> can carry it, as are the width and the
    /// height: this instance is kept across the parent's rebuilds, and an <c>init</c> accessor
    /// cannot be written from there.
    /// </para>
    /// </summary>
    public int Overscan { get => _overscan; init => _overscan = value; }

    public SizeValue Width { get => _width; init => _width = value; }
    public SizeValue Height { get => _height; init => _height = value; }

    private int _overscan = 3;
    private SizeValue _width;
    private SizeValue _height;

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not ListView fresh) return;
        Count = fresh.Count;
        ItemExtent = fresh.ItemExtent;
        ItemBuilder = fresh.ItemBuilder;
        _overscan = fresh.Overscan;
        _width = fresh.Width;
        _height = fresh.Height;
    }

    private (int First, int Last) WindowFor(float offset, float viewport)
    {
        if (Count == 0 || ItemExtent <= 0) return (0, -1);
        var first = Math.Max(0, (int)MathF.Floor(offset / ItemExtent) - Overscan);
        var last = Math.Min(Count - 1, (int)MathF.Ceiling((offset + viewport) / ItemExtent) + Overscan);
        return (first, last);
    }

    /// <summary>Until the realized frame reports the true viewport: enough rows to cover any
    /// plausible first paint — a spare screenful, not the whole list.</summary>
    private float ViewportGuess() => ItemExtent * 12;

    private void OnScrolled(float offset)
    {
        _offset = offset;
        var (first, last) = WindowFor(offset, _viewport > 0 ? _viewport : ViewportGuess());
        if (first == _first && last == _last) return;   // pixels moved, the window did not
        SetState(() => { });
    }

    private void OnViewportChanged(float viewport)
    {
        if (Math.Abs(viewport - _viewport) < 0.5f) return;
        SetState(() => _viewport = viewport);
    }

    public override VisualNode Build(ComponentContext context)
    {
        var viewport = _viewport > 0 ? _viewport : ViewportGuess();
        (_first, _last) = WindowFor(_offset, viewport);

        var rows = new Column { Width = SizeValue.Fill };
        // The spacers ARE the rest of the list: layout sees the true content height, so the
        // scrollbar and MaxOffset are exactly what a fully-built list would produce.
        if (_first > 0) rows.Add(Spacer.Fixed(_first * ItemExtent));
        for (var i = _first; i <= _last; i++)
            rows.Add(ItemBuilder(i));
        if (_last < Count - 1) rows.Add(Spacer.Fixed((Count - 1 - _last) * ItemExtent));

        return new ScrollView(rows)
        {
            Width = Width,
            Height = Height,
            OnScrolled = OnScrolled,
            OnViewportChanged = OnViewportChanged,
        };
    }
}
