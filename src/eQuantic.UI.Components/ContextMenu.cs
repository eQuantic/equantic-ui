using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// A menu of what its child is, opened when a person asks for it (#1531): a secondary click, a finger held still,
/// or the menu key or Shift+F10 with the focus inside. It opens where the pointer asked, or along the child's
/// bottom edge when the keyboard did, and then is the <see cref="Menu"/>'s own panel: the arrows move over the
/// enabled items, Enter or a press runs one through <see cref="OnSelect"/>, Escape or a press outside closes it.
/// <para>
/// A composition of public blocks, a <see cref="ContextTarget"/> under an <see cref="Anchored"/> placed
/// <see cref="Anchored.At"/> the request's point, so a menu with another look is the same composition with
/// another panel.
/// </para>
/// </summary>
public sealed class ContextMenu : StatefulComponent
{
    private bool _open;
    private int _highlight;
    private Point? _at;

    public ContextMenu(VisualNode child, IReadOnlyList<MenuItem> items, Action<int>? onSelect = null)
    {
        Child = child;
        Items = items;
        OnSelect = onSelect;
    }

    /// <summary>What the menu is of.</summary>
    public VisualNode Child { get; private set; }

    public IReadOnlyList<MenuItem> Items { get; private set; }

    /// <summary>The index of the item chosen; the menu closes after it.</summary>
    public Action<int>? OnSelect { get; private set; }

    /// <summary>Whether a request opens the menu. Backed by a field, so <see cref="AdoptConfig"/> can carry it.</summary>
    public bool Enabled { get => _enabled; init => _enabled = value; }
    private bool _enabled = true;

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not ContextMenu fresh) return;
        Child = fresh.Child;
        Items = fresh.Items;
        OnSelect = fresh.OnSelect;
        _enabled = fresh.Enabled;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var panel = MenuPanel.Build(context, Items, _open, _highlight, Choose);
        VisualNode menu = new Anchored(new ContextTarget(Child, Request) { Enabled = _enabled && Items.Count > 0 }, panel)
        {
            At = _at,
            Placement = AnchorPlacement.BottomStart,
            Open = _open,
            OnDismiss = () => SetState(() => _open = false),
            PanelRole = AnchorPanelRole.Menu,
            ActiveIndex = _open ? _highlight : -1,
        };
        return MenuPanel.WithKeys(menu, _open && Items.Count > 0,
            () => SetState(() => _highlight = MenuPanel.Step(Items, _highlight, +1)),
            () => SetState(() => _highlight = MenuPanel.Step(Items, _highlight, -1)),
            () => Choose(_highlight));
    }

    private void Request(ContextRequest request) => SetState(() =>
    {
        _at = request.At;
        _open = true;
        _highlight = MenuPanel.FirstEnabled(Items);
    });

    private void Choose(int index)
    {
        if (index < 0 || index >= Items.Count || Items[index].Disabled) return;
        OnSelect?.Invoke(index);
        SetState(() => _open = false);
    }
}
