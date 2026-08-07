using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using StatefulComponent = eQuantic.UI.Primitives.StatefulComponent;

namespace EQuanticApp.Pages;

/// <summary>
/// Your first write-once page: this C# renders on the server, compiles to JavaScript at build
/// time, and the SAME source realizes natively when you add a Photon shell. No JS was written.
/// </summary>
[Page("/", Title = "EQuanticApp")]
public sealed class HomePage : StatefulComponent
{
    private int _count;

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        var body = new Column(gap: Space.S4)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Main = MainAlign.Center,
            Cross = CrossAlign.Center,
        };
        body.Add(new Text("EQuanticApp", TypeRole.Display, theme.TextPrimary, maxLines: 1));
        body.Add(new Text("C# in, pixels out — the counter below is a component, a state field and a handler.",
            TypeRole.BodyM, theme.TextSecondary, maxLines: 2));
        body.Add(new Text($"{_count}", TypeRole.Display, theme.TextPrimary, maxLines: 1) { Tabular = true });

        var actions = new Row(gap: Space.S3);
        actions.Add(new Button("Count", Variant.Primary, SizeVariant.Medium,
            onPressed: () => SetState(() => _count++)));
        actions.Add(new Button("Reset", Variant.Outline, SizeVariant.Medium,
            onPressed: () => SetState(() => _count = 0)) { Disabled = _count == 0 });
        body.Add(actions);

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
            Padding = EdgeInsets.All(Space.S6),
        }, body);
    }
}
