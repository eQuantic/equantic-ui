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
/// <para>
/// GROUPS: the ungrouped <see cref="Options"/> come first, then each of <see cref="Groups"/> under its
/// heading, which every reader announces as a labelled <see cref="Group"/>. The index
/// <see cref="SelectedIndex"/> names and <see cref="OnChanged"/> reports counts every option in that
/// reading order, across the groups, as the web's own <c>select.selectedIndex</c> counts across its
/// <c>optgroup</c>s; the arrows walk the same one list and never stop on a heading. A group whose
/// label is empty draws no heading and wraps no group: a group with no name is noise to a reader.
/// </para>
/// v1 fences: typeahead, multi-select, disabled options.
/// </summary>
public sealed class Select : StatefulComponent
{
    private bool _open;

    /// <summary>The option the KEYBOARD is on — where Enter lands. Separate from SelectedIndex,
    /// which is the app's committed value: arrowing around commits nothing until Enter.</summary>
    private int _highlight;

    /// <summary>Every option in reading order, the one list an index counts in: settled when the
    /// options arrive, in the constructor and in <see cref="AdoptConfig"/>.</summary>
    private IReadOnlyList<string> _choices;

    public Select(IReadOnlyList<string>? options = null, int selectedIndex = -1,
        Action<int>? onChanged = null, string? placeholder = null, IReadOnlyList<OptionGroup>? groups = null)
    {
        Options = options ?? [];
        SelectedIndex = selectedIndex;
        OnChanged = onChanged;
        Placeholder = placeholder;
        Groups = groups ?? [];
        _choices = Flatten(Options, Groups);
    }

