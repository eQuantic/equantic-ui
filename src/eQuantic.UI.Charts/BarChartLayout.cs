namespace eQuantic.UI.Charts;

/// <summary>One drawn bar or stacked segment, in the plot's own coordinates (dp, origin top-left).</summary>
/// <param name="Category">Which category it belongs to.</param>
/// <param name="Series">Which series (index into the chart's list) it belongs to.</param>
/// <param name="Negative">Whether it grows away from the baseline toward the axis's low end.</param>
/// <param name="DataEnd">Whether it carries the rounded DATA END — every grouped bar does, and only
/// the outermost segment of a stack; the segments under it end square, separated by the gap.</param>
public sealed record BarRect(int Category, int Series, float X, float Y, float Width, float Height,
    bool Negative, bool DataEnd);

/// <summary>Everything the marks of a bar chart are drawn from, solved once per size.</summary>
/// <param name="Baseline">Where zero (or the axis floor) sits: a Y for vertical bars, an X for
/// horizontal ones.</param>
public sealed record BarChartGeometry(float Width, float Height, ChartOrientation Orientation,
    ValueTicks Ticks, float Baseline, IReadOnlyList<BarRect> Bars)
{
    /// <summary>A tick's position along the value axis: a Y (vertical) or an X (horizontal).</summary>
    public float TickPosition(int index)
    {
        var across = Orientation == ChartOrientation.Vertical ? Height : Width;
        var offset = BarChartLayout.TickOffset(Ticks, index, across);
        return Orientation == ChartOrientation.Vertical ? Height - offset : offset;
    }
}

/// <summary>
/// The geometry of a bar chart as a PURE FUNCTION of its data and its box — the same arithmetic on
/// the server, on Photon and in the transpiled twin, pinned by one fixture both sides dump. The
/// marks' specs are constants here, not properties an author sets: bars at most 24dp thick with the
/// slot's leftover left as air, a 2dp surface gap between touching fills (stacked segments and
/// grouped neighbours alike, never a stroke), a 4dp rounded data end square at the baseline.
/// </summary>
public static class BarChartLayout
{
    public const float MaxThickness = 24f;
    public const float Gap = 2f;
    public const float DataEndRadius = 4f;
    /// <summary>How far past the painted pixels a bar still answers the pointer — the hit target is
    /// bigger than the mark.</summary>
    public const float HitSlack = 4f;

    /// <summary>How far from the value axis's LOW end tick <paramref name="index"/> sits, along an
    /// axis <paramref name="across"/> dp long.</summary>
    public static float TickOffset(ValueTicks ticks, int index, float across) =>
        ticks.Span <= 0 ? 0 : (float)((ticks.At(index) - ticks.Min) / ticks.Span) * across;

    /// <summary>The value domain the visible data spans (never excluding zero), as clean ticks —
    /// or the author's fixed bounds when both were given.</summary>
    public static ValueTicks Ticks(IReadOnlyList<ChartSeries> series, IReadOnlyList<bool> visible,
        int categoryCount, BarLayout layout, ValueAxis axis)
    {
        if (axis.Min.HasValue && axis.Max.HasValue)
            return ValueScale.Fixed(axis.Min.Value, axis.Max.Value, axis.Ticks);

        double low = 0;
        double high = 0;
        for (var c = 0; c < categoryCount; c++)
        {
            double positive = 0;
            double negative = 0;
            for (var s = 0; s < series.Count; s++)
            {
                if (!visible[s]) continue;
                var v = series[s].At(c);
                if (layout == BarLayout.Stacked)
                {
                    if (v >= 0) positive += v;
                    else negative += v;
                }
                else
                {
                    if (v > high) high = v;
                    if (v < low) low = v;
                }
            }

            if (layout == BarLayout.Stacked)
            {
                if (positive > high) high = positive;
                if (negative < low) low = negative;
            }
        }

        if (axis.Min.HasValue) low = axis.Min.Value;
        if (axis.Max.HasValue) high = axis.Max.Value;
        return ValueScale.Nice(low, high, axis.Ticks);
    }

