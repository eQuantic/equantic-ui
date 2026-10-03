using eQuantic.UI.Primitives;

namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>A page reading a report down to its scalars and through the collections that cross whole, each read landing as the server read it.</summary>
[Page("/crossing-report")]
public sealed class CrossingReportPage(CrossingReport report) : StatelessComponent
{
    public override VisualNode Build(ComponentContext context)
    {
        var page = new Column();
        page.Add(new Text($"count {report.Totals.Count * 2}", TypeRole.BodyM));
        page.Add(new Text(report.Totals.Count == 42 ? "count is 42" : "count is not 42", TypeRole.BodyM));
        page.Add(new Text($"x {report.Position.X}", TypeRole.BodyM));
        page.Add(new Text(report.Maybe.HasValue ? $"maybe {report.Maybe.Value.Count}" : "maybe none", TypeRole.BodyM));
        page.Add(new Text(report.Never.HasValue ? $"never {report.Never.Value.Count}" : "never none", TypeRole.BodyM));
        page.Add(new Text(report is { Tags.Count: > 0 } ? $"tag {report.Tags[0]}" : "no tags", TypeRole.BodyM));
        page.Add(new Text($"price {report.Prices["a"] * 2}", TypeRole.BodyM));
        page.Add(new Text($"scores {report.Scores.Count} first {report.Scores[0] + 1}", TypeRole.BodyM));
        return page;
    }
}
