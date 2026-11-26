using eQuantic.UI.Primitives;

namespace HydrationSite.Pages;

/// <summary>
/// Labels the browser draws blank: the prefetch keeps what it loaded where nothing crosses to the browser,
/// so the server draws the label and the browser its default.
/// </summary>
[Page("/labels")]
public sealed class LabelsPage : StatelessComponent, IServerPrefetch
{
    private static string _title = "";

    [ServerOnly]
    public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        _title = "Our cakes";
        return Task.CompletedTask;
    }

    public override VisualNode Build(ComponentContext context) =>
        Column(gap: Space.S2, children: [Text(_title, TypeRole.Heading), Text("Fresh today", TypeRole.BodyM)]);
}
