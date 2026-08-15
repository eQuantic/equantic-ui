using EQuanticNativeApp.Resources;

namespace EQuanticNativeApp.Screens;

/// <summary>
/// The first destination, and the write-once claim in one file: this component holds state, reads
/// a resx string in the DEVICE's language, and renders unchanged in a browser.
/// </summary>
public sealed class ActivityScreen : StatefulComponent
{
    private int _count;

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        return ScrollView(Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S4),
        },
        Column(gap: Space.S4, children: [
            Card(Box(new BoxStyle { Padding = EdgeInsets.All(Space.S4) },
                Column(gap: Space.S2, children: [
                    Text(string.Format(Strings.PressedTimes, _count), TypeRole.Title,
                        theme.TextPrimary, maxLines: 1),
                    Text("This screen keeps its own state. Switching destination and coming back "
                        + "rebuilds it — hoist the field into AppShell to outlive the trip.",
                        TypeRole.BodyM, theme.TextSecondary, maxLines: 3),
                ]))),
            Button(Strings.CountUp, onPressed: () => SetState(() => _count++)),
        ])));
    }
}
