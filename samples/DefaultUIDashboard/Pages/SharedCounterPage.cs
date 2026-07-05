using eQuantic.UI.Components.Shared;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using StatefulComponent = eQuantic.UI.Primitives.StatefulComponent;

namespace DefaultUIDashboard.Pages;

/// <summary>
/// A page authored ENTIRELY in the write-once model — no Core wrapper, no state class, no adapter in
/// user code: a Primitives StatefulComponent with [Page]. The server bridges it through the web
/// realizer for SSR; the client mounts the transpiled class directly (SharedStatefulComponent). The
/// SAME class could be handed to a native PhotonHost verbatim.
/// </summary>
[Page("/counter-shared", Title = "Write-Once Counter Page")]
public sealed class SharedCounterPage : StatefulComponent
{
    private int _count;

    public override VisualNode Build(ComponentContext context)
    {
        var column = new Column(gap: Space.S3);
        column.Add(new Text("Write-once page", TypeRole.Title));
        column.Add(new Text($"Count: {_count}", TypeRole.Heading));
        column.Add(new Button("Increment", onPressed: () => SetState(() => _count++)));

        return new Card(column, CardKind.Outlined) { Width = SizeValue.Fill };
    }
}
