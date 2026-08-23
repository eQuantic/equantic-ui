using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// A date, picked (design system C15). The <see cref="Calendar"/> in an anchored dialog panel,
/// behind a field that shows what is chosen — the pointer tier of the spec.
/// <para>
/// The TYPED row is not a fallback in the apologetic sense: it is the field itself, always
/// present, always the first thing Tab reaches. C15 requires it for keyboard and switch users and
/// says it stays required once the calendar ships, which is the right call for a second reason —
/// typing 17/07 is faster than paging a grid, and a date far from today (a birthday) is painful
/// any other way.
/// </para>
/// <para>
/// What the spec asks for and this does not do yet: range mode, and the year grid a title tap
/// opens. Both are the Calendar's to grow; this wrapper would not change shape for either.
/// </para>
/// </summary>
public sealed class DatePicker : StatefulComponent
{
    private bool _open;

    /// <summary>What the reader has TYPED, while they are typing it. Null means the field shows
    /// the selection instead — the two are separate because a half-typed date is not a date, and
    /// reformatting under the cursor is the thing every date field gets wrong.</summary>
    private string? _typing;

    public DatePicker(DateOnly? selected = null, Action<DateOnly>? onChanged = null,
        DateOnly? min = null, DateOnly? max = null, string label = "")
    {
        Selected = selected;
        OnChanged = onChanged;
        Min = min;
        Max = max;
        Label = label;
    }

    public DateOnly? Selected { get; private set; }
    public Action<DateOnly>? OnChanged { get; private set; }
    public DateOnly? Min { get; private set; }
    public DateOnly? Max { get; private set; }
    public string Label { get; private set; }
    public bool Disabled { get; init; }

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not DatePicker fresh) return;

        // What the reader typed outlives a re-render — it has to, or a parent that rebuilds on
        // every keystroke erases the word being written. What it must NOT outlive is the app
        // moving the value somewhere else: the buffer wins in Build, so a stale one shows
        // yesterday's text over today's date.
        //
        // The test is whether the buffer still SAYS the incoming value, not whether the value
        // moved. Every parseable keystroke is reported, so the app hands most of them straight
        // back — and clearing on movement alone would reformat the field under the cursor,
        // turning "7/1" into "07/01/2026" mid-word.
        if (_typing is { } buffer && Parse(buffer) != fresh.Selected) _typing = null;

        Selected = fresh.Selected;
        OnChanged = fresh.OnChanged;
        Min = fresh.Min;
        Max = fresh.Max;
        Label = fresh.Label;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var shown = _typing ?? (Selected is { } value ? Format(value) : "");

        // What the reader typed is either a date or it is not. Saying so under the field is the
        // whole reason the typed row can be trusted as the primary path rather than an escape.
        var invalid = _typing is { Length: > 0 } typed && Parse(typed) is null;

        var panel = new Box(new BoxStyle
        {
            Background = theme.Surface,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
            BorderWidth = 1,
            BorderColor = theme.Border,
            Elevation = 2,
            Padding = EdgeInsets.All(Space.S3),
        }, new Calendar(Selected, Pick, Min, Max));

        // The opener is the ICON, and the field around it stays a field. Pressing the whole row
        // would be friendlier to a mouse, but on the web it makes a <button> that contains the
        // <input> — content HTML forbids, and browsers resolve by breaking the typing the C15
        // spec calls the required path for keyboard and switch users. So the calendar hangs off
        // a button of its own: the input is tabbed to and typed into, the button is tabbed to and
        // pressed, and the popover anchors where the glyph already was.
        VisualNode opener = new Anchored(
            new Pressable(new Icon(Icons.Calendar, IconSize.Dense, theme.TextSecondary), Toggle)
            {
                Label = SdkStrings.ChooseDate,
                Expanded = _open,
            },
            panel)
        {
            Open = _open && !Disabled,
            OnDismiss = Close,
            // A DIALOG, not a listbox: the calendar owns its own keyboard, so focus moves INTO the
            // panel rather than being driven from the field the way a combobox drives its list.
            PanelRole = AnchorPanelRole.Dialog,
        };

        // Esc closes while the panel is up, and it wraps the OPENER because that is what the
        // panel's lifetime belongs to — putting it outside the field would also put a key
        // listener around an input for as long as the calendar is open.
        if (_open && !Disabled) opener = new Shortcut(opener, KeyChord.Escape, Close);

        return new TextInput(shown, Type, Label,
            placeholder: SdkStrings.DateFormatHint,
            error: invalid ? SdkStrings.DateFormatHint : null,
            trailing: Disabled ? null : opener)
        {
            Disabled = Disabled,
        };
    }

    /// <summary>A day chosen in the grid: commit it, close, and let the field show it again.</summary>
    private void Pick(DateOnly day)
    {
        OnChanged?.Invoke(day);
        SetState(() =>
        {
            _open = false;
            _typing = null;
        });
    }

    /// <summary>
    /// A keystroke in the field. It commits the moment what is there IS a date, so a reader who
    /// types the whole thing never has to press anything else.
    /// <para>
    /// That means INTERMEDIATE commits: typing 7/17/2026 passes through "7/1", which .NET parses
    /// as the 1st and this reports. The alternative is committing on blur or Enter, which asks
    /// for a second action on every date and is the thing the typed row exists to avoid. The
    /// values are transient and the last one is the one the reader meant — but an app that reacts
    /// EXPENSIVELY to a date (a query per change) should debounce, and that is worth knowing here
    /// rather than discovering in a network tab.
    /// </para></summary>
    private void Type(string text)
    {
        var parsed = Parse(text);
        if (parsed is { } day && InRange(day)) OnChanged?.Invoke(day);
        SetState(() => _typing = text);
    }

    private void Toggle() => SetState(() => _open = !_open);

    private void Close() => SetState(() => _open = false);

    private bool InRange(DateOnly day) =>
        (Min is not { } min || day >= min) && (Max is not { } max || day <= max);

    /// <summary>The date as the culture writes it — the short pattern, which is what the reader
    /// will type back. Written as an INTERPOLATION with a format: a compat type's `ToString("d")`
    /// reaches the runtime class's own `toString`, which takes no format, while a hole with a
    /// specifier reaches the formatter that knows the culture's patterns.</summary>
    private static string Format(DateOnly day) => $"{day:d}";

    /// <summary>What the reader typed, as a date, or null while it is not one yet. The culture's
    /// own short pattern decides — 07/17 is July in en-US and nothing at all in pt-BR, and
    /// guessing between them would silently pick the wrong month twelve times a year.</summary>
    private static DateOnly? Parse(string text)
    {
        // `DateOnly.TryParse(text, out var d)` is what this wants to say, and the compat types have
        // no TryParse on this side yet — the out-parameter lowering exists for numbers and enums
        // and not for these. Filed; the shape below says the same thing and transpiles today.
        try
        {
            return DateOnly.Parse(text);
        }
        catch
        {
            return null;
        }
    }
}
