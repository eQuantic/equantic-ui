using eQuantic.UI.Components;
using eQuantic.UI.Primitives;

namespace EQuanticApp;

/// <summary>
/// The frame every page sits in. There is no Layout type to register: a shell is an ordinary
/// component, and because it is one, the build generates a factory for it — so a page writes
/// <c>AppShell("/", …)</c> exactly the way it writes <c>Column(…)</c>.
/// <para>
/// It takes the page's OWN route so the active link can say so. Not just a colour: <c>Current</c>
/// emits <c>aria-current="page"</c>, which is how a screen reader learns what the highlight tells
/// everyone else.
/// </para>
/// </summary>
public sealed class AppShell : StatelessComponent
{
    public AppShell(string route, VisualNode child)
    {
        Route = route;
        Child = child;
    }

    public string Route { get; init; }
    public VisualNode Child { get; init; }

    private static readonly (string Href, string Label)[] Sections =
    [
        ("/", "Home"),
        ("/about", "About"),
    ];

    // The languages this app ships — one resx per entry. The switcher re-renders in place (no
    // reload) and tells the server for the next request.
    private static readonly CultureOption[] Languages =
    [
        new("en", "English"),
        new("pt-BR", "Português"),
    ];

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        var header = Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.Symmetric(Space.S5, Space.S3),
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
        },
        Row(gap: Space.S4, cross: CrossAlign.Center, children: [
            Text("EQuanticApp", TypeRole.Title, theme.TextPrimary, maxLines: 1),
            .. Sections.Select(section => (VisualNode)new Link(section.Href,
                Text(section.Label, TypeRole.Label,
                    section.Href == Route ? theme.TextPrimary : theme.TextSecondary, maxLines: 1))
            {
                Current = section.Href == Route,
            }),
            Spacer(),
            CultureSwitcher(Languages),
        ]));

        return Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
        },
        Column(gap: 0, children: [
            header,
            Box(new BoxStyle { Width = SizeValue.Fill, Padding = EdgeInsets.All(Space.S6) }, Child),
        ]));
    }
}
