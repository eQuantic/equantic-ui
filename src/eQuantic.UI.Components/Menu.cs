using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>One row of a <see cref="Menu"/>: label, optional leading glyph, Destructive tone for
/// dangerous actions, Disabled renders at the theme's disabled opacity and ignores taps.</summary>
public sealed record MenuItem(string Label)
{
    public Icons? Icon { get; init; }
    public bool Destructive { get; init; }
    public bool Disabled { get; init; }
}

/// <summary>
/// The design system's dropdown Menu (wave 3): a trigger the MENU makes pressable (the child owns
/// all visuals — the Pressable/Link contract) and an anchored E2 Surface panel of item rows.
/// Open/close is INTERNAL state: tap toggles, outside tap dismisses (the Anchored scrim), selecting
/// fires <see cref="OnSelect"/> with the item index and closes. Item rows are 40dp, hover
/// SurfaceSubtle (S5 diff — zero JS on web), Destructive items speak the Destructive text color.
/// v1 fences: keyboard navigation/typeahead (a11y system), submenus, dividers/sections.
/// </summary>
public sealed class Menu : StatefulComponent
{
    private bool _open;

    public Menu(VisualNode trigger, IReadOnlyList<MenuItem> items, Action<int>? onSelect = null)
    {
        Trigger = trigger;
        Items = items;
        OnSelect = onSelect;
    }

    public VisualNode Trigger { get; private set; }
    public IReadOnlyList<MenuItem> Items { get; private set; }
    public Action<int>? OnSelect { get; private set; }
    public AnchorPlacement Placement { get; init; } = AnchorPlacement.BottomStart;

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not Menu fresh) return;
        Trigger = fresh.Trigger;
        Items = fresh.Items;
        OnSelect = fresh.OnSelect;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        var list = new Column(gap: 0) { Width = SizeValue.Fill };
        for (var i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            var index = i;

            var row = new Row(gap: Space.S2)
            {
                Cross = CrossAlign.Center,
                Width = SizeValue.Fill,
                Height = SizeValue.Fill,
            };
            if (item.Icon is { } glyph)
                row.Add(new Icon(glyph, IconSize.Dense,
                    item.Destructive ? theme.Colors(Variant.Destructive).Base : theme.TextSecondary));
            row.Add(new Text(item.Label, TypeRole.BodyM,
                item.Destructive ? theme.Colors(Variant.Destructive).Base : theme.TextPrimary));

            var surface = new Box(new BoxStyle
            {
                Height = 40,
                Padding = EdgeInsets.Symmetric(Space.S3, 0),
                Width = SizeValue.Fill,
                Opacity = item.Disabled ? theme.DisabledOpacity : null,
                Hover = item.Disabled ? null : new StyleDiff { Background = theme.SurfaceSubtle },
            }, row);

            list.Add(item.Disabled
                ? surface
                : new Pressable(surface, () =>
                {
                    OnSelect?.Invoke(index);
                    SetState(() => _open = false);
                }));
        }

        var panel = new Box(new BoxStyle
        {
            MinWidth = 180,
            Background = theme.Surface,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
            BorderWidth = 1,
            BorderColor = theme.Border,
            Elevation = 2,
            Padding = EdgeInsets.Symmetric(0, Space.S1),
            Clip = true,
        }, list);

        return new Anchored(new Pressable(Trigger, () => SetState(() => _open = !_open)), panel)
        {
            Placement = Placement,
            Open = _open,
            OnDismiss = () => SetState(() => _open = false),
        };
    }
}
