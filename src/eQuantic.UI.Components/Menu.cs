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
    /// <summary>Backed by a field, so <see cref="AdoptConfig"/> can carry it: this instance is kept
    /// across the parent's rebuilds, and an <c>init</c> accessor cannot be written from there.</summary>
    public AnchorPlacement Placement { get => _placement; init => _placement = value; }
    private AnchorPlacement _placement = AnchorPlacement.BottomStart;

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not Menu fresh) return;
        Trigger = fresh.Trigger;
        Items = fresh.Items;
        OnSelect = fresh.OnSelect;
        _placement = fresh.Placement;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var panel = MenuPanel.Build(context, Items, _open, _highlight, Choose);

        VisualNode menu = new Anchored(new Pressable(Trigger, Toggle) { Expanded = _open }, panel)
        {
            Placement = Placement,
            Open = _open,
            OnDismiss = () => SetState(() => _open = false),
            // The full ARIA menu pattern: role=menu + numbered menuitem rows on the panel,
            // haspopup/controls on the trigger, and the keyboard highlight as
            // aria-activedescendant — stated, not merely painted.
            PanelRole = AnchorPanelRole.Menu,
            ActiveIndex = _open ? _highlight : -1,
        };

        return MenuPanel.WithKeys(menu, _open && Items.Count > 0,
            () => SetState(() => _highlight = MenuPanel.Step(Items, _highlight, +1)),
            () => SetState(() => _highlight = MenuPanel.Step(Items, _highlight, -1)),
            () => Choose(_highlight));
    }

    private void Toggle() => SetState(() =>
    {
        _open = !_open;
        if (_open) _highlight = MenuPanel.FirstEnabled(Items);
    });

    private void Choose(int index)
    {
        if (index < 0 || index >= Items.Count || Items[index].Disabled) return;
        OnSelect?.Invoke(index);
        SetState(() => _open = false);
    }
}
