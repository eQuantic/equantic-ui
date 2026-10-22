using eQuantic.UI.Primitives;

namespace eQuantic.UI.Spreadsheets;

/// <summary>
/// An axis of the spreadsheet's table: one leading entry, the header's, before every row (or column) of
/// the document's own axis. Table index 0 is the header and table index <c>i + 1</c> the document's
/// <c>i</c>, so the header band and the cells share one layout and one scroll.
/// </summary>
internal sealed class LeadingSpans : TableSpans
{
    private readonly TableSpans _rest;
    private readonly double _lead;

    public LeadingSpans(TableSpans rest, double lead)
    {
        _rest = rest;
        _lead = lead;
    }

    /// <summary>The axis this one leads.</summary>
    public TableSpans Rest => _rest;

    /// <summary>How far the leading entry reaches.</summary>
    public double Lead => _lead;

    public override int Count => _rest.Count + 1;

    public override double Total => _lead + _rest.Total;

    public override double ExtentOf(int index) => index <= 0 ? _lead : _rest.ExtentOf(index - 1);

    public override double OffsetOf(int index) => index <= 0 ? 0 : _lead + _rest.OffsetOf(index - 1);

    public override int IndexAt(double offset) =>
        offset < _lead || _rest.Count == 0 ? 0 : _rest.IndexAt(offset - _lead) + 1;
}
