using eQuantic.UI.Primitives;
using HydrationSite.Sections;

namespace HydrationSite.Pages;

/// <summary>The page that reaches the ribbon itself, which gives the ribbon a chunk apart from the showcase's.</summary>
[Page("/ribbon")]
public sealed class RibbonPage : StatelessComponent
{
    public override VisualNode Build(ComponentContext context) => new Ribbon("Today");
}
