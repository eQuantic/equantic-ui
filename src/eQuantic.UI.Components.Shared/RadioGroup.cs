using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components.Shared;

/// <summary>
/// The design system's RadioGroup (spec B13), CONTROLLED: a group always has exactly one value —
/// selection moves, never clears. Radio = 22dp circle: 2dp BorderStrong; selected = 2dp Primary
/// ring + 10dp Primary dot (two rrect draws). Each row is a full-width target, min 44 tall,
/// gap 12. v1 fence: the dot scale motion joins the animation system.
/// </summary>
public sealed class RadioGroup : StatelessComponent
{
    public RadioGroup(IReadOnlyList<string> options, int selected, Action<int>? onChanged = null,
        string? label = null)
    {
        Options = options;
        Selected = selected;
        OnChanged = onChanged;
        Label = label;
    }

    public IReadOnlyList<string> Options { get; init; }
    public int Selected { get; init; }
    public Action<int>? OnChanged { get; init; }

    /// <summary>Group label (Caption, TextSecondary) rendered above the options.</summary>
    public string? Label { get; init; }

    public bool Disabled { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var primary = theme.Colors(Variant.Primary);

        var column = new Column(gap: Space.S1) { Width = SizeValue.Fill };
        if (Label is { } groupLabel)
            column.Add(new Text(groupLabel, TypeRole.Caption, theme.TextMuted, maxLines: 1));

        for (var i = 0; i < Options.Count; i++)
        {
            var isSelected = i == Selected;
            var index = i;

            var circleContent = new Row(gap: 0) { Main = MainAlign.Center, Height = SizeValue.Fill };
            if (isSelected)
            {
                circleContent.Add(new Box(new BoxStyle
                {
                    Width = 10,
                    Height = 10,
                    Background = primary.Base,
                    CornerRadius = new CornerRadii(Radius.Full),
                }));
            }

            var circle = new Box(new BoxStyle
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadii(Radius.Full),
                BorderWidth = 2f,
                BorderColor = isSelected ? primary.Base : theme.BorderStrong,
            }, circleContent);

            var row = new Row(gap: Space.S3) { Cross = CrossAlign.Center, Width = SizeValue.Fill, Height = 44 };
            row.Add(circle);
            row.Add(new Text(Options[i], TypeRole.BodyM,
                Disabled ? theme.TextMuted : theme.TextPrimary, maxLines: 1));

            column.Add(new Pressable(row, Disabled || OnChanged is null ? null : () => OnChanged(index))
            {
                Disabled = Disabled,
                Label = Options[i],
            });
        }

        return column;
    }
}
