using eQuantic.UI.Primitives;
using eQuantic.UI.Server.Metadata;
using StatelessComponent = eQuantic.UI.Primitives.StatelessComponent;

namespace eQuantic.UI.Server.Tests.SameNamedPages.Reports;

/// <summary>The other <c>Report</c>, also routed with <c>MapPage&lt;T&gt;</c> and no attribute (#514).</summary>
// The shared name IS the case under test, and the factory surface may keep the other Report's
// factory (EQ3102): no test builds either one through it.
#pragma warning disable EQ3102
public sealed class Report : StatelessComponent, IHandleMetadata
{
    public void ConfigureMetadata(SeoBuilder seo) => seo.Title("Reports Report");

    public override VisualNode Build(ComponentContext context) => new Text("the reports report", TypeRole.Heading);
}
#pragma warning restore EQ3102