    /// <summary>The options outside every group, which lead the list.</summary>
    public IReadOnlyList<string> Options { get; private set; }
    /// <summary>The groups of options after <see cref="Options"/>, each under its heading.</summary>
    public IReadOnlyList<OptionGroup> Groups { get; private set; }
    public int SelectedIndex { get; private set; }
    public Action<int>? OnChanged { get; private set; }
    public string? Placeholder { get; private set; }
    /// <summary>Backed by a field, so <see cref="AdoptConfig"/> can carry it: this instance is kept
    /// across the parent's rebuilds, and an <c>init</c> accessor cannot be written from there.</summary>
    public bool Disabled { get => _disabled; init => _disabled = value; }
    private bool _disabled;

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not Select fresh) return;
        Options = fresh.Options;
        SelectedIndex = fresh.SelectedIndex;
        OnChanged = fresh.OnChanged;
        Placeholder = fresh.Placeholder;
        Groups = fresh.Groups;
        _choices = fresh._choices;
        // The list can shrink under an open panel: the keyboard stays on an option that exists.
        _highlight = Math.Max(0, Math.Min(_highlight, _choices.Count - 1));
        _disabled = fresh.Disabled;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        // The theme's Input slot, then its control ladder, then the component's own (§3.10): the field
        // and each row of its panel share the padding and the gap, and a row is the field's height
        // unless the slot names its own.
        var slot = theme.Input;
        var height = ControlMetrics.Height(context, SizeVariant.Medium, slot?.Controls);
        var paddingX = slot?.PickerPaddingX ?? Space.S3;
        var gap = slot?.PickerGap ?? Space.S2;
        var radius = slot?.Radius ?? theme.Shape(ShapeScale.Medium);
        var borderWidth = slot?.BorderWidth ?? 1;
        var rowHeight = slot?.PanelRowHeight ?? height;
        var choices = _choices;
        var hasValue = SelectedIndex >= 0 && SelectedIndex < choices.Count;

        var fieldRow = new Row(gap: gap) { Cross = CrossAlign.Center, Width = SizeValue.Fill, Height = SizeValue.Fill };
        fieldRow.Add(new Text(hasValue ? choices[SelectedIndex] : Placeholder ?? "Select…",
            TypeRole.BodyM, hasValue ? theme.TextPrimary : theme.TextMuted, maxLines: 1));
        fieldRow.Add(new Flexible(new Spacer()));
        fieldRow.Add(new Icon(Icons.ChevronDown, IconSize.Sm, theme.TextSecondary));

        var field = new Box(new BoxStyle
        {
            Height = height,
            Width = SizeValue.Fill,
            Padding = EdgeInsets.Symmetric(paddingX, 0),
            Background = slot?.Fill ?? theme.Surface,
            CornerRadius = new CornerRadii(radius),
            BorderWidth = borderWidth,
            BorderColor = slot?.BorderColor ?? theme.BorderStrong,
            Opacity = Disabled ? theme.DisabledOpacity : null,
            Hover = Disabled ? null : new StyleDiff { BorderColor = theme.Colors(Variant.Primary).Base },
        }, fieldRow);

        // One index across the groups: a row is numbered in reading order wherever it stands.
        var list = new Column(gap: 0) { Width = SizeValue.Fill };
        var index = 0;
        foreach (var option in Options)
            list.Add(OptionRow(theme, option, index++, gap, paddingX, rowHeight));
        foreach (var group in Groups)
        {
            var rows = new Column(gap: 0) { Width = SizeValue.Fill };
            // The Group decides whether its label names it; a nameless one is no group, and draws
            // no heading either.
            var labelled = new Group(rows) { Label = group.Label };
            if (labelled.IsNamed)
                // The heading is the group's name, which the Group announces: drawn, and read once.
                rows.Add(new Box(new BoxStyle { Padding = EdgeInsets.Symmetric(paddingX, Space.S1), Width = SizeValue.Fill },
                    new Text(group.Label, TypeRole.Caption, theme.TextMuted, maxLines: 1) { ExcludeFromSemantics = true }));
            foreach (var option in group.Options)
                rows.Add(OptionRow(theme, option, index++, gap, paddingX, rowHeight));
            list.Add(labelled);
        }

        var panel = new Box(new BoxStyle
        {
            Background = theme.Surface,
            CornerRadius = new CornerRadii(radius),
            BorderWidth = borderWidth,
            BorderColor = slot?.PanelBorderColor ?? theme.Border,
            Elevation = slot?.PanelElevation ?? 2,
            Padding = EdgeInsets.Symmetric(0, Space.S1),
            Clip = true,
        }, list);

        // A disabled field is still the combobox, one that says it is disabled: a plain box there was
        // static text to every reader on Photon, its role and its state gone.
        var trigger = new Pressable(field, Disabled ? null : Toggle) { Disabled = Disabled, Expanded = _open && !Disabled };

        VisualNode select = new Anchored(trigger, panel)
        {
            Open = _open && !Disabled,
            OnDismiss = () => SetState(() => _open = false),
            MatchAnchorWidth = true,
            // The combobox pattern, whole: the FIELD becomes role=combobox with haspopup/controls,
            // the panel a role=listbox of numbered options, and the keyboard highlight rides
            // aria-activedescendant off the field — stated, not merely painted.
            PanelRole = AnchorPanelRole.Listbox,
            // No option, no active one: an empty listbox names nothing.
            ActiveIndex = _open && _choices.Count > 0 ? _highlight : -1,
        };

        // The keyboard, exactly while the panel is up. Nested Shortcuts share one child root — the
        // realizers LIST the chords on it — and one that is not enabled binds nothing, so a closed
        // Select owns no keys at all. The chords stay around the tree whether it is open or not, so
        // opening moves nothing in it and the focus stays on the field (#567).
        var live = _open && !Disabled && choices.Count > 0;
        select = new Shortcut(select, KeyChord.ArrowDown,
            () => SetState(() => _highlight = Math.Min(choices.Count - 1, _highlight + 1))) { Enabled = live };
        select = new Shortcut(select, KeyChord.ArrowUp,
            () => SetState(() => _highlight = Math.Max(0, _highlight - 1))) { Enabled = live };
        select = new Shortcut(select, KeyChord.Enter, () => Choose(_highlight)) { Enabled = live };
        return select;
    }

    private static IReadOnlyList<string> Flatten(IReadOnlyList<string> options, IReadOnlyList<OptionGroup> groups) =>
        [.. options, .. groups.SelectMany(group => group.Options)];

    private Pressable OptionRow(IAppTheme theme, string option, int index, float gap, float paddingX, float rowHeight)
    {
        var selected = index == SelectedIndex;

        var row = new Row(gap: gap) { Cross = CrossAlign.Center, Width = SizeValue.Fill, Height = SizeValue.Fill };
        row.Add(new Text(option, TypeRole.BodyM,
            selected ? theme.Colors(Variant.Primary).OnSubtle : theme.TextPrimary, maxLines: 1));
        if (selected)
        {
            row.Add(new Flexible(new Spacer()));
            row.Add(new Icon(Icons.Check, IconSize.Sm, theme.Colors(Variant.Primary).OnSubtle));
        }

        var highlighted = _open && index == _highlight;
        return new Pressable(new Box(new BoxStyle
        {
            Height = rowHeight,
            Padding = EdgeInsets.Symmetric(paddingX, 0),
            Width = SizeValue.Fill,
            // The keyboard's row wears the hover coat: one visual language for "you are here",
            // whichever hand is steering.
            Background = selected ? theme.Colors(Variant.Primary).Subtle
                : highlighted ? theme.SurfaceSubtle : null,
            Hover = selected ? null : new StyleDiff { Background = theme.SurfaceSubtle },
        }, row), () => Choose(index))
        {
            // An option is PICKED, like a tab — the check glyph is paint, aria-selected is
            // the answer.
            Role = PressableRole.Option,
            Selected = selected,
        };
    }

    private void Toggle() => SetState(() =>
    {
        _open = !_open;
        // The keyboard starts where the VALUE is — Enter straight away re-commits the current
        // choice, which is the no-surprise default the platforms share.
        if (_open) _highlight = SelectedIndex >= 0 && SelectedIndex < _choices.Count ? SelectedIndex : 0;
    });

    private void Choose(int index)
    {
        if (index < 0 || index >= _choices.Count) return;
        OnChanged?.Invoke(index);
        SetState(() => _open = false);
    }
}
