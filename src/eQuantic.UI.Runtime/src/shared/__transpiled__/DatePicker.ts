import { $eq, Anchored, Box, BoxStyle, BuildContext, Calendar, CornerRadii, DateOnly, EdgeInsets, KeyChord, Pressable, SdkStrings, SharedStatefulComponent, Shortcut, TextInput, UiComponent, VisualNode } from "@equantic/runtime";

export class DatePicker extends SharedStatefulComponent {
    _open: boolean = false;
    _typing: any;
    declare selected: any;
    declare onChanged: any;
    declare min: any;
    declare max: any;
    declare label: string;
    declare disabled: boolean;

    constructor(selected: any = null, onChanged: any = null, min: any = null, max: any = null, label: any = '', props?: any) {
        super();
        if (selected !== undefined) this.selected = selected;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (min !== undefined) this.min = min;
        if (max !== undefined) this.max = max;
        if (label !== undefined) this.label = label;
        if (this.disabled === undefined) this.disabled = false;
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
        let invalid = ((this._typing != null && this._typing.length > 0) && (typed = this._typing, true)) && DatePicker.parse(typed) == null;
        let field = new TextInput(shown, this.type.bind(this), this.label, SdkStrings.dateFormatHint, null, invalid ? SdkStrings.dateFormatHint : null, 'calendar', 'large', { disabled: this.disabled });
        let trigger = this.disabled ? field : new Pressable(field, this.toggle.bind(this), { label: this.label.length > 0 ? this.label : SdkStrings.chooseDate, expanded: this._open });
        let panel = new Box(new BoxStyle({ background: theme.surface, cornerRadius: new CornerRadii(theme.shape('medium')), borderWidth: 1, borderColor: theme.border, elevation: 2, padding: EdgeInsets.all(12) }), new Calendar(this.selected, this.pick.bind(this), this.min, this.max));
        let picker: VisualNode = new Anchored(trigger, panel, { open: this._open && !this.disabled, onDismiss: this.close.bind(this), panelRole: 'dialog' });
        if (this._open && !this.disabled) picker = new Shortcut(picker, KeyChord.escape, this.close.bind(this));
        return picker;
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof DatePicker && (fresh = next, true)))) return;
        this.selected = fresh.selected;
        this.onChanged = fresh.onChanged;
        this.min = fresh.min;
        this.max = fresh.max;
        this.label = fresh.label;
    }

    pick(day: DateOnly) {
        this.onChanged?.(day);
        this.setState(() => {
            this._open = false;
            this._typing = null;
        });
    }

    type(text: string) {
        let parsed = DatePicker.parse(text);
        let day: any; 
        if ((day = parsed) != null && this.inRange(day)) this.onChanged?.(day);
        this.setState(() => this._typing = text);
    }

    toggle() {
        return this.setState(() => this._open = !this._open);
    }

    close() {
        return this.setState(() => this._open = false);
    }

    inRange(day: DateOnly) {
        let min: any; let max: any; return (!((min = this.min) != null) || (day.compareTo(min) >= 0)) && (!((max = this.max) != null) || (day.compareTo(max) <= 0));
    }

    static format(day: DateOnly) {
        return `${$eq.text.format(day, 'd')}`;
    }

    static parse(text: string) {
        try {
            return $eq.time.dateOnly.parse(text);
        } catch {
            return null;
        }
    }
}