    /// <summary>Solves every bar for a plot of <paramref name="width"/> × <paramref name="height"/>.</summary>
    public static BarChartGeometry Solve(IReadOnlyList<ChartSeries> series, IReadOnlyList<bool> visible,
        int categoryCount, BarLayout layout, ChartOrientation orientation, ValueAxis axis,
        float width, float height)
    {
        var ticks = Ticks(series, visible, categoryCount, layout, axis);
        var vertical = orientation == ChartOrientation.Vertical;
        var along = vertical ? width : height;     // the category axis
        var across = vertical ? height : width;    // the value axis

        // Bars grow from zero — or from the axis floor when zero is outside the domain, which is what
        // an author who fixed both bounds asked for.
        var baseValue = ticks.Min > 0 ? ticks.Min : ticks.Max < 0 ? ticks.Max : 0;
        var baseline = Offset(ticks, baseValue, across);

        var shown = new List<int>();
        for (var s = 0; s < series.Count; s++)
        {
            if (visible[s]) shown.Add(s);
        }

        var slot = categoryCount == 0 ? along : along / categoryCount;
        var bars = new List<BarRect>();

        for (var c = 0; c < categoryCount; c++)
        {
            var categoryStart = c * slot;
            if (layout == BarLayout.Grouped)
            {
                var n = shown.Count;
                if (n == 0) continue;
                var thickness = Math.Min(MaxThickness, (slot - (Gap * (n + 1))) / n);
                if (thickness < 1) thickness = 1;
                var group = (n * thickness) + ((n - 1) * Gap);
                var start = categoryStart + ((slot - group) / 2);
                for (var k = 0; k < n; k++)
                {
                    var s = shown[k];
                    var v = series[s].At(c);
                    var from = Offset(ticks, baseValue, across);
                    var to = Offset(ticks, v, across);
                    bars.Add(Rect(vertical, across, c, s, start + (k * (thickness + Gap)), thickness,
                        Math.Min(from, to), Math.Max(from, to), v < baseValue, true));
                }
            }
            else
            {
                var thickness = Math.Min(MaxThickness, slot - (2 * Gap));
                if (thickness < 1) thickness = 1;
                var position = categoryStart + ((slot - thickness) / 2);
                var lastPositive = -1;
                var lastNegative = -1;
                foreach (var s in shown)
                {
                    if (series[s].At(c) >= 0) lastPositive = s;
                    else lastNegative = s;
                }

                var positiveTop = baseValue;
                var negativeBottom = baseValue;
                foreach (var s in shown)
                {
                    var v = series[s].At(c);
                    double from;
                    double to;
                    bool dataEnd;
                    if (v >= 0)
                    {
                        from = positiveTop;
                        to = positiveTop + v;
                        positiveTop = to;
                        dataEnd = s == lastPositive;
                    }
                    else
                    {
                        to = negativeBottom;
                        from = negativeBottom + v;
                        negativeBottom = from;
                        dataEnd = s == lastNegative;
                    }

                    var low = Offset(ticks, Math.Min(from, to), across);
                    var high = Offset(ticks, Math.Max(from, to), across);
                    // The gap between touching segments: the inner ones end 2dp short of the next.
                    if (!dataEnd)
                    {
                        if (v >= 0) high -= Gap;
                        else low += Gap;
                        if (high < low) high = low;
                    }

                    bars.Add(Rect(vertical, across, c, s, position, thickness, low, high, v < 0, dataEnd));
                }
            }
        }

        return new BarChartGeometry(width, height, orientation, ticks, vertical ? height - baseline : baseline, bars);
    }

    /// <summary>The bar under (<paramref name="x"/>, <paramref name="y"/>) — its index into
    /// <see cref="BarChartGeometry.Bars"/>, or -1. The hit area is the mark plus the gap and then
    /// some (<see cref="HitSlack"/>), so a thin bar is not a pinpoint.</summary>
    public static int HitTest(BarChartGeometry geometry, float x, float y)
    {
        var bars = geometry.Bars;
        for (var i = 0; i < bars.Count; i++)
        {
            var b = bars[i];
            if (x >= b.X - HitSlack && x <= b.X + b.Width + HitSlack
                && y >= b.Y - HitSlack && y <= b.Y + b.Height + HitSlack)
                return i;
        }

        return -1;
    }

    /// <summary>A value's distance from the axis's low end.</summary>
    private static float Offset(ValueTicks ticks, double value, float across) =>
        ticks.Span <= 0 ? 0 : (float)((value - ticks.Min) / ticks.Span) * across;

    /// <summary><paramref name="low"/> and <paramref name="high"/> are distances from the value
    /// axis's low end; a vertical plot's Y grows downward from the top, so the bar's top is
    /// <paramref name="across"/> minus <paramref name="high"/>.</summary>
    private static BarRect Rect(bool vertical, float across, int category, int series, float position,
        float thickness, float low, float high, bool negative, bool dataEnd)
    {
        var length = high - low;
        return vertical
            ? new BarRect(category, series, position, across - high, thickness, length, negative, dataEnd)
            : new BarRect(category, series, low, position, length, thickness, negative, dataEnd);
    }
}
