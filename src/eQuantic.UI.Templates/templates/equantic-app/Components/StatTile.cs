using eQuantic.UI.Primitives;

namespace EQuanticApp.Components;

/// <summary>
/// Your first component. Nothing here opts into anything: because it is a component, the build
/// generates a factory named after it, so the page below composes it exactly the way it composes
/// the framework's own — <c>StatTile("Count", "3")</c>, no <c>new</c>, no import.
/// <para>
/// The factory mirrors the WIDEST constructor. If a component has several and the widest is not
/// the one you want offered, mark the one you do with <c>[UiFactory]</c>.
/// </para>
/// </summary>
public sealed class StatTile : StatelessComponent
{
    public StatTile(string label, string value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; init; }
    public string Value { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        return Box(new BoxStyle
        {
            Padding = EdgeInsets.All(Space.S4),
            Background = theme.Surface,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
        },
        Column(gap: Space.S1, children: [
            Text(Label, TypeRole.Caption, theme.TextMuted, maxLines: 1),
            Text(Value, TypeRole.Title, theme.TextPrimary, maxLines: 1),
        ]));
    }
}
