namespace eQuantic.UI.Primitives;

/// <summary>
/// One axis of a table: how many rows (or columns) it has, how far each one reaches, and where each
/// one starts. A <see cref="TableViewport"/> is handed one per axis.
/// <para>
/// <em>How does Flutter solve it?</em> Its <c>TableView</c> asks a builder for one <c>TableSpan</c> per
/// index. An axis is asked for here instead, because the offset of row 1 048 575 is a sum over every
/// row before it, which an implementation can keep as prefix sums and a callback per index cannot.
/// Distances are <see langword="double"/>: a million rows of 28 points is past what a
/// <see langword="float"/> holds exactly.
/// </para>
/// <para>
/// A CLASS, not an interface, and on purpose: an interface in a component's constructor is a
/// dependency the container provides (<c>CapabilityRule</c>), and an axis is data a table is handed,
/// as <see cref="System.IO.Stream"/> is a class for the same reason in .NET. The components ship a
/// uniform axis and a sparse one; the spreadsheet's engine adapts its document.
/// </para>
/// </summary>
public abstract class TableSpans
{
    protected TableSpans() { }

    /// <summary>How many rows, or columns.</summary>
    public abstract int Count { get; }

    /// <summary>How far the whole axis reaches: the offset of the end of the last one.</summary>
    public abstract double Total { get; }

    /// <summary>How far the one at <paramref name="index"/> reaches along the axis.</summary>
    public abstract double ExtentOf(int index);

    /// <summary>Where the one at <paramref name="index"/> starts, from the start of the axis. The offset
    /// of <see cref="Count"/> is <see cref="Total"/>.</summary>
    public abstract double OffsetOf(int index);

    /// <summary>The one an <paramref name="offset"/> along the axis falls in, held between the first and
    /// the last.</summary>
    public abstract int IndexAt(double offset);
}
