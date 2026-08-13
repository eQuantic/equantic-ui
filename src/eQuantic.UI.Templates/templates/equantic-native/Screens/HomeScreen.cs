using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using EQuanticNativeApp.Resources;

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
        // From Resources/Strings.resx — plain ResourceManager + satellites, in the language the
        // DEVICE reports (the shell sets the process culture before the first frame). A pt-BR
        // phone says "Pressionado 3 vezes" with no code for it here.
        column.Add(new Text(string.Format(Strings.PressedTimes, _count), TypeRole.BodyM,
            theme.TextSecondary, maxLines: 1));
        column.Add(new Button(Strings.CountUp, onPressed: () => SetState(() => _count++)));

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
        }, column);
    }
}
