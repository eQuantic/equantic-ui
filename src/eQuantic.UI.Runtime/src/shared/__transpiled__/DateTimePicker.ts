import { $eq, BuildContext, DateOnly, DatePicker, Flexible, Row, SharedStatefulComponent, SizeValue, TimeOnly, TimePicker, UiComponent } from "@equantic/runtime";

export class DateTimePicker extends SharedStatefulComponent {
    _date: any;
    _time: any;
    static $hydration = { _date: 'dateOnly', _time: 'timeOnly' };
    declare selected: any;
    declare onChanged: any;
    declare min: any;
    declare max: any;
    declare stepMinutes: number;
    declare dateLabel: string;
    declare timeLabel: string;
    declare disabled: boolean;

    constructor(selected: any = null, onChanged: any = null, min: any = null, max: any = null, stepMinutes: any = 30, dateLabel: any = '', timeLabel: any = '', props?: any) {
        super();
        if (selected !== undefined) this.selected = selected;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (min !== undefined) this.min = min;
        if (max !== undefined) this.max = max;
        if (stepMinutes !== undefined) this.stepMinutes = stepMinutes;
        if (dateLabel !== undefined) this.dateLabel = dateLabel;
        if (timeLabel !== undefined) this.timeLabel = timeLabel;
        if (this.stepMinutes === undefined) this.stepMinutes = 0;
        if (this.disabled === undefined) this.disabled = false;
        this.selected = selected;
        this.onChanged = onChanged;
        this.min = min;
        this.max = max;
        this.stepMinutes = stepMinutes;
        this.dateLabel = dateLabel;
        this.timeLabel = timeLabel;
        let value: any; 
        this._date = (value = selected) != null ? $eq.time.dateOnly.fromDateTime(value) : null;
        let moment: any; 
        this._time = (moment = selected) != null ? $eq.time.timeOnly.fromDateTime(moment) : null;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(_context: BuildContext) {
        let row = new Row(12, 'start', 'center', false, null, null, { cross: 'start', width: SizeValue.fill });
        let lower: any; let upper: any; 
        row.add(new Flexible(new DatePicker(this._date, this.pickDate.bind(this), (lower = this.min) != null ? $eq.time.dateOnly.fromDateTime(lower) : null, (upper = this.max) != null ? $eq.time.dateOnly.fromDateTime(upper) : null, this.dateLabel, { disabled: this.disabled })));
        row.add(new Flexible(new TimePicker(this._time, this.pickTime.bind(this), this.stepMinutes, null, null, this.timeLabel, { disabled: this.disabled })));
        return row;
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof DateTimePicker && (fresh = next, true)))) return;
        let moved = !$eq.equals(fresh.selected, this.selected);
        this.selected = fresh.selected;
        this.onChanged = fresh.onChanged;
        this.min = fresh.min;
        this.max = fresh.max;
        this.stepMinutes = fresh.stepMinutes;
        this.dateLabel = fresh.dateLabel;
        this.timeLabel = fresh.timeLabel;
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
        this.onChanged?.(moment);
    }
}

