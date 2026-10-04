using eQuantic.UI.Primitives;
using eQuantic.UI.Server.Metadata;
using StatelessComponent = eQuantic.UI.Primitives.StatelessComponent;

namespace eQuantic.UI.Server.Tests.SameNamedPages.Mapped;

/// <summary>
/// A page with no [Page] attribute, routed with <c>MapPage&lt;T&gt;</c> beside another <c>Report</c>
/// (<see cref="Reports.Report"/>), so the mapped endpoint's own path is pinned too (#514).
/// </summary>
public sealed class Report : StatelessComponent, IHandleMetadata
{
    public void ConfigureMetadata(SeoBuilder seo) => seo.Title("Mapped Report");

    public override VisualNode Build(ComponentContext context) => new Text("the mapped report", TypeRole.Heading);
}
