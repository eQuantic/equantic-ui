using eQuantic.UI.Primitives;

namespace HydrationSite.Pages;

/// <summary>The catalogue, which the server also draws for /promo (Program.cs).</summary>
[Page("/catalogue")]
public sealed class CataloguePage : StatelessComponent
{
    public override VisualNode Build(ComponentContext context) => Text("The catalogue", TypeRole.Heading);
}
