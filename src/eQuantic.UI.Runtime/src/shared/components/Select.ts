import { $eq, Anchored, Box, BoxStyle, BuildContext, Column, ControlMetrics, CornerRadii, EdgeInsets, Flexible, Group, Icon, IconGlyph, KeyChord, OptionGroup, Pressable, Row, Shortcut, SizeValue, Spacer, StatefulComponent, StyleDiff, Text, UiComponent, VisualNode } from "../runtime-exports";

export class Select extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Components.Select';
    _open: boolean = false;
    _highlight: number = 0;
    _choices: string[];
    _disabled: boolean = false;
    declare options: string[];
    declare groups: OptionGroup[];
    declare selectedIndex: number;
    declare onChanged: ((int: number) => void) | null;
    declare placeholder: any;

    get disabled() {
        return this._disabled;
    }

    set disabled(value) {
        this._disabled = value;
    }

    constructor(options: any = null, selectedIndex: any = -1, onChanged: any = null, placeholder: any = null, groups: any = null, props?: any) {
        super();
        if (options !== undefined) this.options = options;
        if (selectedIndex !== undefined) this.selectedIndex = selectedIndex;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (placeholder !== undefined) this.placeholder = placeholder;
        if (groups !== undefined) this.groups = groups;
        if (this.selectedIndex === undefined) this.selectedIndex = 0;
        this.options = options ?? [];
        this.selectedIndex = selectedIndex;
        this.onChanged = onChanged;
        this.placeholder = placeholder;
        this.groups = groups ?? [];
        this._choices = Select.flatten(this.options, this.groups);
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let slot = theme.input;
        let height = ControlMetrics.height(context, 'medium', (slot?.controls ?? null));
        let paddingX = slot?.pickerPaddingX ?? 12;
        let gap = slot?.pickerGap ?? 8;
        let radius = slot?.radius ?? theme.shape('medium');
        let borderWidth = slot?.borderWidth ?? 1;
        let rowHeight = slot?.panelRowHeight ?? height;
        let choices = this._choices;
        let hasValue = this.selectedIndex >= 0 && this.selectedIndex < $eq.collections.count(choices);
        let fieldRow = new Row(gap, 'start', 'center', false, null, null, { cross: 'center', width: SizeValue.fill, height: SizeValue.fill });
        fieldRow.add(new Text(hasValue ? $eq.collections.item(choices, this.selectedIndex) : this.placeholder ?? 'Select…', 'bodyM', hasValue ? theme.textPrimary : theme.textMuted, 1));
        fieldRow.add(new Flexible(new Spacer()));
        fieldRow.add(new Icon(IconGlyph.fromIcons('chevronDown'), 16, theme.textSecondary));
        let field = new Box(new BoxStyle({ height: height, width: SizeValue.fill, padding: EdgeInsets.symmetric(paddingX, 0), background: slot?.fill ?? theme.surface, cornerRadius: new CornerRadii(radius), borderWidth: borderWidth, borderColor: slot?.borderColor ?? theme.borderStrong, opacity: this.disabled ? theme.disabledOpacity : null, hover: this.disabled ? null : new StyleDiff({ borderColor: theme.colors('primary').base }) }), fieldRow);
        let list = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        let index = 0;
        for (const option of this.options) list.add(this.optionRow(theme, option, index++, gap, paddingX, rowHeight));
        for (const group of this.groups) {
            let rows = list;
            if (!(!$eq.text.hasNonWhiteSpace(group.label))) {
                rows = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
                rows.add(new Box(new BoxStyle({ padding: EdgeInsets.symmetric(paddingX, 4), width: SizeValue.fill }), new Text(group.label, 'caption', theme.textMuted, 1, 'start', false, false, null, 0, { excludeFromSemantics: true })));
                list.add(new Group(rows, { label: group.label }));
            }
            for (const option of group.options) rows.add(this.optionRow(theme, option, index++, gap, paddingX, rowHeight));
        }
        let panel = new Box(new BoxStyle({ background: theme.surface, cornerRadius: new CornerRadii(radius), borderWidth: borderWidth, borderColor: slot?.panelBorderColor ?? theme.border, elevation: slot?.panelElevation ?? 2, padding: EdgeInsets.symmetric(0, 4), clip: true }), list);
        let trigger = new Pressable(field, this.disabled ? null : $eq.delegates.bound(this, this.toggle), { disabled: this.disabled, expanded: this._open && !this.disabled });
        let select: VisualNode = new Anchored(trigger, panel, { open: this._open && !this.disabled, onDismiss: () => this.setState(() => this._open = false), matchAnchorWidth: true, panelRole: 'listbox', activeIndex: this._open && $eq.collections.count(this._choices) > 0 ? this._highlight : -1 });
        let live = this._open && !this.disabled && $eq.collections.count(choices) > 0;
        select = new Shortcut(select, KeyChord.arrowDown, () => this.setState(() => this._highlight = Math.min($eq.collections.count(choices) - 1, this._highlight + 1)), { enabled: live });
        select = new Shortcut(select, KeyChord.arrowUp, () => this.setState(() => this._highlight = Math.max(0, this._highlight - 1)), { enabled: live });
        select = new Shortcut(select, KeyChord.enter, () => this.choose(this._highlight), { enabled: live });
        return select;
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof Select && (fresh = next, true)))) return;
        this.options = fresh.options;
        this.selectedIndex = fresh.selectedIndex;
        this.onChanged = fresh.onChanged;
        this.placeholder = fresh.placeholder;
        this.groups = fresh.groups;
        this._choices = fresh._choices;
        this._highlight = Math.max(0, Math.min(this._highlight, $eq.collections.count(this._choices) - 1));
        this._disabled = fresh.disabled;
    }

    static flatten(options: string[], groups: OptionGroup[]) {
        return [...options, ...groups.flatMap((group) => group.options)];
    }

    optionRow(theme: any, option: string, index: number, gap: number, paddingX: number, rowHeight: number) {
        let selected = index === this.selectedIndex;
        let row = new Row(gap, 'start', 'center', false, null, null, { cross: 'center', width: SizeValue.fill, height: SizeValue.fill });
        row.add(new Text(option, 'bodyM', selected ? theme.colors('primary').onSubtle : theme.textPrimary, 1));
        if (selected) {
            row.add(new Flexible(new Spacer()));
            row.add(new Icon(IconGlyph.fromIcons('check'), 16, theme.colors('primary').onSubtle));
        }
        let highlighted = this._open && index === this._highlight;
        return new Pressable(new Box(new BoxStyle({ height: rowHeight, padding: EdgeInsets.symmetric(paddingX, 0), width: SizeValue.fill, background: selected ? theme.colors('primary').subtle : highlighted ? theme.surfaceSubtle : null, hover: selected ? null : new StyleDiff({ background: theme.surfaceSubtle }) }), row), () => this.choose(index), { role: 'option', selected: selected });
    }

    toggle() {
        return this.setState(() => {
            this._open = !this._open;
            if (this._open) this._highlight = this.selectedIndex >= 0 && this.selectedIndex < $eq.collections.count(this._choices) ? this.selectedIndex : 0;
        });
    }

    choose(index: number) {
        if (index < 0 || index >= $eq.collections.count(this._choices)) return;
        this.onChanged?.(index);
        this.setState(() => this._open = false);
    }
}

