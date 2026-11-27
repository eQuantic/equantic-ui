import { $eq, BuildContext, DateOnly, DatePicker, DateTime, DevelopmentNotices, Flexible, Row, SizeValue, StatefulComponent, TimeOnly, TimePicker, UiComponent } from "../runtime-exports";

export class DateTimePicker extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Components.DateTimePicker';
    _date: any = null;
    _time: any = null;
    _disabled: boolean = false;
    _labelHidden: boolean = false;
    _reported: any = null;
    declare selected: any;
    declare onChanged: ((dateTime: DateTime) => void) | null;
    declare min: any;
    declare max: any;
    declare stepMinutes: number;
    declare dateLabel: string;
    declare timeLabel: string;
    declare style: any;
    declare calendarStyle: any;

    get disabled() {
        return this._disabled;
    }

    set disabled(value) {
        this._disabled = value;
    }

    get labelHidden() {
        return this._labelHidden;
    }

    set labelHidden(value) {
        this._labelHidden = value;
    }

    constructor(selected: any = null, onChanged: any = null, min: any = null, max: any = null, stepMinutes: any = 30, dateLabel: any = '', timeLabel: any = '', style: any = null, calendarStyle: any = null, props?: any) {
        super();
        if (selected !== undefined) this.selected = selected;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (min !== undefined) this.min = min;
        if (max !== undefined) this.max = max;
        if (stepMinutes !== undefined) this.stepMinutes = stepMinutes;
        if (dateLabel !== undefined) this.dateLabel = dateLabel;
        if (timeLabel !== undefined) this.timeLabel = timeLabel;
        if (style !== undefined) this.style = style;
        if (calendarStyle !== undefined) this.calendarStyle = calendarStyle;
        if (this.stepMinutes === undefined) this.stepMinutes = 0;
        this.selected = selected;
        this.onChanged = onChanged;
        this.min = min;
        this.max = max;
        this.stepMinutes = stepMinutes;
        this.dateLabel = dateLabel;
        this.timeLabel = timeLabel;
        this.style = style;
        this.calendarStyle = calendarStyle;
        let value: any; 
        this._date = (value = selected) != null ? $eq.time.dateOnly.fromDateTime(value) : null;
        let moment: any; 
        this._time = (moment = selected) != null ? $eq.time.timeOnly.fromDateTime(moment) : null;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(_context: BuildContext) {
        let row = new Row(12, 'start', 'center', false, null, null, { cross: 'start', width: SizeValue.fill });
        let lower: any; let upper: any; 
        row.add(new Flexible(new DatePicker(this._date, $eq.delegates.bound(this, this.pickDate), (lower = this.min) != null ? $eq.time.dateOnly.fromDateTime(lower) : null, (upper = this.max) != null ? $eq.time.dateOnly.fromDateTime(upper) : null, this.dateLabel, this.style, this.calendarStyle, { disabled: this.disabled, labelHidden: this.labelHidden, composed: true })));
        row.add(new Flexible(new TimePicker(this._time, $eq.delegates.bound(this, this.pickTime), this.stepMinutes, null, null, this.timeLabel, this.style, { disabled: this.disabled, composed: true })));
        return row;
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof DateTimePicker && (fresh = next, true)))) return;
        let said: any; 
        if ((said = this._reported) != null && fresh.selected == null) DevelopmentNotices.say(`A DateTimePicker reported ${$eq.text.format(said, 'g')} through onChanged and was drawn again with no moment, ` + 'so its fields emptied: a picker shows the moment it is handed. Hand it back the moment onChanged reports.');
        this._reported = null;
        let moved = !$eq.equals(fresh.selected, this.selected);
        this.selected = fresh.selected;
        this.onChanged = fresh.onChanged;
        this.min = fresh.min;
        this.max = fresh.max;
        this.stepMinutes = fresh.stepMinutes;
        this.dateLabel = fresh.dateLabel;
        this.timeLabel = fresh.timeLabel;
        this.style = fresh.style;
        this.calendarStyle = fresh.calendarStyle;
        this._disabled = fresh.disabled;
        this._labelHidden = fresh.labelHidden;
        if (moved) {
            let value: any; 
            this._date = (value = this.selected) != null ? $eq.time.dateOnly.fromDateTime(value) : null;
            let moment: any; 
            this._time = (moment = this.selected) != null ? $eq.time.timeOnly.fromDateTime(moment) : null;
        }
    }

    pickDate(day: DateOnly) {
        this.setState(() => this._date = day);
        this.report();
    }

    pickTime(time: TimeOnly) {
        this.setState(() => this._time = time);
        this.report();
    }

    report() {
        let day: any; let time: any; 
        if (!((day = this._date) != null) || !((time = this._time) != null)) return;
        let moment = day.toDateTime(time);
        let min: any; 
        if ((min = this.min) != null && (moment.compareTo(min) < 0)) return;
        let max: any; 
        if ((max = this.max) != null && (moment.compareTo(max) > 0)) return;
        this.selected = moment;
        if (!(this.onChanged == null)) this._reported = moment;
        this.onChanged?.(moment);
    }
}

