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
/// <para>
/// KEYBOARD: while open, arrows move a highlight over the ENABLED items, Enter runs the one under
/// it, Escape closes — the same Shortcut-while-open pattern the Select uses, working on both
/// targets because each host's chord pipeline already does.
/// </para>
/// v1 fences: typeahead, submenus, dividers/sections.
/// </summary>
public sealed class Menu : StatefulComponent
{
    private bool _open;

    /// <summary>Where the keyboard is. Arrows SKIP disabled items — a highlight that lands on a
    /// row Enter refuses to run is a lie about what Enter does.</summary>
    private int _highlight;

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
                Height = Sizing.Height(SizeVariant.Medium),
                Padding = EdgeInsets.Symmetric(Space.S3, 0),
                Width = SizeValue.Fill,
                Opacity = item.Disabled ? theme.DisabledOpacity : null,
                Background = _open && index == _highlight && !item.Disabled ? theme.SurfaceSubtle : null,
                Hover = item.Disabled ? null : new StyleDiff { Background = theme.SurfaceSubtle },
            }, row);

            list.Add(item.Disabled
                ? surface
                : new Pressable(surface, () => Choose(index)));
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

        VisualNode menu = new Anchored(new Pressable(Trigger, Toggle), panel)
        {
            Placement = Placement,
            Open = _open,
            OnDismiss = () => SetState(() => _open = false),
        };

        if (_open && Items.Count > 0)
        {
            menu = new Shortcut(menu, KeyChord.ArrowDown, () => SetState(() => _highlight = Step(+1)));
            menu = new Shortcut(menu, KeyChord.ArrowUp, () => SetState(() => _highlight = Step(-1)));
            menu = new Shortcut(menu, KeyChord.Enter, () => Choose(_highlight));
        }
        return menu;
    }

    private void Toggle() => SetState(() =>
    {
        _open = !_open;
        if (_open) _highlight = FirstEnabled();
    });

    private void Choose(int index)
    {
        if (index < 0 || index >= Items.Count || Items[index].Disabled) return;
        OnSelect?.Invoke(index);
        SetState(() => _open = false);
    }

    private int FirstEnabled()
    {
        for (var i = 0; i < Items.Count; i++)
            if (!Items[i].Disabled) return i;
        return 0;
    }

    /// <summary>The next enabled item in <paramref name="direction"/>, stopping at the edge — a
    /// menu that wraps turns a held arrow key into a roulette.</summary>
    private int Step(int direction)
    {
        for (var i = _highlight + direction; i >= 0 && i < Items.Count; i += direction)
            if (!Items[i].Disabled) return i;
        return _highlight;
    }
}
