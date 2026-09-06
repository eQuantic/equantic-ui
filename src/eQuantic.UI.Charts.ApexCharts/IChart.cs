using eQuantic.UI.Web;

namespace eQuantic.UI.Charts.ApexCharts;

/// <summary>What the ApexCharts wrapper promises a page: a title and responsiveness. This used to
/// live in <c>eQuantic.UI.Charts</c>, which is now the write-once chart library
/// (docs/CHARTS-PLAN.md); the wrapper carries its own copy until it goes (slice 5 of that plan).</summary>
public interface IChart : IComponent
{
    string? Title { get; set; }
    bool Responsive { get; set; }
}
