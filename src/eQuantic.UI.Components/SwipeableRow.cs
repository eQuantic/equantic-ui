using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// A list row that reveals ONE action when swiped towards the end (spec B21). One, deliberately: a
/// hidden action is already hard to find, and a drawer of three is a menu nobody can aim at — if
/// there are several, the row belongs in a long-press menu instead.
/// <para>
/// The action is a REAL button behind the row, not a painted hint, so it is reachable by keyboard
/// and by assistive tech even when nobody ever swipes. That is what makes the gesture an
/// accelerator rather than the only way in.
/// </para>
/// <para>
/// CONTROLLED, like the gesture it sits on: <see cref="Open"/> is the caller's state, and
/// <see cref="OnOpenChanged"/> fires when a release crosses half the action's width. A list that
/// allows one open row at a time is a caller that closes the others — the row does not police it.
/// </para>
/// </summary>
public sealed class SwipeableRow : StatelessComponent
{
    /// <summary>Width of the revealed action — a comfortable thumb target, not a whole screen.</summary>
    public const float ActionWidth = 96;

    public SwipeableRow(VisualNode child, string actionLabel, Icons actionIcon,
        Action? onAction = null)
    {
        Child = child;
        ActionLabel = actionLabel;
        ActionIcon = actionIcon;
        OnAction = onAction;
    }

    public VisualNode Child { get; init; }
    public string ActionLabel { get; init; }
    public Icons ActionIcon { get; init; }
    public Action? OnAction { get; init; }

    public Variant ActionVariant { get; init; } = Variant.Destructive;

    public bool Open { get; init; }
    public Action<bool>? OnOpenChanged { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var colors = theme.Colors(ActionVariant);

        var actionContent = new Column(gap: 3)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Main = MainAlign.Center,
            Cross = CrossAlign.Center,
        };
        actionContent.Add(new Icon(ActionIcon, IconSize.Dense, colors.OnBase));
        actionContent.Add(new Text(ActionLabel, TypeRole.Caption, colors.OnBase, maxLines: 1));

        var action = new Pressable(new Box(new BoxStyle
        {
            Width = ActionWidth,
            Height = SizeValue.Fill,
            Background = colors.Base,
        }, actionContent), OnAction)
        {
            Label = ActionLabel,
        };

        var surface = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Background = theme.Surface,
        }, Child);

        // The action sits BEHIND, pinned to the end; the row slides over it.
        var revealed = new Stack();
        revealed.Add(new Positioned(action, top: 0, bottom: 0, end: 0));
        revealed.Add(new Draggable(surface, OnReleased)
        {
            Axis = DragAxis.Horizontal,
            Min = -ActionWidth,
            Max = 0,
            RestOffset = Open ? -ActionWidth : 0,
        });

        return new Box(new BoxStyle { Width = SizeValue.Fill, Clip = true }, revealed);
    }

    /// <summary>Past half the action's width the row stays open; short of it it springs back.</summary>
    private void OnReleased(float offset) =>
        OnOpenChanged?.Invoke(offset <= -ActionWidth / 2);
}
