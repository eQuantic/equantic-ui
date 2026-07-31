using DefaultUIDashboard.Components;
using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using StatefulComponent = eQuantic.UI.Core.StatefulComponent;

namespace DefaultUIDashboard.Pages;

/// <summary>
/// The counter demo, migrated to the write-once library: a CORE page (state + routing) whose body is
/// a shared-vocabulary card through the <see cref="VisualNodeComponent"/> bridge — no legacy
/// components, no utility CSS. The three buttons are the same write-once <c>Button</c> the native
/// realizer rasterizes.
/// </summary>
[Page("/counter", Title = "Counter Demo")]
public class Counter : StatefulComponent
{
    public override ComponentState CreateState() => new CounterState();
}

public class CounterState : ComponentState<Counter>
{
    private int _count = 0;

    public override IComponent Build(RenderContext context)
    {
        var body = new Column(gap: Space.S4) { Cross = CrossAlign.Center, Padding = EdgeInsets.All(Space.S6) };
        body.Add(new Text("Interactive counter", TypeRole.Title));
        body.Add(new Text($"{_count}", TypeRole.Display));

        var actions = new Row(gap: Space.S3) { Cross = CrossAlign.Center };
        actions.Add(new Button("−", Variant.Outline, onPressed: () => SetState(() => _count--)));
        actions.Add(new Button("Reset", Variant.Ghost, onPressed: () => SetState(() => _count = 0)));
        actions.Add(new Button("+", onPressed: () => SetState(() => _count++)));
        body.Add(actions);

        return new SampleShell
        {
            ActivePath = "/counter",
            Children =
            {
                new VisualNodeComponent(new Card(body, CardKind.Outlined) { Width = SizeValue.Fill }),
            },
        };
    }
}
