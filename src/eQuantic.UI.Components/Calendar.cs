using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// A month at a time, as a real grid (design system C15). One Tab stop for the whole thing, arrows
/// that walk a day, and cells that announce what they are — the vocabulary the picker track added
/// in slice 1, over the names the culture supplies in slice 2.
/// <para>
/// It is a component of its own, not the inside of a DatePicker, because an inline calendar has
/// its own uses (a schedule, a range) and because the popover tiers are then thin: a DatePicker is
/// this in an <see cref="Anchored"/> panel, a mobile one is this in a BottomSheet. The keyboard is
/// written once, here.
/// </para>
/// <para>
/// What C15 asks for and this does NOT do yet: range selection (endpoints plus the band between),
/// the year grid a title tap opens, and swipe-to-page. Each is additive — the grid, the keyboard
/// and the semantics do not change shape for them.
/// </para>
/// <para>
/// The name collides with <c>System.Globalization.Calendar</c> for a file that imports both, and
/// it is kept anyway: it is what the design system calls this and what an author reaching for a
/// month grid will type. The declarative surface never sees the collision — <c>Calendar(…)</c> is
/// a factory METHOD — and the rare file that needs both namespaces aliases one, which is what the
/// framework's own tests do.
/// </para>
/// </summary>
public sealed class Calendar : StatefulComponent
{
    /// <summary>The month on screen, as its first day. Separate from the SELECTION: arrowing into
    /// the next month pages the view without choosing anything, exactly as the platforms do. The month is
    /// the culture calendar's (<see cref="CultureCalendar"/>): in fa-IR, January 17th 2024 is on the grid
    /// of Dey 1402, from December 22nd to January 20th (#1617). Any day of it names it, since a culture
    /// switched after it was stored counts another first day: <see cref="Build"/> counts it.</summary>
    private DateOnly _month;

    /// <summary>The day the keyboard is on — where Enter lands. Null until the grid is entered,
    /// so a calendar nobody has touched shows no focus ring.</summary>
    private DateOnly? _cursor;

    public Calendar(DateOnly? selected = null, Action<DateOnly>? onChanged = null,
        DateOnly? min = null, DateOnly? max = null)
    {
        Selected = selected;
        OnChanged = onChanged;
        Min = min;
        Max = max;
        _month = FirstOfMonth(selected ?? Today());
    }

    public DateOnly? Selected { get; private set; }
    public Action<DateOnly>? OnChanged { get; private set; }
    public DateOnly? Min { get; private set; }
    public DateOnly? Max { get; private set; }

