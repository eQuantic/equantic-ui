using eQuantic.UI.Primitives;
using eQuantic.UI.Server.Metadata;
using StatelessComponent = eQuantic.UI.Primitives.StatelessComponent;

namespace eQuantic.UI.Server.Tests.SameNamedPages.Admin;

/// <summary>One of two pages called <c>Dashboard</c>, in its own namespace (#514).</summary>
[Page("/same-named/admin")]
public sealed class Dashboard : StatelessComponent, IHandleMetadata
{
    public void ConfigureMetadata(SeoBuilder seo) => seo.Title("Admin Dashboard");

    public override VisualNode Build(ComponentContext context) => new Text("the admin dashboard", TypeRole.Heading);
}
