using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The design system's Tabs (spec B5), CONTROLLED: 48dp row, Fixed mode = 2–4 equal-width tabs;
/// active = Primary 700, inactive = TextMuted 600, pressed = SurfaceSubtle on the cell; the
/// indicator is a 3dp Primary rrect (top corners rounded) inset 16 under the active tab. v1
/// fences: Scrollable mode (edge-fade gradient), the indicator TRANSLATION between tabs and panel
/// swipes join the animation/gesture systems; the inline count Badge composes externally.
/// </summary>
public sealed class Tabs : StatelessComponent
{
    public Tabs(IReadOnlyList<string> labels, int selected, Action<int>? onSelect = null)
    {
        Labels = labels;
        Selected = selected;
        OnSelect = onSelect;
    }

    public IReadOnlyList<string> Labels { get; init; }
    public int Selected { get; init; }
    public Action<int>? OnSelect { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var primary = theme.Colors(Variant.Primary);
        // The theme's Navigation slot, then the component's own (§3.10).
        var slot = theme.Navigation;
        var indicator = slot?.IndicatorThickness ?? 3;

        var row = new Row(gap: 0) { Width = SizeValue.Fill, Height = slot?.TabHeight ?? 48 };
        for (var i = 0; i < Labels.Count; i++)
        {
            var isActive = i == Selected;
            var index = i;

            var label = new Text(Labels[i], TypeRole.Caption,
                isActive ? primary.Base : slot?.InactiveColor ?? theme.TextMuted, maxLines: 1)
            {
                StyleOverride = new TypeStyle(slot?.TabLabelSize ?? 14, slot?.TabLabelLineHeight ?? 18,
                    isActive ? slot?.TabSelectedLabelWeight ?? FontWeight.Bold : slot?.TabLabelWeight ?? FontWeight.SemiBold,
                    slot?.TabLabelTracking ?? 0.1f, 1.3f),
            };
            var labelRow = new Row(gap: 0) { Main = MainAlign.Center, Height = SizeValue.Fill };
            labelRow.Add(label);

            // Cell = label centered + the 3dp indicator (inset 16, top corners rounded) at the bottom.
            var cell = new Column(gap: 0) { Height = SizeValue.Fill };
            cell.Add(new Flexible(labelRow));
            cell.Add(new Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Height = indicator,
                Padding = EdgeInsets.Symmetric(slot?.IndicatorInset ?? Space.S4, 0),
            }, isActive
                ? new Box(new BoxStyle
                {
                    Width = SizeValue.Fill,
                    Height = indicator,
                    Background = primary.Base,
                    CornerRadius = new CornerRadii(slot?.IndicatorRadius ?? 2, slot?.IndicatorRadius ?? 2, 0, 0),
                })
                : null));

            // The handoff's "Pressed: SurfaceSubtle fill on the tab cell": the CELL is the surface the
            // press fills, so the column sits in a box that is the whole cell and carries the fill.
            // Without the box the press reached the first box drawn, the 3dp track under the label.
            var surface = new Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Height = SizeValue.Fill,
                Pressed = new StyleDiff { Background = slot?.QuietFill ?? theme.SurfaceSubtle },
            }, cell);

            row.Add(new Flexible(new Pressable(surface, OnSelect is null ? null : () => OnSelect(index))
            {
                Label = Labels[i],
                // The underline is paint; the attribute is the answer. Roving under the Tablist
                // wrapper below, exactly like the RadioGroup's items — but a tab is PICKED, so
                // the web speaks aria-selected.
                Role = PressableRole.Tab,
                Selected = isActive,
            }));
        }

        // ONE Tab stop for the strip, arrows move the selection and WRAP at the ends — the ARIA
        // tablist pattern, same family as the SegmentedControl's radio-group twin.
        if (OnSelect is null || Labels.Count == 0) return row;
        var count = Labels.Count;
        return new Adjustable(row, direction => OnSelect((Selected + direction + count) % count))
        {
            Role = AdjustableRole.Tablist,
        };
    }
}
