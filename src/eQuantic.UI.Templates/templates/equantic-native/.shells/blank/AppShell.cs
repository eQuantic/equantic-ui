using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using EQuanticNativeApp.Resources;

namespace EQuanticNativeApp;

/// <summary>
/// The root of the app — whatever shell you scaffolded, <c>Program.cs</c> names THIS type and
/// nothing else, so swapping the shape of the app never touches the entry point.
/// <para>
/// This is the blank one: a counter, and the write-once claim in eleven lines. The same source
/// renders in a browser unchanged. Scaffold with <c>--shell tabs</c>, <c>drawer</c> or
/// <c>list-detail</c> to start from a navigation shape instead.
/// </para>
/// </summary>
public sealed class AppShell : StatefulComponent
{
    private int _count;

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        return Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
        },
        Column(gap: Space.S4, main: MainAlign.Center, cross: CrossAlign.Center, children: [
            Text("EQuanticNativeApp", TypeRole.Heading, theme.TextPrimary, maxLines: 1),
            // From Resources/Strings.resx — plain ResourceManager + satellites, in the language the
            // DEVICE reports (the shell sets the process culture before the first frame). A pt-BR
            // phone says "Pressionado 3 vezes" with no code for it here.
            Text(string.Format(Strings.PressedTimes, _count), TypeRole.BodyM, theme.TextSecondary,
                maxLines: 1),
            Button(Strings.CountUp, onPressed: () => SetState(() => _count++)),
        ]));
    }
}
