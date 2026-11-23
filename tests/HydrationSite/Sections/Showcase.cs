using eQuantic.UI.Primitives;

namespace HydrationSite.Sections;

/// <summary>
/// The shelf both shelf pages show, holding a <see cref="Ribbon"/>. Built with <c>new</c>, as the pages build
/// it: a factory call goes through the one module of the app's factories, which would put every component in
/// one chunk and leave the shelf pages no chunk to import for its side effects (#1804).
/// </summary>
public sealed class Showcase : StatelessComponent
{
    public override VisualNode Build(ComponentContext context) =>
        Column(gap: Space.S2, children: [
            new Ribbon("New"),
            Text("Carrot cake"),
        ]);
}
