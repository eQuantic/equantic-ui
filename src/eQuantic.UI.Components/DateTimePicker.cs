using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// A moment: the date and the time of day, as the two controls that already answer each half.
/// <para>
/// It COMPOSES rather than reimplements, and that is the whole design. A combined picker that
/// grew its own grid and its own list would be a third keyboard to get right and a third place
/// for the date arithmetic to drift; here the calendar's arrows and the list's options are the
/// ones already tested, and this owns exactly one thing — that the two halves make one value.
/// </para>
/// <para>
/// A moment needs both halves before it is a moment, so nothing is reported until both are in.
/// Picking a date first and a time second is the order people work in, and either can be
/// re-picked afterwards without losing the other.
/// </para>
/// </summary>
public sealed class DateTimePicker : StatefulComponent
{
    private DateOnly? _date;
    private TimeOnly? _time;

    public DateTimePicker(DateTime? selected = null, Action<DateTime>? onChanged = null,
        DateTime? min = null, DateTime? max = null, int stepMinutes = 30,
        string dateLabel = "", string timeLabel = "")
    {
        Selected = selected;
        OnChanged = onChanged;
        Min = min;
        Max = max;
        StepMinutes = stepMinutes;
        DateLabel = dateLabel;
        TimeLabel = timeLabel;
        _date = selected is { } value ? DateOnly.FromDateTime(value) : null;
        _time = selected is { } moment ? TimeOnly.FromDateTime(moment) : null;
    }

    public DateTime? Selected { get; private set; }
    public Action<DateTime>? OnChanged { get; private set; }
    public DateTime? Min { get; private set; }
    public DateTime? Max { get; private set; }
    public int StepMinutes { get; private set; }
    public string DateLabel { get; private set; }
    public string TimeLabel { get; private set; }
    public bool Disabled { get; init; }

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not DateTimePicker fresh) return;
        // NULL is a value the app can hand down, so the comparison includes it: a parent that
        // clears the moment is moving it as surely as one that changes it, and a picker still
        // showing yesterday's halves after a Clear button is the bug that reads as "it ignored me".
        var moved = fresh.Selected != Selected;
        Selected = fresh.Selected;
        OnChanged = fresh.OnChanged;
        Min = fresh.Min;
        Max = fresh.Max;
        StepMinutes = fresh.StepMinutes;
        DateLabel = fresh.DateLabel;
        TimeLabel = fresh.TimeLabel;
        // The app moved the moment, so both halves follow it — the same rule the Calendar keeps
        // for its view, and for the same reason: a control that ignores the value it was handed
        // looks broken long before anyone calls it wrong.
        if (moved)
        {
            _date = Selected is { } value ? DateOnly.FromDateTime(value) : null;
            _time = Selected is { } moment ? TimeOnly.FromDateTime(moment) : null;
        }
    }

    public override VisualNode Build(ComponentContext context)
    {
        var row = new Row(gap: Space.S3) { Cross = CrossAlign.Start, Width = SizeValue.Fill };
        row.Add(new Flexible(new DatePicker(_date, PickDate,
            Min is { } lower ? DateOnly.FromDateTime(lower) : null,
            Max is { } upper ? DateOnly.FromDateTime(upper) : null,
            DateLabel)
        { Disabled = Disabled }));
        row.Add(new Flexible(new TimePicker(_time, PickTime, StepMinutes, label: TimeLabel)
        { Disabled = Disabled }));
        return row;
    }

    private void PickDate(DateOnly day)
    {
        SetState(() => _date = day);
        Report();
    }

    private void PickTime(TimeOnly time)
    {
        SetState(() => _time = time);
        Report();
    }

    /// <summary>Both halves, or nothing: half a moment is not a value an app can store.</summary>
    private void Report()
    {
        if (_date is not { } day || _time is not { } time) return;
        var moment = day.ToDateTime(time);
        // The bounds are on the MOMENT, not on its halves — 17 July is inside a range that ends at
        // 17 July 09:00 only until the time says otherwise, which is exactly the case a
        // date-only check would wave through.
        if (Min is { } min && moment < min) return;
        if (Max is { } max && moment > max) return;
        Selected = moment;
        OnChanged?.Invoke(moment);
    }
}
