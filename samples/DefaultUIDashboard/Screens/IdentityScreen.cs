using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// A page whose content depends on a service the container hands it: who is signed in. The browser
/// builds the page without that service, so before the value crossed as what the page reads of it, the
/// browser drew the other branch, and the whole object, its authority included, was in the HTML (#510).
/// <para>
/// The page source holds the name it shows and nothing else of the identity. The link keeps the
/// destination the server drew once the page hydrates, and after arriving here from another screen.
/// </para>
/// </summary>
[Page("/identity", Title = "Identity — eQuantic Console")]
public sealed class IdentityScreen(ConsoleIdentity? identity = null) : StatelessComponent
{
    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        // A stateless page owns no drawer state, so the frame gets a closed one, as the ticker's does.
        return ConsoleShell.Frame(theme, "/identity", "Identity",
            Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Height = SizeValue.Fill,
                Background = theme.Background,
                Padding = EdgeInsets.All(Space.S6),
            },
            Column(gap: Space.S4, children: [
                Text("Who is signed in", TypeRole.Heading, theme.TextPrimary),
                Text(identity is null ? "Nobody is signed in." : $"Signed in as {identity.DisplayName}.",
                    TypeRole.BodyL, theme.TextPrimary),
                Link(identity is null ? "/form" : "/server-data",
                    Text(identity is null ? "Sign in" : "See what the server knows", TypeRole.BodyM, theme.TextSecondary)),
            ])),
            navOpen: false, onToggleNav: () => { });
    }
}
