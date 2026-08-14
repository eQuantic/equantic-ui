using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The design system's Select (wave 3): a 40dp Radius.Md field (1dp BorderStrong on Surface,
/// chevron trailing) whose anchored option panel MATCHES THE FIELD WIDTH (the Anchored
/// MatchAnchorWidth contract). Value/options are CONTROLLED (<see cref="OnChanged"/> fires the
/// index); open/close is internal state like TextInput's focus. The selected option row speaks
/// Primary.Subtle with a trailing check.
/// <para>
/// KEYBOARD: while open, arrows move a highlight, Enter chooses it, Escape closes (the Anchored's
/// own binding) — declared as ordinary <see cref="Shortcut"/> nodes around the open panel, which
/// is what makes the same lines work on both targets: each host's key pipeline already dispatches
/// chords, and "on screen is the subscription" scopes them to exactly while the panel is up.
/// </para>
/// v1 fences: typeahead, option groups, multi-select, disabled options.
/// </summary>
public sealed class Select : StatefulComponent
{
    private bool _open;

    /// <summary>The option the KEYBOARD is on — where Enter lands. Separate from SelectedIndex,
    /// which is the app's committed value: arrowing around commits nothing until Enter.</summary>
    private int _highlight;

    public Select(IReadOnlyList<string> options, int selectedIndex = -1,
        Action<int>? onChanged = null, string? placeholder = null)
    {
        Options = options;
        SelectedIndex = selectedIndex;
        OnChanged = onChanged;
        Placeholder = placeholder;
    }

    public IReadOnlyList<string> Options { get; private set; }
    public int SelectedIndex { get; private set; }
    public Action<int>? OnChanged { get; private set; }
    public string? Placeholder { get; private set; }
    public bool Disabled { get; init; }

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not Select fresh) return;
        Options = fresh.Options;
        SelectedIndex = fresh.SelectedIndex;
        OnChanged = fresh.OnChanged;
        Placeholder = fresh.Placeholder;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var hasValue = SelectedIndex >= 0 && SelectedIndex < Options.Count;

        var fieldRow = new Row(gap: Space.S2) { Cross = CrossAlign.Center, Width = SizeValue.Fill, Height = SizeValue.Fill };
        fieldRow.Add(new Text(hasValue ? Options[SelectedIndex] : Placeholder ?? "Select…",
            TypeRole.BodyM, hasValue ? theme.TextPrimary : theme.TextMuted, maxLines: 1));
        fieldRow.Add(new Flexible(new Spacer()));
        fieldRow.Add(new Icon(Icons.ChevronDown, IconSize.Sm, theme.TextSecondary));

        var field = new Box(new BoxStyle
        {
            Height = Sizing.Height(SizeVariant.Medium, context.Density),
            Width = SizeValue.Fill,
            Padding = EdgeInsets.Symmetric(Space.S3, 0),
            Background = theme.Surface,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
            BorderWidth = 1,
            BorderColor = theme.BorderStrong,
            Opacity = Disabled ? theme.DisabledOpacity : null,
            Hover = Disabled ? null : new StyleDiff { BorderColor = theme.Colors(Variant.Primary).Base },
        }, fieldRow);

        var list = new Column(gap: 0) { Width = SizeValue.Fill };
        for (var i = 0; i < Options.Count; i++)
        {
            var index = i;
            var selected = i == SelectedIndex;

            var row = new Row(gap: Space.S2) { Cross = CrossAlign.Center, Width = SizeValue.Fill, Height = SizeValue.Fill };
            row.Add(new Text(Options[i], TypeRole.BodyM,
                selected ? theme.Colors(Variant.Primary).OnSubtle : theme.TextPrimary, maxLines: 1));
            if (selected)
            {
                row.Add(new Flexible(new Spacer()));
                row.Add(new Icon(Icons.Check, IconSize.Sm, theme.Colors(Variant.Primary).OnSubtle));
            }

            var highlighted = _open && i == _highlight;
            list.Add(new Pressable(new Box(new BoxStyle
            {
                Height = Sizing.Height(SizeVariant.Medium, context.Density),
                Padding = EdgeInsets.Symmetric(Space.S3, 0),
                Width = SizeValue.Fill,
                // The keyboard's row wears the hover coat: one visual language for "you are here",
                // whichever hand is steering.
                Background = selected ? theme.Colors(Variant.Primary).Subtle
                    : highlighted ? theme.SurfaceSubtle : null,
                Hover = selected ? null : new StyleDiff { Background = theme.SurfaceSubtle },
            }, row), () => Choose(index)));
        }

        var panel = new Box(new BoxStyle
        {
            Background = theme.Surface,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
            BorderWidth = 1,
            BorderColor = theme.Border,
            Elevation = 2,
            Padding = EdgeInsets.Symmetric(0, Space.S1),
            Clip = true,
        }, list);

        var trigger = Disabled
            ? field
            : (VisualNode)new Pressable(field, Toggle) { Expanded = _open };

        VisualNode select = new Anchored(trigger, panel)
        {
            Open = _open && !Disabled,
            OnDismiss = () => SetState(() => _open = false),
            MatchAnchorWidth = true,
        };

        // The keyboard, exactly while the panel is up. Nested Shortcuts share one child root — the
        // realizers LIST the chords on it — and unmounting unsubscribes, so a closed Select owns no
        // keys at all.
        if (_open && !Disabled && Options.Count > 0)
        {
            select = new Shortcut(select, KeyChord.ArrowDown,
                () => SetState(() => _highlight = Math.Min(Options.Count - 1, _highlight + 1)));
            select = new Shortcut(select, KeyChord.ArrowUp,
                () => SetState(() => _highlight = Math.Max(0, _highlight - 1)));
            select = new Shortcut(select, KeyChord.Enter, () => Choose(_highlight));
        }
        return select;
    }

    private void Toggle() => SetState(() =>
    {
        _open = !_open;
        // The keyboard starts where the VALUE is — Enter straight away re-commits the current
        // choice, which is the no-surprise default the platforms share.
        if (_open) _highlight = SelectedIndex >= 0 && SelectedIndex < Options.Count ? SelectedIndex : 0;
    });

    private void Choose(int index)
    {
        if (index < 0 || index >= Options.Count) return;
        OnChanged?.Invoke(index);
        SetState(() => _open = false);
    }
}
