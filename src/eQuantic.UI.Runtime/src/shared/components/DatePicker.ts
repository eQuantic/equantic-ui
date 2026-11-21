import { $eq, Anchored, Box, BoxStyle, BuildContext, Calendar, CornerRadii, DateOnly, DevelopmentNotices, EdgeInsets, Icon, IconGlyph, KeyChord, Pressable, SdkStrings, Shortcut, StatefulComponent, TextInput, UiComponent, VisualNode } from "../runtime-exports";

export class DatePicker extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Components.DatePicker';
    _open: boolean = false;
    _typing: any = null;
    _reported: any = null;
    _disabled: boolean = false;
    _labelHidden: boolean = false;
    _composed: boolean = false;
    declare selected: any;
    declare onChanged: ((dateOnly: DateOnly) => void) | null;
    declare min: any;
    declare max: any;
    declare label: string;

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

    get composed() {
        return this._composed;
    }

    set composed(value) {
        this._composed = value;
    }

    constructor(selected: any = null, onChanged: any = null, min: any = null, max: any = null, label: any = '', props?: any) {
        super();
        if (selected !== undefined) this.selected = selected;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (min !== undefined) this.min = min;
        if (max !== undefined) this.max = max;
        if (label !== undefined) this.label = label;
        this.selected = selected;
        this.onChanged = onChanged;
        this.min = min;
        this.max = max;
        this.label = label;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let value: any; 
        let shown = this._typing ?? ((value = this.selected) != null ? DatePicker.format(value) : '');
        let typed: any; 
        let invalid = (($0) => (($0 != null && $0.length > 0) && (typed = $0, true)))(this._typing) && DatePicker.parse(typed) == null;
        let slot = theme.calendar;
        let panel = new Box(new BoxStyle({ background: slot?.panelFill ?? theme.surface, cornerRadius: new CornerRadii(slot?.panelRadius ?? theme.shape('medium')), borderWidth: slot?.panelBorderWidth ?? 1, borderColor: slot?.panelBorderColor ?? theme.border, elevation: slot?.panelElevation ?? 2, padding: EdgeInsets.all(slot?.panelPadding ?? 12) }), new Calendar(this.selected, $eq.delegates.bound(this, this.pick), this.min, this.max));
        let opener: VisualNode = new Anchored(new Pressable(new Icon(IconGlyph.fromIcons('calendar'), slot?.openerIconSize ?? 20, slot?.openerColor ?? theme.textSecondary), $eq.delegates.bound(this, this.toggle), { label: SdkStrings.chooseDate, expanded: this._open }), panel, { open: this._open && !this.disabled, onDismiss: $eq.delegates.bound(this, this.close).bind(this), panelRole: 'dialog' });
        opener = new Shortcut(opener, KeyChord.escape, $eq.delegates.bound(this, this.close), { enabled: this._open && !this.disabled });
        return new TextInput(shown, $eq.delegates.bound(this, this.type), this.label, SdkStrings.dateFormatHint, null, invalid ? SdkStrings.dateFormatHint : null, null, 'large', this.disabled ? null : opener, 'text', null, null, { disabled: this.disabled, labelHidden: this.labelHidden });
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof DatePicker && (fresh = next, true)))) return;
        let said: any; 
        if ((said = this._reported) != null && fresh.selected == null && !this.composed) DevelopmentNotices.say(`A DatePicker reported ${$eq.text.format(said, 'd')} through onChanged and was drawn again with no date, ` + 'so its field emptied: a picker shows the date it is handed. Hand it back the day onChanged reports.');
        this._reported = null;
        let buffer: any; 
        if ((buffer = this._typing) != null && !$eq.equals(DatePicker.parse(buffer), fresh.selected)) this._typing = null;
        this.selected = fresh.selected;
        this.onChanged = fresh.onChanged;
        this.min = fresh.min;
        this.max = fresh.max;
        this.label = fresh.label;
        this._disabled = fresh.disabled;
        this._labelHidden = fresh.labelHidden;
        this._composed = fresh.composed;
    }

    pick(day: DateOnly) {
        this._reported = day;
        this.onChanged?.(day);
        this.setState(() => {
            this._open = false;
            this._typing = null;
        });
    }

    type(text: string) {
        let parsed = DatePicker.parse(text);
        let day: any; 
        if ((day = parsed) != null && this.inRange(day)) {
            this._reported = day;
            this.onChanged?.(day);
        }
        this.setState(() => this._typing = text);
    }

    toggle() {
        return this.setState(() => this._open = !this._open);
    }

    close() {
        return this.setState(() => this._open = false);
    }

    inRange(day: DateOnly) {
        let min: any; let max: any; 
        return (!((min = this.min) != null) || (day.compareTo(min) >= 0)) && (!((max = this.max) != null) || (day.compareTo(max) <= 0));
    }

    static format(day: DateOnly) {
        return `${$eq.text.format(day, 'd')}`;
    }

    static parse(text: string) {
        let day: any; 
        return ((day = $eq.time.dateOnly.tryParse(text)) !== undefined || ((day = $eq.time.dateOnly.minValue()), false)) ? day : null;
    }
}

