import { $eq, Box, BoxStyle, BuildContext, CalendarNames, Column, CornerRadii, DateOnly, Flexible, Icon, IconButton, Navigable, NavigableMoveValue, Pressable, Row, SdkStrings, SizeValue, Spacer, StatefulComponent, StyleDiff, Text, UiComponent } from "../runtime-exports";

export class Calendar extends StatefulComponent {
    _month: DateOnly;
    _cursor: any;
    static cellSize: number = 44;
    static headerHeight: number = 28;
    static $hydration = { _month: 'dateOnly', _cursor: 'dateOnly', selected: 'dateOnly', min: 'dateOnly', max: 'dateOnly' };
    declare selected: any;
    declare onChanged: any;
    declare min: any;
    declare max: any;
    declare label: any;

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
        let monthTitle = `${CalendarNames.monthNames[this._month.month - 1]} ${this._month.year}`;
        let header = new Row(4, 'start', 'center', false, null, null, { cross: 'center', width: SizeValue.fill });
        header.add(new Text(this.label ?? monthTitle, 'titleSmall', theme.textPrimary, 1));
        header.add(new Flexible(new Spacer()));
        header.add(new IconButton(new Icon('chevronLeft'), SdkStrings.previousMonth, 'standard', 'small', () => this.page(-1)));
        header.add(new IconButton(new Icon('chevronRight'), SdkStrings.nextMonth, 'standard', 'small', () => this.page(1)));
        let names = CalendarNames.dayNamesShort;
        let dayRow = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill });
        for (let column = 0; column < 7; column++) {
            dayRow.add(new Box(new BoxStyle({ width: SizeValue.fixed(Calendar.cellSize), height: SizeValue.fixed(Calendar.headerHeight) }), new Text(names[(first + column) % 7], 'caption', theme.textMuted, 1).centered()));
        }
        let rows = [dayRow];
        let start = Calendar.gridStart(this._month, first);
        for (let week = 0; week < 6; week++) {
            let weekRow = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill });
            for (let column = 0; column < 7; column++) weekRow.add(this.cell(start.addDays(week * 7 + column), context));
            rows.push(weekRow);
        }
        let month = new Column(8);
        month.add(header);
        month.add(new Navigable(rows, this.move.bind(this), { label: this.label ?? monthTitle, hasHeaderRow: true, activeCell: this.cursorCell(start), role: 'grid' }));
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
        let chosen: any; 
        if (moved && (chosen = this.selected) != null) {
            this._month = Calendar.firstOfMonth(chosen);
            this._cursor = chosen;
        } else { let cursor: any; if ((cursor = this._cursor) != null && !this.inRange(cursor)) {
            this._cursor = this.clampToRange(cursor);
            this._month = Calendar.firstOfMonth(this._cursor);
        } }
    }

    cell(day: DateOnly, context: any) {
        let theme = context.theme;
        let size = SizeValue.fixed(Calendar.cellSize);
        if (day.month !== this._month.month) return new Box(new BoxStyle({ width: size, height: size }));
        let selected = $eq.equals(this.selected, day);
        let isToday = day.equals(Calendar.today());
        let reachable = this.inRange(day);
        let primary = theme.colors('primary');
        let numeral = new Text(String(day.day), 'bodyM', selected ? primary.onBase : isToday ? primary.base : theme.textPrimary, 1);
        let cell = new Box(new BoxStyle({ width: size, height: size, cornerRadius: new CornerRadii(Calendar.cellSize / 2), background: selected ? primary.base : null, borderWidth: !selected && isToday ? 1.5 : 0, borderColor: primary.base, opacity: reachable ? null : theme.disabledOpacity, hover: reachable && !selected ? new StyleDiff({ background: theme.surfaceSubtle }) : null }), numeral.centered());
        if (!reachable) return cell;
        return new Pressable(cell, () => this.choose(day), { role: 'gridCell', selected: selected, label: isToday ? `${Calendar.spoken(day)}, ${SdkStrings.today}` : Calendar.spoken(day) });
    }

    static spoken(day: DateOnly) {
        return `${CalendarNames.dayNamesLong[Calendar.sundayIndex(day)]}, ${day.day} ${CalendarNames.monthNames[day.month - 1]} ${day.year}`;
    }

    static sundayIndex(day: DateOnly) {
        return (day.dayNumber + 1) % 7;
    }

    cursorCell(start: DateOnly) {
        let cursor: any; 
        if (!((cursor = this._cursor) != null)) return null;
        let offset = cursor.dayNumber - start.dayNumber;
        if (offset < 0 || offset >= 42) return null;
        if (cursor.month !== this._month.month || !this.inRange(cursor)) return null;
        let row = Math.trunc(offset / 7);
        let item = 0;
        for (let column = 0; column < offset % 7; column++) {
            let day = start.addDays(row * 7 + column);
            if (day.month === this._month.month && this.inRange(day)) item++;
        }
        return [row + 1, item];
    }

    move(move: NavigableMoveValue) {
        let from = this._cursor ?? (this.selected ?? this.clampToRange(Calendar.today()));
        let to = (() => { const _s = move; if (_s === 'previousItem') return from.addDays(-1); if (_s === 'nextItem') return from.addDays(1); if (_s === 'previousRow') return from.addDays(-7); if (_s === 'nextRow') return from.addDays(7); if (_s === 'previousPage') return from.addMonths(-1); if (_s === 'nextPage') return from.addMonths(1); if (_s === 'previousSection') return from.addYears(-1); if (_s === 'nextSection') return from.addYears(1); if (_s === 'rowStart') return from.addDays(-Calendar.dayInWeek(from)); if (_s === 'rowEnd') return from.addDays(6 - Calendar.dayInWeek(from)); return from; })();
        this.setState(() => {
            this._cursor = this.clampToRange(to);
            this._month = Calendar.firstOfMonth(this._cursor);
        });
    }

    static dayInWeek(day: DateOnly) {
        return (Calendar.sundayIndex(day) - CalendarNames.firstDayOfWeek + 7) % 7;
    }

    static gridStart(month: DateOnly, firstDayOfWeek: number) {
        return month.addDays(-((Calendar.sundayIndex(month) - firstDayOfWeek + 7) % 7));
    }

    page(months: number) {
        return this.setState(() => {
            let target = this._month.addMonths(months);
            if (!this.hasReachableDay(target)) return;
            this._month = target;
            let cursor: any; 
            if ((cursor = this._cursor) != null) {
                this._cursor = this.clampToRange(cursor.addMonths(months));
                this._month = Calendar.firstOfMonth(this._cursor);
            }
        });
    }

    hasReachableDay(month: DateOnly) {
        let first = Calendar.firstOfMonth(month);
        let next = first.addMonths(1);
        let min: any; 
        return this.inRange(first) || this.inRange(next.addDays(-1)) || (min = this.min) != null && (min.compareTo(first) >= 0) && (min.compareTo(next) < 0);
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
        let min: any; let max: any; return (!((min = this.min) != null) || (day.compareTo(min) >= 0)) && (!((max = this.max) != null) || (day.compareTo(max) <= 0));
    }

    clampToRange(day: DateOnly) {
        let min: any; 
        if ((min = this.min) != null && (day.compareTo(min) < 0)) return min;
        let max: any; 
        if ((max = this.max) != null && (day.compareTo(max) > 0)) return max;
        return day;
    }

    static firstOfMonth(day: DateOnly) {
        return $eq.time.dateOnly(day.year, day.month, 1);
    }

    static today() {
        return $eq.time.dateOnly.fromDateTime($eq.time.dateTime.now());
    }
}

