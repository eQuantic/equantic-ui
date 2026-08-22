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
    /// the next month pages the view without choosing anything, exactly as the platforms do.</summary>
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

    /// <summary>The grid's accessible name. Defaults to the month on screen, which is what a
    /// screen reader needs to hear when focus enters it.</summary>
    public string? Label { get; init; }

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not Calendar fresh) return;
        var moved = fresh.Selected is { } arriving && arriving != Selected;
        Selected = fresh.Selected;
        OnChanged = fresh.OnChanged;
        Min = fresh.Min;
        Max = fresh.Max;

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
        var monthTitle = $"{CalendarNames.MonthNames[_month.Month - 1]} {_month.Year}";

        var header = new Row(gap: Space.S1) { Cross = CrossAlign.Center, Width = SizeValue.Fill };
        header.Add(new Text(Label ?? monthTitle, TypeRole.TitleSmall, theme.TextPrimary, maxLines: 1));
        header.Add(new Flexible(new Spacer()));
        header.Add(new IconButton(Icons.ChevronLeft, SdkStrings.PreviousMonth, IconButtonKind.Standard,
            SizeVariant.Small, () => Page(-1)));
        header.Add(new IconButton(Icons.ChevronRight, SdkStrings.NextMonth, IconButtonKind.Standard,
            SizeVariant.Small, () => Page(1)));

        // The day-name row, rotated by where the week starts. CalendarNames answers Sunday-first
        // ALWAYS (indexed by DayOfWeek), so the rotation happens exactly once, here.
        var names = CalendarNames.DayNamesShort;
        var dayRow = new Row(gap: 0) { Width = SizeValue.Fill };
        for (var column = 0; column < 7; column++)
        {
            dayRow.Add(new Box(new BoxStyle { Width = SizeValue.Fixed(CellSize), Height = SizeValue.Fixed(HeaderHeight) },
                new Text(names[(first + column) % 7], TypeRole.Caption, theme.TextMuted, maxLines: 1).Centered()));
        }

        // Six weeks, always: a month that needs five would otherwise change the grid's height as
        // the user pages, which moves everything under it.
        var rows = new List<VisualNode> { dayRow };
        var start = GridStart(_month, first);
        for (var week = 0; week < 6; week++)
        {
            var weekRow = new Row(gap: 0) { Width = SizeValue.Fill };
            for (var column = 0; column < 7; column++)
                weekRow.Add(Cell(start.AddDays(week * 7 + column), context));
            rows.Add(weekRow);
        }

        var month = new Column(gap: Space.S2);
        month.Add(header);
        month.Add(new Navigable(rows, Move)
        {
            Label = Label ?? monthTitle,
            HasHeaderRow = true,
            ActiveCell = CursorCell(start),
            Role = NavigableRole.Grid,
        });
        return month;
    }

    /// <summary>One day. Outside the month it is a hole rather than a greyed number (C15: other
    /// month days hidden) — the grid keeps its shape without offering somewhere to go.</summary>
    private VisualNode Cell(DateOnly day, ComponentContext context)
    {
        var theme = context.Theme;
        var size = SizeValue.Fixed(CellSize);
        if (day.Month != _month.Month) return new Box(new BoxStyle { Width = size, Height = size });

        // `Selected == day` is a LIFTED comparison in C# — false when nothing is selected — and the
        // twin lowers it to `selected.equals(day)`, which throws on null. So an unselected calendar
        // rendered on the server and died in the browser. Written explicitly here; the transpiler
        // gap it stands on is filed separately, because every `nullableDate == date` has it.
        var selected = Selected is { } chosen && chosen == day;
        var isToday = day == Today();
        var reachable = InRange(day);
        var primary = theme.Colors(Variant.Primary);

        var numeral = new Text(day.Day.ToString(), TypeRole.BodyM,
            selected ? primary.OnBase : isToday ? primary.Base : theme.TextPrimary, maxLines: 1);

        var cell = new Box(new BoxStyle
        {
            Width = size,
            Height = size,
            // A full circle for the selection, a ring for today — C15's own two marks, and they
            // compose: today STAYS today when it is picked.
            CornerRadius = new CornerRadii(CellSize / 2),
            Background = selected ? primary.Base : null,
            BorderWidth = !selected && isToday ? 1.5f : 0,
            BorderColor = primary.Base,
            Opacity = reachable ? null : theme.DisabledOpacity,
            Hover = reachable && !selected ? new StyleDiff { Background = theme.SurfaceSubtle } : null,
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
        $"{CalendarNames.DayNamesLong[SundayIndex(day)]}, {day.Day} {CalendarNames.MonthNames[day.Month - 1]} {day.Year}";

    /// <summary>Which day of the week, Sunday-first — the index CalendarNames is ordered by.
    /// Computed from the day NUMBER rather than the DayOfWeek enum: 0001-01-01 is a Monday, so the
    /// arithmetic is exact, and it keeps a seven-entry name→value map out of the twin at every
    /// call site (an enum crosses as its member name, so `(int)` on one is a lookup table).</summary>
    private static int SundayIndex(DateOnly day) => (day.DayNumber + 1) % 7;

    /// <summary>Where the keyboard is, as the grid's own (row, item) — row 0 is the day-name
    /// header, so the weeks start at 1. Null while nothing has been focused.</summary>
    private (int Row, int Item)? CursorCell(DateOnly start)
    {
        if (_cursor is not { } cursor) return null;
        var offset = cursor.DayNumber - start.DayNumber;
        if (offset < 0 || offset >= 42) return null;
        // The cursor's own day has to BE a cell, or there is nothing to point at.
        if (cursor.Month != _month.Month || !InRange(cursor)) return null;

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
            var day = start.AddDays(row * 7 + column);
            if (day.Month == _month.Month && InRange(day)) item++;
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
            NavigableMove.PreviousItem => from.AddDays(-1),
            NavigableMove.NextItem => from.AddDays(1),
            NavigableMove.PreviousRow => from.AddDays(-7),
            NavigableMove.NextRow => from.AddDays(7),
            NavigableMove.PreviousPage => from.AddMonths(-1),
            NavigableMove.NextPage => from.AddMonths(1),
            NavigableMove.PreviousSection => from.AddYears(-1),
            NavigableMove.NextSection => from.AddYears(1),
            NavigableMove.RowStart => from.AddDays(-DayInWeek(from)),
            NavigableMove.RowEnd => from.AddDays(6 - DayInWeek(from)),
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

    /// <summary>The first cell of the grid: the start of the week containing the 1st.</summary>
    private static DateOnly GridStart(DateOnly month, int firstDayOfWeek) =>
        month.AddDays(-((SundayIndex(month) - firstDayOfWeek + 7) % 7));

    private void Page(int months) => SetState(() =>
    {
        _month = _month.AddMonths(months);
        // The cursor travels with the view, or the next arrow press would jump back.
        if (_cursor is { } cursor) _cursor = ClampToRange(cursor.AddMonths(months));
    });

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

    private bool InRange(DateOnly day) =>
        (Min is not { } min || day >= min) && (Max is not { } max || day <= max);

    /// <summary>A move never lands outside the allowed span — the keyboard cannot walk somewhere
    /// Enter would refuse.</summary>
    private DateOnly ClampToRange(DateOnly day)
    {
        if (Min is { } min && day < min) return min;
        if (Max is { } max && day > max) return max;
        return day;
    }

    private static DateOnly FirstOfMonth(DateOnly day) => new(day.Year, day.Month, 1);

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.Now);

    /// <summary>C15: 44dp on device. The header row is shorter — it carries a caption, not a target.</summary>
    private const float CellSize = 44;
    private const float HeaderHeight = 28;
}
