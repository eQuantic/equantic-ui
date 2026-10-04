using eQuantic.UI.Primitives;
using eQuantic.UI.Server.Metadata;
using StatelessComponent = eQuantic.UI.Primitives.StatelessComponent;

namespace eQuantic.UI.Server.Tests.SameNamedPages.Shop;

/// <summary>The other <c>Dashboard</c>, in a namespace of its own, as an app with two areas has it (#514).</summary>
[Page("/same-named/shop")]
public sealed class Dashboard : StatelessComponent, IHandleMetadata
{
    public void ConfigureMetadata(SeoBuilder seo) => seo.Title("Shop Dashboard");

    public override VisualNode Build(ComponentContext context) => new Text("the shop dashboard", TypeRole.Heading);
}
