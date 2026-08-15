using EQuanticNativeApp.Screens;

namespace EQuanticNativeApp;

/// <summary>
/// The list-detail shell — the shape a mail app, a settings tree and a file browser all have, and
/// the one where a phone and a tablet genuinely disagree about what should be on screen.
/// <para>
/// One piece of state answers both: WHICH item is chosen. On a phone that is a place — nothing
/// chosen means the list, something chosen means the detail, and the back button un-chooses. Past
/// 840dp it is only a highlight, because both panes are already visible and there is nowhere to
/// navigate to. Same field, same handler, two readings of it.
/// </para>
/// </summary>
public sealed class AppShell : StatefulComponent
{
    private int? _selected;

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        // Compact: one pane at a time. The list IS the screen until something is chosen.
        var phone = _selected is { } chosen
            ? new Column(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill }
                .With(new AppBar(Inbox.Items[chosen].Title)
                {
                    Leading = IconButton(Icons.ChevronLeft, "Back",
                        onPressed: () => SetState(() => _selected = null)),
                })
                .With(Flexible(Inbox.Detail(theme, chosen)))
            : new Column(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill }
                .With(AppBar("Inbox"))
                .With(Flexible(Inbox.List(_selected, Choose)));

        // Expanded: both panes, and the chosen row stays lit while its detail is read. Nothing is
        // chosen on a phone until the user picks; here the first item is, because an empty right
        // pane on a wide screen is a design that starts by showing nothing.
        var wide = new Row(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill }
            .With(Box(new BoxStyle { Width = 360, Height = SizeValue.Fill },
                new Column(gap: 0) { Height = SizeValue.Fill }
                    .With(AppBar("Inbox"))
                    .With(Flexible(Inbox.List(_selected ?? 0, Choose)))))
            .With(Divider(axis: DividerAxis.Vertical))
            .With(Flexible(new Column(gap: 0) { Height = SizeValue.Fill }
                .With(AppBar(Inbox.Items[_selected ?? 0].Title))
                .With(Flexible(Inbox.Detail(theme, _selected ?? 0)))));

        return Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
        },
        SafeArea(AdaptiveNode(phone, medium: null, expanded: wide), SafeEdges.Top));
    }

    private void Choose(int index) => SetState(() => _selected = index);
}
