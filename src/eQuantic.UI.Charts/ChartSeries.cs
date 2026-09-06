using System.Globalization;

namespace eQuantic.UI.Charts;

/// <summary>
/// One named sequence of values, one per category of the chart it is plotted on. A series takes
/// its palette slot ONCE — <see cref="Slot"/>, or its position in the chart's list when none is
/// given — and keeps it: colour follows the entity, never its rank, so hiding a neighbour from the
/// legend never repaints this one.
/// </summary>
/// <param name="Name">What the legend and the tooltip call it.</param>
/// <param name="Values">One value per category; a missing tail reads as zero.</param>
/// <param name="Slot">The palette slot (0–7) this series keeps, or -1 for its position.</param>
public sealed record ChartSeries(string Name, IReadOnlyList<double> Values, int Slot = -1)
{
    /// <summary>The slot this series draws with when it sits at <paramref name="position"/>.</summary>
    public int SlotAt(int position) => Slot >= 0 ? Slot : position;

    /// <summary>The value at <paramref name="category"/>, zero past the end.</summary>
    public double At(int category) => category < Values.Count ? Values[category] : 0;
}

/// <summary>How several series share a category: side by side, or one on top of another.</summary>
public enum BarLayout : byte
{
    Grouped = 0,
    Stacked = 1,
}

/// <summary>Which way the bars grow. Vertical bars are columns; horizontal bars read long category
/// names best, which is what the method recommends for part-to-whole with many categories.</summary>
public enum ChartOrientation : byte
{
    Vertical = 0,
    Horizontal = 1,
}

/// <summary>The category axis: the names the bars are grouped by, in order.</summary>
/// <param name="Categories">One label per category.</param>
/// <param name="Title">The axis title, when the categories need one.</param>
public sealed record CategoryAxis(IReadOnlyList<string> Categories, string? Title = null);

/// <summary>
/// The ONE value axis. There is no second: two measures of different scale are two charts, or one
/// indexed to a common base — a dual axis invents a correlation the data does not carry.
/// </summary>
/// <param name="Title">The axis title.</param>
/// <param name="Min">A fixed lower bound; the data's own (never above zero) when null.</param>
/// <param name="Max">A fixed upper bound; the data's own (never below zero) when null.</param>
/// <param name="Format">The .NET format the ticks and the tooltip use, applied in the request's
/// culture — <c>N0</c>, <c>N2</c>, <c>P0</c>, <c>C0</c> are the ones that cross to every target.</param>
/// <param name="Ticks">How many tick labels to aim for; the scale snaps to clean steps.</param>
public sealed record ValueAxis(string? Title = null, double? Min = null, double? Max = null,
    string Format = "N0", int Ticks = 5)
{
    /// <summary>A value as the axis shows it.</summary>
    public string Label(double value) => value.ToString(Format, CultureInfo.CurrentCulture);
}
