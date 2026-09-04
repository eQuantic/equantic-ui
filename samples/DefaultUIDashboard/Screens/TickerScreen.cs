using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// The shape that leaked: a page with no state of ITS own, composing a component that subscribes to
/// a device. Most sections of a real site are written this way, and it is the one the framework got
/// wrong — a stateless page said it had nothing to release on navigation away, which was true until
/// it started retaining the nested stateful components it built.
/// <para>
/// Left unsaid, the <see cref="Pulse"/> below kept its clock after the visitor navigated: its
/// SetState drew it back into whatever page they had moved to, and one more timer stayed alive per
/// visit. Navigating away from here and watching the next page stay clean IS the test.
/// </para>
/// </summary>
[Page("/ticker", Title = "Ticker — eQuantic Console")]
public sealed class TickerScreen : StatelessComponent
{
    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        // A stateless page owns no drawer state, so the frame gets a closed one: what this screen
        // exists to exercise is the teardown, not the compact nav.
        return ConsoleShell.Frame(theme, "/ticker", "Ticker",
            Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Height = SizeValue.Fill,
                Background = theme.Background,
                Padding = EdgeInsets.All(Space.S6),
            },
            Column(gap: Space.S4, children: [
                Text("A page with no state, and a component with a clock", TypeRole.Heading,
                    theme.TextPrimary),
                Text("Navigate away and back. The dot must stop when this page goes, and nothing "
                    + "from here may appear on the page you land on.", TypeRole.BodyM,
                    theme.TextSecondary),
                new Pulse(),
            ])),
            navOpen: false, onToggleNav: () => { });
    }
}
