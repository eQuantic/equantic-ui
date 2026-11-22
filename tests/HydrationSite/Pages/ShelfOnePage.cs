using eQuantic.UI.Primitives;
using HydrationSite.Sections;

namespace HydrationSite.Pages;

/// <summary>A shelf page: it reaches the ribbon only through the showcase (see <see cref="Sections.Ribbon"/>).</summary>
[Page("/shelf/one")]
public sealed class ShelfOnePage : StatelessComponent
{
    public override VisualNode Build(ComponentContext context) => new Showcase();
}
