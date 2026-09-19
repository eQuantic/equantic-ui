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
