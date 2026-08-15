namespace EQuanticApp;

/// <summary>
/// The frame every page sits in — and the responsive claim, on the web, with no listener.
/// <para>
/// Past 840dp the sections stand in a sidebar; under it they wrap into a strip beneath the header.
/// <c>AdaptiveNode</c> emits BOTH trees behind build-time media queries, so the right one is
/// already on screen at first paint, before any JavaScript has run. The same node on Photon lays
/// out only the variant that fits the window.
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

    private static readonly (string Href, string Label, Icons Icon)[] Sections =
    [
        ("/", "Overview", Icons.Table),
        ("/reports", "Reports", Icons.Info),
        ("/settings", "Settings", Icons.Person),
    ];

    private static readonly CultureOption[] Languages =
    [
        new("en", "English"),
        new("pt-BR", "Português"),
    ];

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        // Not just a colour: Current emits aria-current="page", which is how a screen reader learns
        // what the highlight tells everyone else.
        VisualNode Entry((string Href, string Label, Icons Icon) section, bool wide)
        {
            var active = section.Href == Route;
            var tint = active ? theme.Colors(Variant.Primary).OnSubtle : theme.TextSecondary;

            return new Link(section.Href, Box(new BoxStyle
            {
                Width = wide ? SizeValue.Fill : default,
                Padding = EdgeInsets.Symmetric(Space.S3, Space.S2),
                Background = active ? theme.Colors(Variant.Primary).Subtle : null,
                CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Small)),
            },
            Row(gap: Space.S2, cross: CrossAlign.Center, children: [
                Icon(section.Icon, IconSize.Sm, tint),
                Text(section.Label, TypeRole.Label, tint, maxLines: 1),
            ])))
            {
                Current = active,
            };
        }

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
            Spacer(),
            CultureSwitcher(Languages),
        ]));

        VisualNode Content() => Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S6),
        }, Child);

        // Compact: the sections wrap into a strip under the header. A sidebar at phone width is a
        // sidebar nobody can read the content beside.
        var narrow = Column(gap: 0, children: [
            header,
            Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Padding = EdgeInsets.Symmetric(Space.S4, Space.S2),
                Background = theme.Surface,
            },
            Row(gap: Space.S2, wrap: true, children: [
                .. Sections.Select(section => Entry(section, wide: false)),
            ])),
            Content(),
        ]);

        var wide = Column(gap: 0, children: [
            header,
            Row(gap: 0, cross: CrossAlign.Start, children: [
                Box(new BoxStyle
                {
                    Width = 240,
                    Padding = EdgeInsets.All(Space.S3),
                    Background = theme.Surface,
                },
                Column(gap: Space.S1, children: [
                    .. Sections.Select(section => Entry(section, wide: true)),
                ])),
                Flexible(Content()),
            ]),
        ]);

        return Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
        },
        AdaptiveNode(narrow, medium: null, expanded: wide));
    }
}
