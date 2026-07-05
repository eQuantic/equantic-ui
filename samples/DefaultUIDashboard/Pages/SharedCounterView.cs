using eQuantic.UI.Components.Shared;
using eQuantic.UI.Primitives;

namespace DefaultUIDashboard.Pages;

/// <summary>
/// The WRITE-ONCE view of the shared showcase: a pure abstract tree (Photon vocabulary + the shared
/// Button) fed by hoisted state — the same tree the native realizer could rasterize verbatim. Lives
/// in its own file so the page keeps Core usings and this keeps Primitives usings (the component-model
/// base names exist in both worlds until the Core unification).
/// </summary>
public static class SharedCounterView
{
    public static VisualNode Build(int count, Action increment)
    {
        var column = new Column(gap: Space.S3);
        column.Add(new Text("Write-once components", TypeRole.Title));
        column.Add(new Text(
            "This card is authored against the shared Photon vocabulary — the same C# renders as DOM here and as GPU pixels on native.",
            TypeRole.BodyM, maxLines: 3));
        column.Add(new Text($"Count: {count}", TypeRole.Heading));

        var actions = new Row(gap: Space.S2);
        actions.Add(new Button("Increment", onPressed: increment));
        actions.Add(new Button("Ghost", Variant.Ghost));
        actions.Add(new Button("Outline", Variant.Outline));
        column.Add(actions);

        return new Card(column, CardKind.Outlined) { Width = SizeValue.Fill };
    }
}
