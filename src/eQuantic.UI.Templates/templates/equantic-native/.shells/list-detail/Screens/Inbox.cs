using eQuantic.UI.Components;
using static eQuantic.UI.Components.UI;
using eQuantic.UI.Primitives;

namespace EQuanticNativeApp.Screens;

/// <summary>The two panes, and the data behind them. Replace <see cref="Items"/> with your own
/// source — a [ServerAction] on the web, your own service on the device.</summary>
public static class Inbox
{
    public sealed record Message(string Title, string From, string Body);

    public static readonly Message[] Items =
    [
        new("Welcome to eQuantic.UI", "The team",
            "This screen is one component. It renders in a window through the Photon GPU engine "
            + "and in a browser as HTML, from the same source — no second implementation, no "
            + "webview."),
        new("Adaptive by default", "The team",
            "Resize the window past 840dp and the list and the detail sit side by side. Nothing "
            + "here listens for a resize: AdaptiveNode carries both trees and the target picks."),
        new("Where to go next", "The team",
            "Add a [Page] to serve this on the web, or dotnet new equantic-app for the web half. "
            + "Both halves read the same components."),
    ];

    /// <summary>The list pane. <c>Selected</c> lights the chosen row AND tells a screen reader
    /// which one it is — on a wide window the highlight is the only thing saying so.</summary>
    public static VisualNode List(int? selected, Action<int> onChoose) =>
        ScrollView(Column(gap: 0, children: [
            .. Items.Select((message, index) => (VisualNode)new ListItem(message.Title, message.From)
            {
                Selected = index == selected,
                OnPressed = () => onChoose(index),
            }),
        ]));

    /// <summary>The detail pane — the same node on both shapes, because a phone and a tablet
    /// disagree about the LAYOUT, never about what a message is.</summary>
    public static VisualNode Detail(IAppTheme theme, int index)
    {
        var message = Items[index];

        return ScrollView(Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S5),
        },
        Column(gap: Space.S3, children: [
            Text(message.Title, TypeRole.Heading, theme.TextPrimary, maxLines: 2),
            Text(message.From, TypeRole.Caption, theme.TextMuted, maxLines: 1),
            Divider(),
            Text(message.Body, TypeRole.BodyM, theme.TextSecondary, maxLines: 12),
        ])));
    }
}
