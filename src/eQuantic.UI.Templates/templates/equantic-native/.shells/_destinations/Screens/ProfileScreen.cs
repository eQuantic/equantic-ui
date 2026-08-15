namespace EQuanticNativeApp.Screens;

/// <summary>The third destination — a settings list, the shape every app grows. Each row is a
/// catalog component, so the switches announce themselves to a screen reader with no work here.</summary>
public sealed class ProfileScreen : StatefulComponent
{
    private bool _notifications = true;
    private bool _analytics;

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        return ScrollView(Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S4),
        },
        Column(gap: Space.S4, children: [
            Row(gap: Space.S3, children: [
                new Avatar("EQ", SizeVariant.Large, "Your account"),
                Flexible(Column(gap: 1, children: [
                    Text("Your account", TypeRole.BodyM, theme.TextPrimary, maxLines: 1),
                    Text("Signed in on this device", TypeRole.Caption, theme.TextMuted, maxLines: 1),
                ])),
            ]),
            Card(Column(gap: 0, children: [
                new ListItem("Notifications")
                {
                    Trailing = Switch(_notifications, () => SetState(() => _notifications = !_notifications)),
                },
                Divider(DividerInset.Leading),
                new ListItem("Share analytics")
                {
                    Trailing = Switch(_analytics, () => SetState(() => _analytics = !_analytics)),
                },
            ])),
            Button("Sign out", Variant.Outline),
        ])));
    }
}
