import { $eq, $NavigableMoveValue, Box, BoxStyle, BuildContext, CalendarNames, Column, CornerRadii, CultureCalendar, DateOnly, Flexible, Icon, IconButton, IconGlyph, Navigable, Pressable, Row, SdkStrings, SizeValue, Spacer, StatefulComponent, StyleDiff, Text, UiComponent, VisualNodeExtensions } from "../runtime-exports";

export class Calendar extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Components.Calendar';
    _month: DateOnly = $eq.time.dateOnly.minValue();
    _cursor: any = null;
    _label: any = null;
    static cellSize: number = 44;
    static headerHeight: number = 28;
    declare selected: any;
    declare onChanged: ((dateOnly: DateOnly) => void) | null;
    declare min: any;
    declare max: any;

    get label() {
        return this._label;
    }

    set label(value) {
        this._label = value;
    }

    constructor(selected: any = null, onChanged: any = null, min: any = null, max: any = null, props?: any) {
        super();
        if (selected !== undefined) this.selected = selected;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (min !== undefined) this.min = min;
        if (max !== undefined) this.max = max;
        this.selected = selected;
        this.onChanged = onChanged;
        this.min = min;
        this.max = max;
        this._month = Calendar.firstOfMonth(selected ?? Calendar.today());
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let first = CalendarNames.firstDayOfWeek;
        let monthTitle = `${$eq.collections.item(CalendarNames.monthNames, CultureCalendar.monthOf(this._month) - 1 | 0)} ${$eq.text.format(CultureCalendar.yearOf(this._month), null, undefined, undefined, 'int32')}`;
        let slot = theme.calendar;
        let cellSize = slot?.cellSize ?? Calendar.cellSize;
        let header = new Row(slot?.headerGap ?? 4, 'start', 'center', false, null, null, { cross: 'center', width: SizeValue.fill });
        header.add(new Text(this.label ?? monthTitle, slot?.titleRole ?? 'titleSmall', slot?.titleColor ?? theme.textPrimary, 1));
        header.add(new Flexible(new Spacer()));
        header.add(new IconButton(new Icon(IconGlyph.fromIcons('chevronLeft')), SdkStrings.previousMonth, 'standard', 'small', () => this.page(-1)));
        header.add(new IconButton(new Icon(IconGlyph.fromIcons('chevronRight')), SdkStrings.nextMonth, 'standard', 'small', () => this.page(1)));
        let names = CalendarNames.dayNamesShort;
        let dayRow = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill });
        for (let column = 0; column < 7; column = column + 1 | 0) {
            dayRow.add(new Box(new BoxStyle({ width: SizeValue.fixed(cellSize), height: SizeValue.fixed(slot?.weekdayHeight ?? Calendar.headerHeight) }), VisualNodeExtensions.centered(new Text($eq.collections.item(names, (first + column | 0) % 7), slot?.weekdayRole ?? 'caption', slot?.weekdayColor ?? theme.textMuted, 1))));
        }
        let rows = [dayRow];
        let start = Calendar.gridStart(Calendar.firstOfMonth(this._month), first);
        for (let week = 0; week < 6; week = week + 1 | 0) {
            let weekRow = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill });
            for (let column = 0; column < 7; column = column + 1 | 0) weekRow.add(this.cell((start + Math.imul(week, 7) | 0) + column | 0, context));
            $eq.collections.listAdd(rows, weekRow);
        }
        let month = new Column(slot?.gap ?? 8);
        month.add(header);
        month.add(new Navigable($eq.delegates.bound(this, this.move), rows, { label: this.label ?? monthTitle, hasHeaderRow: true, activeCell: this.cursorCell(start), role: 'grid' }));
        return month;
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof Calendar && (fresh = next, true)))) return;
        let arriving: any; 
        let moved = (arriving = fresh.selected) != null && !$eq.equals(arriving, this.selected);
        this.selected = fresh.selected;
        this.onChanged = fresh.onChanged;
        this.min = fresh.min;
        this.max = fresh.max;
        this._label = fresh.label;
        let chosen: any; 
        if (moved && (chosen = this.selected) != null) {
            this._month = Calendar.firstOfMonth(chosen);
            this._cursor = chosen;
        } else { let cursor: any; if ((cursor = this._cursor) != null && !this.inRange(cursor)) {
            this._cursor = this.clampToRange(cursor);
            this._month = Calendar.firstOfMonth($eq.nullable.value(this._cursor));
        } }
    }

    cell(number: number, context: BuildContext) {
        let theme = context.theme;
        let slot = theme.calendar;
        let cellSize = slot?.cellSize ?? Calendar.cellSize;
        let size = SizeValue.fixed(cellSize);
        if (!Calendar.written(number)) return new Box(new BoxStyle({ width: size, height: size }));
        let day = $eq.time.dateOnly.fromDayNumber(number);
        if (!this.inMonth(day)) return new Box(new BoxStyle({ width: size, height: size }));
        let selected = $eq.equals(this.selected, day);
        let isToday = day.equals(Calendar.today());
        let reachable = this.inRange(day);
        let primary = theme.colors('primary');
        let numeral = new Text($eq.text.format(CultureCalendar.dayOf(day), null, undefined, undefined, 'int32'), slot?.dayRole ?? 'bodyM', selected ? primary.onBase : isToday ? primary.base : slot?.dayColor ?? theme.textPrimary, 1);
        let cell = new Box(new BoxStyle({ width: size, height: size, cornerRadius: new CornerRadii(Math.fround(cellSize / 2)), background: selected ? primary.base : null, borderWidth: !selected && isToday ? slot?.todayRingWidth ?? 1.5 : 0, borderColor: primary.base, opacity: reachable ? null : theme.disabledOpacity, hover: reachable && !selected ? new StyleDiff({ background: slot?.dayHoverFill ?? theme.surfaceSubtle }) : null }), VisualNodeExtensions.centered(numeral));
        if (!reachable) return cell;
        return new Pressable(cell, () => this.choose(day), { role: 'gridCell', selected: selected, label: isToday ? `${Calendar.spoken(day)}, ${SdkStrings.today}` : Calendar.spoken(day) });
    }

    static spoken(day: DateOnly) {
        return `${$eq.collections.item(CalendarNames.dayNamesLong, Calendar.sundayIndex(day))}, ${$eq.text.format(CultureCalendar.dayOf(day), null, undefined, undefined, 'int32')} ` + `${$eq.collections.item(CalendarNames.monthNames, CultureCalendar.monthOf(day) - 1 | 0)} ${$eq.text.format(CultureCalendar.yearOf(day), null, undefined, undefined, 'int32')}`;
    }

    inMonth(day: DateOnly) {
        let first = Calendar.firstOfMonth(this._month);
        let past = day.dayNumber - first.dayNumber | 0;
        return past >= 0 && past < CultureCalendar.daysInMonth(first);
    }

    static sundayIndex(day: DateOnly) {
        return (day.dayNumber + 1 | 0) % 7;
    }

    cursorCell(start: number): [number, number] | null {
        let cursor: any; 
        if (!((cursor = this._cursor) != null)) return null;
        let offset = cursor.dayNumber - start | 0;
        if (offset < 0 || offset >= 42) return null;
        if (!this.inMonth(cursor) || !this.inRange(cursor)) return null;
        let row = Math.trunc(offset / 7);
        let item = 0;
        for (let column = 0; column < offset % 7; column = column + 1 | 0) {
            let number = (start + Math.imul(row, 7) | 0) + column | 0;
            if (!Calendar.written(number)) continue;
            let day = $eq.time.dateOnly.fromDayNumber(number);
            if (this.inMonth(day) && this.inRange(day)) item = item + 1 | 0;
        }
        return [row + 1 | 0, item];
    }

    move(move: $NavigableMoveValue) {
        let from = this._cursor ?? (this.selected ?? this.clampToRange(Calendar.today()));
        let to = (() => { const $s = move; if ($s === 'previousItem') return Calendar.daysAway(from, -1); if ($s === 'nextItem') return Calendar.daysAway(from, 1); if ($s === 'previousRow') return Calendar.daysAway(from, -7); if ($s === 'nextRow') return Calendar.daysAway(from, 7); if ($s === 'previousPage') return Calendar.monthsAway(from, -1); if ($s === 'nextPage') return Calendar.monthsAway(from, 1); if ($s === 'previousSection') return Calendar.monthsAway(from, -12); if ($s === 'nextSection') return Calendar.monthsAway(from, 12); if ($s === 'rowStart') return Calendar.daysAway(from, -Calendar.dayInWeek(from) | 0); if ($s === 'rowEnd') return Calendar.daysAway(from, 6 - Calendar.dayInWeek(from) | 0); return from; })();
        this.setState(() => {
            this._cursor = this.clampToRange(to);
            this._month = Calendar.firstOfMonth($eq.nullable.value(this._cursor));
        });
    }

    static dayInWeek(day: DateOnly) {
        return ((Calendar.sundayIndex(day) - CalendarNames.firstDayOfWeek | 0) + 7 | 0) % 7;
    }

    static gridStart(month: DateOnly, firstDayOfWeek: number) {
        return month.dayNumber - ((Calendar.sundayIndex(month) - firstDayOfWeek | 0) + 7 | 0) % 7 | 0;
    }

    static written(number: number) {
        return number >= CultureCalendar.firstDay.dayNumber && number <= CultureCalendar.lastDay.dayNumber;
    }

    page(months: number) {
        return this.setState(() => {
            if (!Calendar.counted(this._month, months)) return;
            let target = CultureCalendar.addMonths(this._month, months);
            if (!this.hasReachableDay(target)) return;
            this._month = target;
            let cursor: any; 
            if ((cursor = this._cursor) != null) {
                this._cursor = this.clampToRange(Calendar.monthsAway(cursor, months));
                this._month = Calendar.firstOfMonth($eq.nullable.value(this._cursor));
            }
        });
    }

    hasReachableDay(month: DateOnly) {
        let first = Calendar.firstOfMonth(month);
        let last = first.addDays(CultureCalendar.daysInMonth(first) - 1 | 0);
        let min: any; 
        return this.inRange(first) || this.inRange(last) || (min = this.min) != null && (min.compareTo(first) >= 0) && (min.compareTo(last) <= 0);
    }

    choose(day: DateOnly) {
        if (!this.inRange(day)) return;
        this.setState(() => {
            this._cursor = day;
            this._month = Calendar.firstOfMonth(day);
        });
        this.onChanged?.(day);
    }

    inRange(day: DateOnly) {
        let min: any; let max: any; 
        return (!((min = this.min) != null) || (day.compareTo(min) >= 0)) && (!((max = this.max) != null) || (day.compareTo(max) <= 0)) && (day.compareTo(CultureCalendar.firstDay) >= 0) && (day.compareTo(CultureCalendar.lastDay) <= 0);
    }

    clampToRange(day: DateOnly) {
        let min: any; 
        if ((min = this.min) != null && (day.compareTo(min) < 0)) day = min;
        let max: any; 
        if ((max = this.max) != null && (day.compareTo(max) > 0)) day = max;
        let first = CultureCalendar.firstDay;
        let last = CultureCalendar.lastDay;
        return (day.compareTo(first) < 0) ? first : (day.compareTo(last) > 0) ? last : day;
    }

    static daysAway(from: DateOnly, days: number) {
        let first = CultureCalendar.firstDay.dayNumber;
        let last = CultureCalendar.lastDay.dayNumber;
        let number = from.dayNumber + days | 0;
        return $eq.time.dateOnly.fromDayNumber(number < first ? first : number > last ? last : number);
    }

    static monthsAway(from: DateOnly, months: number) {
        return Calendar.counted(from, months) ? CultureCalendar.addMonths(from, months) : months < 0 ? CultureCalendar.firstDay : CultureCalendar.lastDay;
    }

    static counted(from: DateOnly, months: number) {
        let target = Calendar.monthIndex(from) + months | 0;
        return target >= Calendar.monthIndex(CultureCalendar.firstDay) && target <= Calendar.monthIndex(CultureCalendar.lastDay);
    }

    static monthIndex(day: DateOnly) {
        return (Math.imul(CultureCalendar.yearOf(day), 12) + CultureCalendar.monthOf(day) | 0) - 1 | 0;
    }

    static firstOfMonth(day: DateOnly) {
        return CultureCalendar.firstOfMonth(day);
    }

    static today() {
        return $eq.time.dateOnly.fromDateTime($eq.time.dateTime.now());
    }
}

