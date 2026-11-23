using eQuantic.UI.Primitives;

namespace HydrationSite.Pages;

/// <summary>
/// An icon of a size only the browser reaches: the page sets it once it is mounted, which happens in the
/// browser alone, and an icon of 22 px is refused.
/// </summary>
[Page("/icon")]
public sealed class IconPage : StatefulComponent
{
    private float _size = IconSize.Md;

    protected override void OnMount() => SetState(() => _size = 22);

    public override VisualNode Build(ComponentContext context) =>
        Column(gap: Space.S2, children: [Text("Search", TypeRole.Heading), Icon(Icons.Search, _size)]);
}
