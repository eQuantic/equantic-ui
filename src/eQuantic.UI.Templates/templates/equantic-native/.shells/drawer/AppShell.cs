using eQuantic.UI.Components;
using static eQuantic.UI.Components.UI;
using eQuantic.UI.Primitives;
using EQuanticNativeApp.Screens;

namespace EQuanticNativeApp;

/// <summary>
/// The drawer shell — for an app with more destinations than a bar can hold, or with sections a
/// user visits rarely enough that they should not own a permanent band of the screen.
/// <para>
/// It is the tabs shell's adaptive rule one step further: on a phone the panel SLIDES OVER the
/// content with a scrim taking the tap that dismisses it; past 840dp the same list is simply docked
/// beside the content, because a window that wide has room to stop hiding it. One list, one
/// handler, no listener anywhere.
/// </para>
/// </summary>
public sealed class AppShell : StatefulComponent
{
    private static readonly NavItem[] Destinations =
    [
        new(Icons.Notifications, "Activity"),
        new(Icons.Search, "Search"),
        new(Icons.Person, "Profile"),
    ];

    private int _destination;
    private bool _open;

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        VisualNode Screen() => _destination switch
        {
            1 => new SearchScreen(),
            2 => new ProfileScreen(),
            _ => new ActivityScreen(),
        };

        // Built fresh for each variant: the modal panel and the docked one are two trees, and a
        // node lives in exactly one.
        VisualNode Menu() => Box(new BoxStyle
        {
            Width = 280,
            Height = SizeValue.Fill,
            Background = theme.Surface,
            Padding = EdgeInsets.Symmetric(Space.S2, Space.S3),
        },
        Column(gap: Space.S1, children: [
            Box(new BoxStyle { Padding = EdgeInsets.All(Space.S3) },
                Text("EQuanticNativeApp", TypeRole.Title, theme.TextPrimary, maxLines: 1)),
            .. Destinations.Select((item, index) => (VisualNode)new ListItem(item.Label)
            {
                Leading = Icon(item.Icon, IconSize.Md, index == _destination
                    ? theme.Colors(Variant.Primary).OnSubtle
                    : theme.TextSecondary),
                // Not just a tint: Selected is what makes the row announce itself as the page you
                // are ON, to a screen reader that cannot see the tint.
                Selected = index == _destination,
                OnPressed = () => Go(index),
            }),
        ]));

        VisualNode Content(bool withMenuButton) =>
            new Column(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill }
                .With(new AppBar(Destinations[_destination].Label)
                {
                    // Docked, the bar has nothing to open — an affordance that opens what is
                    // already open is exactly how an adaptive shell reads as broken.
                    Leading = withMenuButton
                        ? IconButton(Icons.Menu, "Open navigation",
                            onPressed: () => SetState(() => _open = true))
                        : null,
                })
                .With(Flexible(Screen()));

        // Compact: the content, with the panel as a LAYER over it. The Stack IS the layering.
        var phone = Stack(children: [
            SafeArea(Content(withMenuButton: true), SafeEdges.Top),
            Overlay(Drawer(Menu(), _open, () => SetState(() => _open = false))),
        ]);

        // Expanded: no panel, no scrim, no open state — the list is part of the layout.
        var docked = new Row(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill }
            .With(SafeArea(Menu(), SafeEdges.Start | SafeEdges.Top | SafeEdges.Bottom))
            .With(Divider(axis: DividerAxis.Vertical))
            .With(Flexible(SafeArea(Content(withMenuButton: false), SafeEdges.Top)));

        return Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
        },
        AdaptiveNode(phone, medium: null, expanded: docked));
    }

    private void Go(int index) => SetState(() =>
    {
        _destination = index;
        _open = false;
    });
}
