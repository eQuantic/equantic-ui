using eQuantic.UI.Primitives;

namespace HydrationSite.Pages;

/// <summary>A page that hydrates as the server drew it: a heading and a counter.</summary>
[Page("/")]
public sealed class HomePage : StatefulComponent
{
    private int _count;

    public override VisualNode Build(ComponentContext context) =>
        Column(gap: Space.S3, children: [
            Text("Home", TypeRole.Heading),
            Button($"Pressed {_count} times", onPressed: () => SetState(() => _count++)),
        ]);
}
