using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// A time of day, picked from a LIST. No grid: an hour board would be a two-dimensional answer to
/// a one-dimensional question, and the survey behind this track said so before a line was written
/// — a time is a sequence, and a sequence is what the vocabulary already had.
/// <para>
/// The step is the app's, because it is a product decision and not a detail: a booking form wants
/// 30 minutes, an alarm wants 1, and offering 1440 options to someone choosing a meeting slot is
/// the same mistake as offering 24.
/// </para>
/// </summary>
public sealed class TimePicker : StatefulComponent
{
    private bool _open;
    private int _highlight;

    public TimePicker(TimeOnly? selected = null, Action<TimeOnly>? onChanged = null,
        int stepMinutes = 30, TimeOnly? min = null, TimeOnly? max = null, string label = "")
    {
        Selected = selected;
        OnChanged = onChanged;
        StepMinutes = Step(stepMinutes);
        Min = min;
        Max = max;
        Label = label;
    }

    public TimeOnly? Selected { get; private set; }
    public Action<TimeOnly>? OnChanged { get; private set; }
    public int StepMinutes { get; private set; }
    public TimeOnly? Min { get; private set; }
    public TimeOnly? Max { get; private set; }
    public string Label { get; private set; }
    public bool Disabled { get; init; }

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not TimePicker fresh) return;
        Selected = fresh.Selected;
        OnChanged = fresh.OnChanged;
        StepMinutes = Step(fresh.StepMinutes);
        Min = fresh.Min;
        Max = fresh.Max;
        Label = fresh.Label;
    }

    /// <summary>The step both doors agree on — the constructor's and the adopt's. A step under a
    /// minute is not a step: the slot walk would stand still, and the guard that stops it from
    /// standing still forever would leave the list holding one option.</summary>
    private static int Step(int minutes) => minutes < 1 ? 1 : minutes;

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        // A closed panel is never realized — the web target returns before it, and the native one
        // does not paint it — so the rows are built only when they will be seen. At a one-minute
        // step that is 1440 rows, each with its own closure, skipped on every render until the
        // field is pressed.
        var open = _open && !Disabled;
        var times = open ? Slots() : [];
        // The highlight as the LIST can honour it. Two ways it drifts out of range: a range with
        // nothing in it (Min past Max) offers no slots at all, and an app that narrows the range
        // or widens the step while the panel is up can leave the index past the end. Either way
        // the trigger would name an option that does not exist, and an aria-activedescendant
        // pointing at nothing is worse for a screen reader than none at all.
        var highlight = times.Count > 0 ? Math.Min(_highlight, times.Count - 1) : -1;

        var field = new Row(gap: Space.S2) { Cross = CrossAlign.Center, Width = SizeValue.Fill, Height = SizeValue.Fill };
        field.Add(new Icon(Icons.Clock, IconSize.Dense, theme.TextMuted));
        field.Add(new Text(Selected is { } value ? Format(value) : SdkStrings.ChooseTime,
            TypeRole.BodyM, Selected is null ? theme.TextMuted : theme.TextPrimary, maxLines: 1));
        field.Add(new Flexible(new Spacer()));
        field.Add(new Icon(Icons.ChevronDown, IconSize.Sm, theme.TextSecondary));

        var box = new Box(new BoxStyle
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
        }, field);

        var list = new Column(gap: 0) { Width = SizeValue.Fill };
        for (var i = 0; i < times.Count; i++)
        {
            var slot = times[i];
            var picked = Selected == slot;
            // The keyboard's row wears the hover coat — one visual language for "you are here",
            // whichever hand is steering. Same rule the Select keeps.
            var highlighted = i == highlight;
            var row = new Row(gap: Space.S2) { Cross = CrossAlign.Center, Width = SizeValue.Fill, Height = SizeValue.Fill };
            row.Add(new Text(Format(slot), TypeRole.BodyM,
                picked ? theme.Colors(Variant.Primary).OnSubtle : theme.TextPrimary, maxLines: 1));

            list.Add(new Pressable(new Box(new BoxStyle
            {
                Height = Sizing.Height(SizeVariant.Medium, context.Density),
                Padding = EdgeInsets.Symmetric(Space.S3, 0),
                Width = SizeValue.Fill,
                Background = picked ? theme.Colors(Variant.Primary).Subtle
                    : highlighted ? theme.SurfaceSubtle : null,
                Hover = picked ? null : new StyleDiff { Background = theme.SurfaceSubtle },
            }, row), () => Pick(slot))
            {
                // One of a list, PICKED — the same word a Select's rows use, because this is one.
                Role = PressableRole.Option,
                Selected = picked,
            });
        }

        var panel = new Box(new BoxStyle
        {
            Background = theme.Surface,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
            BorderWidth = 1,
            BorderColor = theme.Border,
            Elevation = 2,
            Padding = EdgeInsets.Symmetric(0, Space.S1),
            // A day of half-hours is 48 rows; the panel scrolls rather than growing past the page.
            Height = SizeValue.Fixed(PanelHeight),
            Clip = true,
        }, new ScrollView(list));

        VisualNode picker = new Anchored(
            Disabled ? box : new Pressable(box, Toggle) { Label = Label.Length > 0 ? Label : SdkStrings.ChooseTime, Expanded = _open },
            panel)
        {
            Open = open,
            OnDismiss = Close,
            MatchAnchorWidth = true,
            PanelRole = AnchorPanelRole.Listbox,
            // The half of the combobox pattern a painted highlight cannot supply: the options are
            // reachable by pointer only until the trigger SAYS which one the keyboard is on.
            ActiveIndex = open ? highlight : -1,
        };

        // The keyboard, exactly while the panel is up — a closed picker owns no keys at all.
        // Without this a keyboard user could open the list and then not move in it: the rows are
        // tabindex=-1 by the listbox pattern, which is the whole reason the highlight travels on
        // aria-activedescendant instead.
        if (open)
        {
            picker = new Shortcut(picker, KeyChord.Escape, Close);
            if (highlight >= 0)
            {
                picker = new Shortcut(picker, KeyChord.ArrowDown,
                    () => SetState(() => _highlight = Math.Min(times.Count - 1, highlight + 1)));
                picker = new Shortcut(picker, KeyChord.ArrowUp,
                    () => SetState(() => _highlight = Math.Max(0, highlight - 1)));
                picker = new Shortcut(picker, KeyChord.Enter, () => Pick(times[highlight]));
            }
        }
        return picker;
    }

    /// <summary>Every slot the step lands on, inside the allowed span.</summary>
    private IReadOnlyList<TimeOnly> Slots()
    {
        var slots = new List<TimeOnly>();
        var first = Min ?? new TimeOnly(0, 0);
        var last = Max ?? new TimeOnly(23, 59);
        for (var at = first; at <= last; at = at.AddMinutes(StepMinutes))
        {
            slots.Add(at);
            // The last step of a day wraps to 00:00, which would loop forever.
            if (at.AddMinutes(StepMinutes) <= at) break;
        }
        return slots;
    }

    private void Pick(TimeOnly time)
    {
        OnChanged?.Invoke(time);
        SetState(() => _open = false);
    }

    private void Toggle() => SetState(() =>
    {
        _open = !_open;
        // The keyboard starts where the VALUE is: Enter straight away re-commits the current time,
        // and the list opens scrolled to it rather than to midnight.
        if (_open) _highlight = Selected is { } value ? SlotOf(value) : 0;
    });

    /// <summary>Where a time sits among the slots, or the first one when it is not one of them —
    /// a step the app narrowed can leave the current value off the list it now offers.</summary>
    private int SlotOf(TimeOnly value)
    {
        var times = Slots();
        for (var i = 0; i < times.Count; i++)
        {
            if (times[i] == value) return i;
        }
        return 0;
    }

    private void Close() => SetState(() => _open = false);

    /// <summary>The time as the culture writes a short one — 14:30 or 2:30 PM, never both. An
    /// interpolation with a specifier, for the reason DatePicker.Format gives.</summary>
    private static string Format(TimeOnly time) => $"{time:t}";

    private const float PanelHeight = 260;
}
