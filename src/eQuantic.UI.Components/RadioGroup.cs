using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The design system's RadioGroup (spec B13), CONTROLLED: a group always has exactly one value —
/// selection moves, never clears. Radio = 22dp circle: 2dp BorderStrong; selected = 2dp Primary
/// ring + 10dp Primary dot (two rrect draws). Each row is a full-width target, min 44 tall,
/// gap 12. ONE Tab stop for the whole group — the <see cref="Adjustable"/> wrapper — and the
/// arrows walk the choice with wrap-around, exactly the native <c>&lt;input type=radio&gt;</c>
/// contract; each row is a <c>role="radio"</c> with its checked-ness stated, out of the tab
/// order, still pressable by pointer. v1 fences: the dot scale motion joins the animation
/// system; <c>aria-activedescendant</c> joins the shared id machinery.
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

        var options = new Column(gap: Space.S1) { Width = SizeValue.Fill };
        for (var i = 0; i < Options.Count; i++)
        {
            var isSelected = i == Selected;
            var index = i;

            var circleContent = isSelected
                ? new Box(new BoxStyle
                {
                    Width = Sizing.RadioDot(context.Density),
                    Height = Sizing.RadioDot(context.Density),
                    Background = primary.Base,
                    CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
                }).Centered()
                : null;

            var circle = new Box(new BoxStyle
            {
                Width = Sizing.SelectionBox(context.Density),
                Height = Sizing.SelectionBox(context.Density),
                CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
                BorderWidth = 2f,
                BorderColor = isSelected ? primary.Base : theme.BorderStrong,
            }, circleContent);

            var row = new Row(gap: Space.S3) { Cross = CrossAlign.Center, Width = SizeValue.Fill, Height = 44 };
            row.Add(circle);
            row.Add(new Text(Options[i], TypeRole.BodyM,
                Disabled ? theme.TextMuted : theme.TextPrimary, maxLines: 1));

            // The selected row presses to nothing, exactly as a native radio does — and each row
            // states what it is: a radio, checked or not, outside the tab order (the group is
            // the Tab stop).
            options.Add(new Pressable(row,
                Disabled || OnChanged is null || isSelected ? null : () => OnChanged(index))
            {
                Disabled = Disabled,
                Label = Options[i],
                Role = PressableRole.Radio,
                Selected = isSelected,
            });
        }

        // The group is ONE Tab stop and the arrows move the choice, wrapping at the ends — the
        // native radio group contract, and the same mechanism the SegmentedControl rides.
        VisualNode group = options;
        if (!Disabled && OnChanged is not null && Options.Count > 0)
        {
            var count = Options.Count;
            group = new Adjustable(options,
                direction => OnChanged((Selected + direction + count) % count))
            {
                Role = AdjustableRole.Radiogroup,
                Label = Label,
            };
        }

        var column = new Column(gap: Space.S1) { Width = SizeValue.Fill };
        if (Label is { } groupLabel)
            column.Add(new Text(groupLabel, TypeRole.Caption, theme.TextMuted, maxLines: 1));
        column.Add(group);
        return column;
    }
}