    /// <summary>
    /// The grid's accessible name. Defaults to the month on screen, which is what a screen reader
    /// needs to hear when focus enters it.
    /// <para>
    /// Backed by a field, so <see cref="AdoptConfig"/> can carry it: this instance is kept across
    /// the parent's rebuilds, and an <c>init</c> accessor cannot be written from there.
    /// </para>
    /// </summary>
    public string? Label { get => _label; init => _label = value; }
    private string? _label;

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not Calendar fresh) return;
        var moved = fresh.Selected is { } arriving && arriving != Selected;
        Selected = fresh.Selected;
        OnChanged = fresh.OnChanged;
        Min = fresh.Min;
        Max = fresh.Max;
        _label = fresh.Label;

        // A CONTROLLED calendar: the app moved the selection, so the view follows it. Without this
        // the instance adopted the new date and kept showing the old month — the selection was
        // simply not on screen, and the calendar looked like it had ignored the app.
        // Only when the selection actually MOVED: re-syncing on every adopt would yank the view
        // back from wherever the reader had paged to, on any unrelated re-render.
        if (moved && Selected is { } chosen)
        {
            _month = FirstOfMonth(chosen);
            _cursor = chosen;
        }
        // New bounds can strand the cursor outside them, where Enter would refuse it.
        else if (_cursor is { } cursor && !InRange(cursor))
        {
            _cursor = ClampToRange(cursor);
            _month = FirstOfMonth(_cursor.Value);
        }
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var first = CalendarNames.FirstDayOfWeek;
        var monthTitle = $"{CalendarNames.MonthNames[CultureCalendar.MonthOf(_month) - 1]} {CultureCalendar.YearOf(_month)}";
        // The theme's Calendar slot, then the component's own (§3.10).
        var slot = theme.Calendar;
        var cellSize = slot?.CellSize ?? CellSize;

        var header = new Row(gap: slot?.HeaderGap ?? Space.S1) { Cross = CrossAlign.Center, Width = SizeValue.Fill };
        header.Add(new Text(Label ?? monthTitle, slot?.TitleRole ?? TypeRole.TitleSmall,
            slot?.TitleColor ?? theme.TextPrimary, maxLines: 1));
        header.Add(new Flexible(new Spacer()));
        header.Add(new IconButton(new Icon(Icons.ChevronLeft), SdkStrings.PreviousMonth, IconButtonKind.Standard,
            SizeVariant.Small, () => Page(-1)));
        header.Add(new IconButton(new Icon(Icons.ChevronRight), SdkStrings.NextMonth, IconButtonKind.Standard,
            SizeVariant.Small, () => Page(1)));

        // The day-name row, rotated by where the week starts. CalendarNames answers Sunday-first
        // ALWAYS (indexed by DayOfWeek), so the rotation happens exactly once, here.
        var names = CalendarNames.DayNamesShort;
        var dayRow = new Row(gap: 0) { Width = SizeValue.Fill };
        for (var column = 0; column < 7; column++)
        {
            dayRow.Add(new Box(new BoxStyle
                {
                    Width = SizeValue.Fixed(cellSize),
                    Height = SizeValue.Fixed(slot?.WeekdayHeight ?? HeaderHeight),
                },
                new Text(names[(first + column) % 7], slot?.WeekdayRole ?? TypeRole.Caption,
                    slot?.WeekdayColor ?? theme.TextMuted, maxLines: 1).Centered()));
        }

        // Six weeks, always: a month that needs five would otherwise change the grid's height as
        // the user pages, which moves everything under it.
        var rows = new List<VisualNode> { dayRow };
        // Its first day counted in the culture's calendar NOW: a culture switched since the month was
        // stored redraws this instance, and the month's first day is that calendar's.
        var start = GridStart(FirstOfMonth(_month), first);
        for (var week = 0; week < 6; week++)
        {
            var weekRow = new Row(gap: 0) { Width = SizeValue.Fill };
            for (var column = 0; column < 7; column++)
                weekRow.Add(Cell(start + week * 7 + column, context));
            rows.Add(weekRow);
        }

        var month = new Column(gap: slot?.Gap ?? Space.S2);
        month.Add(header);
        month.Add(new Navigable(Move, rows)
        {
            Label = Label ?? monthTitle,
            HasHeaderRow = true,
            ActiveCell = CursorCell(start),
            Role = NavigableRole.Grid,
        });
        return month;
    }

    /// <summary>One day, by its number (days since January 1st of the year 1). Outside the month it is a
    /// hole rather than a greyed number (C15: other month days hidden) — the grid keeps its shape without
    /// offering somewhere to go. So is a day the calendar does not write, which the first and last month's
    /// weeks reach: before the year 1 and after 9999 no date names it at all, and Um Al Qura's end in
    /// November 2077 (#1849).</summary>
    private VisualNode Cell(int number, ComponentContext context)
    {
        var theme = context.Theme;
        var slot = theme.Calendar;
        var cellSize = slot?.CellSize ?? CellSize;
        var size = SizeValue.Fixed(cellSize);
        if (!Written(number)) return new Box(new BoxStyle { Width = size, Height = size });
        var day = DateOnly.FromDayNumber(number);
        if (!InMonth(day)) return new Box(new BoxStyle { Width = size, Height = size });

        var selected = Selected == day;
        var isToday = day == Today();
        var reachable = InRange(day);
        var primary = theme.Colors(Variant.Primary);

        var numeral = new Text(CultureCalendar.DayOf(day).ToString(), slot?.DayRole ?? TypeRole.BodyM,
            selected ? primary.OnBase : isToday ? primary.Base : slot?.DayColor ?? theme.TextPrimary, maxLines: 1);

        var cell = new Box(new BoxStyle
        {
            Width = size,
            Height = size,
            // A full circle for the selection, a ring for today — C15's own two marks, and they
            // compose: today STAYS today when it is picked.
            CornerRadius = new CornerRadii(cellSize / 2),
            Background = selected ? primary.Base : null,
            BorderWidth = !selected && isToday ? slot?.TodayRingWidth ?? 1.5f : 0,
            BorderColor = primary.Base,
            Opacity = reachable ? null : theme.DisabledOpacity,
            Hover = reachable && !selected ? new StyleDiff { Background = slot?.DayHoverFill ?? theme.SurfaceSubtle } : null,
        }, numeral.Centered());

        if (!reachable) return cell;

        return new Pressable(cell, () => Choose(day))
        {
            Role = PressableRole.GridCell,
            Selected = selected,
            // The full day name, never the abbreviation: a screen reader reads "Fri" letter by
            // letter. Today says so in words, because the ring says it only to people who can see.
            Label = isToday
                ? $"{Spoken(day)}, {SdkStrings.Today}"
                : Spoken(day),
        };
    }

    /// <summary>How a cell announces itself — "Friday, 17 July 2026": the day name and the month
    /// name in the reader's language, in a FIXED day-month-year order.
    /// <para>
    /// The order is ours, not the culture's. A long-date pattern would put the month first in
    /// en-US and the day first in pt-BR, and reaching it means formatting through the pattern
    /// rather than composing the parts — worth doing, and not while the composition is the thing
    /// under test. What matters for a screen reader is already right: the names are translated,
    /// and the day is spoken in full rather than as an abbreviation it would spell out.
    /// </para></summary>
    private static string Spoken(DateOnly day) =>
        $"{CalendarNames.DayNamesLong[SundayIndex(day)]}, {CultureCalendar.DayOf(day)} "
        + $"{CalendarNames.MonthNames[CultureCalendar.MonthOf(day) - 1]} {CultureCalendar.YearOf(day)}";

    /// <summary>Whether a day is in the month on screen, by the culture calendar's months: counted from
    /// the month's first day by its length, so no day outside the month is asked its month. A calendar of
    /// a bounded range refuses that question past its first and last month (Um Al Qura's, in April 1900
    /// and November 2077), where the grid's leading and trailing weeks reach.</summary>
    private bool InMonth(DateOnly day)
    {
        var first = FirstOfMonth(_month);
        var past = day.DayNumber - first.DayNumber;
        return past >= 0 && past < CultureCalendar.DaysInMonth(first);
    }

    /// <summary>Which day of the week, Sunday-first — the index CalendarNames is ordered by.
    /// Computed from the day NUMBER rather than the DayOfWeek enum: 0001-01-01 is a Monday, so the
    /// arithmetic is exact, and it keeps a seven-entry name→value map out of the twin at every
    /// call site (an enum crosses as its member name, so `(int)` on one is a lookup table).</summary>
    private static int SundayIndex(DateOnly day) => (day.DayNumber + 1) % 7;

    /// <summary>Where the keyboard is, as the grid's own (row, item) — row 0 is the day-name
    /// header, so the weeks start at 1. Null while nothing has been focused.</summary>
    private (int Row, int Item)? CursorCell(int start)
    {
        if (_cursor is not { } cursor) return null;
        var offset = cursor.DayNumber - start;
        if (offset < 0 || offset >= 42) return null;
        // The cursor's own day has to BE a cell, or there is nothing to point at.
        if (!InMonth(cursor) || !InRange(cursor)) return null;

        // COUNTED, not the column. The realizers number cells by walking the row and numbering
        // what carries role=gridcell, and this grid is full of things that do not: the days of the
        // neighbouring months are holes and the days outside min/max are plain boxes. July 2026
        // starts on a Wednesday, so the first week has three holes before the 1st — by column it
        // would be item 3, and the realizer calls it item 0. The activedescendant would have named
        // the 4th while the ring sat on the 1st.
        var row = offset / 7;
        var item = 0;
        for (var column = 0; column < offset % 7; column++)
        {
            var number = start + row * 7 + column;
            if (!Written(number)) continue;
            var day = DateOnly.FromDayNumber(number);
            if (InMonth(day) && InRange(day)) item++;
        }
        return (row + 1, item);
    }

    /// <summary>What an abstract move means to a month grid. The COMPOSITE decides, which is why
    /// Navigable reports a direction and not a key: a page here is a month, a section is a year.</summary>
    private void Move(NavigableMove move)
    {
        var from = _cursor ?? Selected ?? ClampToRange(Today());
        var to = move switch
        {
            NavigableMove.PreviousItem => DaysAway(from, -1),
            NavigableMove.NextItem => DaysAway(from, 1),
            NavigableMove.PreviousRow => DaysAway(from, -7),
            NavigableMove.NextRow => DaysAway(from, 7),
            NavigableMove.PreviousPage => MonthsAway(from, -1),
            NavigableMove.NextPage => MonthsAway(from, 1),
            NavigableMove.PreviousSection => MonthsAway(from, -12),
            NavigableMove.NextSection => MonthsAway(from, 12),
            NavigableMove.RowStart => DaysAway(from, -DayInWeek(from)),
            NavigableMove.RowEnd => DaysAway(from, 6 - DayInWeek(from)),
            _ => from,
        };

        SetState(() =>
        {
            _cursor = ClampToRange(to);
            // The view FOLLOWS the cursor: arrowing off the edge of a month pages it, which is
            // what makes ↓ from the last week work at all.
            _month = FirstOfMonth(_cursor.Value);
        });
    }

    /// <summary>Which column a day sits in, counting from where this culture's week starts.</summary>
    private static int DayInWeek(DateOnly day) =>
        (SundayIndex(day) - CalendarNames.FirstDayOfWeek + 7) % 7;

    /// <summary>The first cell of the grid, by its number: the start of the week containing the 1st, which
    /// before January 1st of the year 1 no date names.</summary>
    private static int GridStart(DateOnly month, int firstDayOfWeek) =>
        month.DayNumber - (SundayIndex(month) - firstDayOfWeek + 7) % 7;

    /// <summary>Whether the calendar writes the day of number <paramref name="number"/>.</summary>
    private static bool Written(int number) =>
        number >= CultureCalendar.FirstDay.DayNumber && number <= CultureCalendar.LastDay.DayNumber;

    private void Page(int months) => SetState(() =>
    {
        // Past the calendar's first or last month there is no month to page to (#1849).
        if (!Counted(_month, months)) return;
        var target = CultureCalendar.AddMonths(_month, months);
        // A month with no reachable day in it is not somewhere to go: min/max bound the chevrons
        // exactly as they bound the arrows, and paging into an empty grid would leave the reader
        // looking at a month they cannot use with no way to tell why.
        if (!HasReachableDay(target)) return;

        _month = target;
        // The cursor travels with the view, or the next arrow press would jump back. Clamping can
        // land it in a different month than the one just paged to — the view follows the CURSOR,
        // which is the rule the arrows already obey, so the two never disagree.
        if (_cursor is { } cursor)
        {
            _cursor = ClampToRange(MonthsAway(cursor, months));
            _month = FirstOfMonth(_cursor.Value);
        }
    });

    /// <summary>Whether a month holds a day the reader may actually pick.</summary>
    private bool HasReachableDay(DateOnly month)
    {
        var first = FirstOfMonth(month);
        var last = first.AddDays(CultureCalendar.DaysInMonth(first) - 1);
        return InRange(first) || InRange(last)
            || (Min is { } min && min >= first && min <= last);
    }

    private void Choose(DateOnly day)
    {
        if (!InRange(day)) return;
        SetState(() =>
        {
            _cursor = day;
            _month = FirstOfMonth(day);
        });
        OnChanged?.Invoke(day);
    }

    /// <summary>Whether a day may be picked: inside min and max, and inside the days the culture's calendar
    /// writes, which Um Al Qura's end in November 2077 (#1849).</summary>
    private bool InRange(DateOnly day) =>
        (Min is not { } min || day >= min) && (Max is not { } max || day <= max)
        && day >= CultureCalendar.FirstDay && day <= CultureCalendar.LastDay;

    /// <summary>A move never lands outside the allowed span — the keyboard cannot walk somewhere
    /// Enter would refuse, nor past the days the calendar writes.</summary>
    private DateOnly ClampToRange(DateOnly day)
    {
        if (Min is { } min && day < min) day = min;
        if (Max is { } max && day > max) day = max;
        var first = CultureCalendar.FirstDay;
        var last = CultureCalendar.LastDay;
        return day < first ? first : day > last ? last : day;
    }

    /// <summary>A day <paramref name="days"/> away, held to the days the calendar writes, so an arrow at its
    /// last day stays there instead of asking a day past it.</summary>
    private static DateOnly DaysAway(DateOnly from, int days)
    {
        var first = CultureCalendar.FirstDay.DayNumber;
        var last = CultureCalendar.LastDay.DayNumber;
        var number = from.DayNumber + days;
        return DateOnly.FromDayNumber(number < first ? first : number > last ? last : number);
    }

    /// <summary>A day <paramref name="months"/> of the calendar's months away, or the calendar's first or
    /// last day where the move would leave the months it counts, which .NET refuses (#1849).</summary>
    private static DateOnly MonthsAway(DateOnly from, int months) =>
        Counted(from, months)
            ? CultureCalendar.AddMonths(from, months)
            : months < 0 ? CultureCalendar.FirstDay : CultureCalendar.LastDay;

    /// <summary>Whether the month <paramref name="months"/> away from a day's is one the calendar counts,
    /// between its first day's month and its last day's, counted as twelve a year, which every culture's
    /// calendar has.</summary>
    private static bool Counted(DateOnly from, int months)
    {
        var target = MonthIndex(from) + months;
        return target >= MonthIndex(CultureCalendar.FirstDay) && target <= MonthIndex(CultureCalendar.LastDay);
    }

    private static int MonthIndex(DateOnly day) => CultureCalendar.YearOf(day) * 12 + CultureCalendar.MonthOf(day) - 1;

    private static DateOnly FirstOfMonth(DateOnly day) => CultureCalendar.FirstOfMonth(day);

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.Now);

    /// <summary>C15: 44dp on device, unless the theme's Calendar slot says otherwise. The header row is
    /// shorter — it carries a caption, not a target.</summary>
    private const float CellSize = 44;
    private const float HeaderHeight = 28;
}
