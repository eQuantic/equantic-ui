using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using EQuanticNativeApp.Screens;

namespace EQuanticNativeApp;

/// <summary>
/// The list-detail shell — the shape a mail app, a settings tree and a file browser all have, and
/// the one where a phone and a tablet genuinely disagree about what should be on screen.
/// <para>
/// One piece of state answers both: WHICH item is chosen. On a phone that is a place — nothing
/// chosen means the list, something chosen means the detail, and back un-chooses. Past 840dp it is
/// only a highlight, because both panes are already visible and there is nowhere to navigate to.
/// </para>
/// <para>
/// The <c>ListDetail</c> component owns that rule, so this file is the DATA and nothing else: no
/// width test, no listener, no second layout to keep in step. The panes go in as BUILDERS because
/// each shape builds its own, and <c>_selected</c> lives here because it is the one thing that has
/// to survive a rotation. Grow it by replacing <see cref="Inbox.Items"/> with your own source — a
/// <c>[ServerAction]</c> on the web, your own service on the device.
/// </para>
/// </summary>
public sealed class AppShell : StatefulComponent
{
    private int? _selected;

    public override VisualNode Build(ComponentContext context) =>
        Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = context.Theme.Background,
        },
        SafeArea(new ListDetail(
            list: () => Inbox.List(_selected, index => SetState(() => _selected = index)),
            detail: _selected is { } chosen ? () => Inbox.Detail(context.Theme, chosen) : null,
            onBack: () => SetState(() => _selected = null))
        {
            ListTitle = "Inbox",
            Title = _selected is { } open ? Inbox.Items[open].Title : null,
            Placeholder = EmptyState(Icon(Icons.Mail), "Pick a message",
                "Choose one on the left and it opens here."),
        }, SafeEdges.Top));
}
