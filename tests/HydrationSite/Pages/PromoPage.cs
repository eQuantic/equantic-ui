using eQuantic.UI.Primitives;

namespace HydrationSite.Pages;

/// <summary>The page the browser's router gives /promo, which the server never draws (#1778).</summary>
[Page("/promo")]
public sealed class PromoPage : StatelessComponent
{
    public override VisualNode Build(ComponentContext context) => Text("Promotions", TypeRole.Heading);
}
