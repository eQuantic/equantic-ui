import { $eq, Anchored, Box, BoxStyle, BuildContext, Column, CornerRadii, EdgeInsets, Flexible, Icon, KeyChord, Pressable, Row, ScrollView, SdkStrings, SharedStatefulComponent, Shortcut, SizeValue, Sizing, Spacer, StyleDiff, Text, TimeOnly, UiComponent, VisualNode } from "@equantic/runtime";

export class TimePicker extends SharedStatefulComponent {
    _open: boolean = false;
    _highlight: number = 0;
    static panelHeight: number = 260;
    static $hydration = { selected: 'timeOnly', min: 'timeOnly', max: 'timeOnly' };
    declare selected: any;
    declare onChanged: any;
    declare stepMinutes: number;
    declare min: any;
    declare max: any;
    declare label: string;
    declare disabled: boolean;

    constructor(selected: any = null, onChanged: any = null, stepMinutes: any = 30, min: any = null, max: any = null, label: any = '', props?: any) {
        super();
        if (selected !== undefined) this.selected = selected;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (stepMinutes !== undefined) this.stepMinutes = stepMinutes;
        if (min !== undefined) this.min = min;
        if (max !== undefined) this.max = max;
        if (label !== undefined) this.label = label;
        if (this.stepMinutes === undefined) this.stepMinutes = 0;
        if (this.disabled === undefined) this.disabled = false;
        this.selected = selected;
        this.onChanged = onChanged;
        this.stepMinutes = TimePicker.step(stepMinutes);
        this.min = min;
        this.max = max;
        this.label = label;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let open = this._open && !this.disabled;
        let times = open ? this.slots() : [];
        let highlight = times.length > 0 ? Math.min(this._highlight, times.length - 1) : -1;
        let field = new Row(8, 'start', 'center', false, null, null, { cross: 'center', width: SizeValue.fill, height: SizeValue.fill });
        field.add(new Icon('clock', 20, theme.textMuted));
        let value: any; 
        field.add(new Text((value = this.selected) != null ? TimePicker.format(value) : SdkStrings.chooseTime, 'bodyM', this.selected == null ? theme.textMuted : theme.textPrimary, 1));
        field.add(new Flexible(new Spacer()));
        field.add(new Icon('chevronDown', 16, theme.textSecondary));
        let box = new Box(new BoxStyle({ height: Sizing.height('medium', context.density), width: SizeValue.fill, padding: EdgeInsets.symmetric(12, 0), background: theme.surface, cornerRadius: new CornerRadii(theme.shape('medium')), borderWidth: 1, borderColor: theme.borderStrong, opacity: this.disabled ? theme.disabledOpacity : null, hover: this.disabled ? null : new StyleDiff({ borderColor: theme.colors('primary').base }) }), field);
        let list = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        for (let i = 0; i < times.length; i++) {
            let slot = times[i];
            let picked = $eq.equals(this.selected, slot);
            let highlighted = i === highlight;
            let row = new Row(8, 'start', 'center', false, null, null, { cross: 'center', width: SizeValue.fill, height: SizeValue.fill });
            row.add(new Text(TimePicker.format(slot), 'bodyM', picked ? theme.colors('primary').onSubtle : theme.textPrimary, 1));
            list.add(new Pressable(new Box(new BoxStyle({ height: Sizing.height('medium', context.density), padding: EdgeInsets.symmetric(12, 0), width: SizeValue.fill, background: picked ? theme.colors('primary').subtle : highlighted ? theme.surfaceSubtle : null, hover: picked ? null : new StyleDiff({ background: theme.surfaceSubtle }) }), row), () => this.pick(slot), { role: 'option', selected: picked }));
        }
        let panel = new Box(new BoxStyle({ background: theme.surface, cornerRadius: new CornerRadii(theme.shape('medium')), borderWidth: 1, borderColor: theme.border, elevation: 2, padding: EdgeInsets.symmetric(0, 4), height: SizeValue.fixed(TimePicker.panelHeight), clip: true }), new ScrollView(list));
        let picker: VisualNode = new Anchored(this.disabled ? box : new Pressable(box, this.toggle.bind(this), { label: this.label.length > 0 ? this.label : SdkStrings.chooseTime, expanded: this._open }), panel, { open: open, onDismiss: this.close.bind(this), matchAnchorWidth: true, panelRole: 'listbox', activeIndex: open ? highlight : -1 });
        if (open) {
            picker = new Shortcut(picker, KeyChord.escape, this.close.bind(this));
            if (highlight >= 0) {
                picker = new Shortcut(picker, KeyChord.arrowDown, () => this.setState(() => this._highlight = Math.min(times.length - 1, highlight + 1)));
                picker = new Shortcut(picker, KeyChord.arrowUp, () => this.setState(() => this._highlight = Math.max(0, highlight - 1)));
                picker = new Shortcut(picker, KeyChord.enter, () => this.pick(times[highlight]));
            }
        }
        return picker;
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof TimePicker && (fresh = next, true)))) return;
        this.selected = fresh.selected;
        this.onChanged = fresh.onChanged;
        this.stepMinutes = TimePicker.step(fresh.stepMinutes);
        this.min = fresh.min;
        this.max = fresh.max;
        this.label = fresh.label;
    }

    static step(minutes: number) {
        return minutes < 1 ? 1 : minutes;
    }

    slots() {
        let slots: TimeOnly[] = [];
        let first = this.min ?? $eq.time.timeOnly(0, 0);
        let last = this.max ?? $eq.time.timeOnly(23, 59);
        for (let at = first; (at.compareTo(last) <= 0); at = at.addMinutes(this.stepMinutes)) {
            slots.push(at);
            if ((at.addMinutes(this.stepMinutes).compareTo(at) <= 0)) break;
        }
        return slots;
    }

    pick(time: TimeOnly) {
        this.onChanged?.(time);
        this.setState(() => this._open = false);
    }

    toggle() {
        return this.setState(() => {
            this._open = !this._open;
            if (this._open) {
                let value: any; 
                this._highlight = (value = this.selected) != null ? this.slotOf(value) : 0;
            }
        });
    }

    slotOf(value: TimeOnly) {
        let times = this.slots();
        for (let i = 0; i < times.length; i++) {
            if (times[i].equals(value)) return i;
        }
        return 0;
    }

    close() {
        return this.setState(() => this._open = false);
    }

    static format(time: TimeOnly) {
        return `${$eq.text.format(time, 't')}`;
    }
}

