using eQuantic.UI.Components;
using eQuantic.UI.Primitives;

namespace EQuanticNativeApp;

/// <summary>The write-once counter: this same component renders on the web unchanged.</summary>
public sealed class HomeScreen : StatefulComponent
{
    private int _count;

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var column = new Column(gap: Space.S4)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Main = MainAlign.Center,
            Cross = CrossAlign.Center,
        };
        column.Add(new Text("EQuanticNativeApp", TypeRole.Heading, theme.TextPrimary, maxLines: 1));
        column.Add(new Text($"Pressed {_count} times", TypeRole.BodyM, theme.TextSecondary, maxLines: 1));
        column.Add(new Button("Count up", onPressed: () => SetState(() => _count++)));

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
        }, column);
    }
}
