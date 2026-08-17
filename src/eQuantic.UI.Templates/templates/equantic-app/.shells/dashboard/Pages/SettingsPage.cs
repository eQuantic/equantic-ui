using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;

namespace EQuanticApp.Pages;

/// <summary>The third route — state, a switch and a field, none of which needed a line of
/// JavaScript. The switch announces itself to a screen reader with no work here.</summary>
[Page("/settings", Title = "Settings")]
public sealed class SettingsPage : StatefulComponent
{
    private bool _notifications = true;
    private string _name = "";

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        return AppShell("/settings", Column(gap: Space.S4, children: [
            Text("Settings", TypeRole.Heading, theme.TextPrimary, maxLines: 1),
            new TextInput(_name, value => SetState(() => _name = value), "Display name"),
            Card(Column(gap: 0, children: [
                new ListItem("Notifications")
                {
                    Trailing = Switch(_notifications,
                        () => SetState(() => _notifications = !_notifications)),
                },
            ])),
            Text(_name.Length == 0 ? "Type a name above." : $"Hello, {_name}.",
                TypeRole.Caption, theme.TextMuted, maxLines: 1),
        ]));
    }
}
