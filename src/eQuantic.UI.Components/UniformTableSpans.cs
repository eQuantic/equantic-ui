using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// An axis whose rows (or columns) all reach as far: a grid of a million rows of 28 costs nothing to
/// describe, and every offset is one multiplication.
/// </summary>
public sealed class UniformTableSpans : TableSpans
{
    public UniformTableSpans(int count, double extent)
    {
        Count = Math.Max(0, count);
        Extent = Math.Max(0, extent);
    }

    public override int Count { get; }

    /// <summary>How far each one reaches.</summary>
    public double Extent { get; }

    public override double Total => Count * Extent;

    public override double ExtentOf(int index) => Extent;

    public override double OffsetOf(int index) => Math.Clamp(index, 0, Count) * Extent;

    public override int IndexAt(double offset)
    {
        if (Count == 0 || Extent <= 0) return 0;
        return Math.Clamp((int)Math.Floor(offset / Extent), 0, Count - 1);
    }
}
