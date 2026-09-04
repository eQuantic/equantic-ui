import { Anchored, Box, BoxStyle, BuildContext, Column, CornerRadii, EdgeInsets, Flexible, Icon, KeyChord, Pressable, Row, Shortcut, SizeValue, Sizing, Spacer, StatefulComponent, StyleDiff, Text, UiComponent, VisualNode } from "../runtime-exports";

export class Select extends StatefulComponent {
    _open: boolean = false;
    _highlight: number = 0;
    declare options: string[];
    declare selectedIndex: number;
    declare onChanged: any;
    declare placeholder: any;
    declare disabled: boolean;

    constructor(options?: any, selectedIndex: any = -1, onChanged: any = null, placeholder: any = null, props?: any) {
        super();
        if (options !== undefined) this.options = options;
        if (selectedIndex !== undefined) this.selectedIndex = selectedIndex;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (placeholder !== undefined) this.placeholder = placeholder;
        if (this.selectedIndex === undefined) this.selectedIndex = 0;
        if (this.disabled === undefined) this.disabled = false;
        this.options = options;
        this.selectedIndex = selectedIndex;
        this.onChanged = onChanged;
        this.placeholder = placeholder;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let hasValue = this.selectedIndex >= 0 && this.selectedIndex < this.options.length;
        let fieldRow = new Row(8, 'start', 'center', false, null, null, { cross: 'center', width: SizeValue.fill, height: SizeValue.fill });
        fieldRow.add(new Text(hasValue ? this.options[this.selectedIndex] : this.placeholder ?? 'Select…', 'bodyM', hasValue ? theme.textPrimary : theme.textMuted, 1));
        fieldRow.add(new Flexible(new Spacer()));
        fieldRow.add(new Icon('chevronDown', 16, theme.textSecondary));
        let field = new Box(new BoxStyle({ height: Sizing.height('medium', context.density), width: SizeValue.fill, padding: EdgeInsets.symmetric(12, 0), background: theme.surface, cornerRadius: new CornerRadii(theme.shape('medium')), borderWidth: 1, borderColor: theme.borderStrong, opacity: this.disabled ? theme.disabledOpacity : null, hover: this.disabled ? null : new StyleDiff({ borderColor: theme.colors('primary').base }) }), fieldRow);
        let list = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        for (let i = 0; i < this.options.length; i++) {
            let index = i;
            let selected = i === this.selectedIndex;
            let row = new Row(8, 'start', 'center', false, null, null, { cross: 'center', width: SizeValue.fill, height: SizeValue.fill });
            row.add(new Text(this.options[i], 'bodyM', selected ? theme.colors('primary').onSubtle : theme.textPrimary, 1));
            if (selected) {
                row.add(new Flexible(new Spacer()));
                row.add(new Icon('check', 16, theme.colors('primary').onSubtle));
            }
            let highlighted = this._open && i === this._highlight;
            list.add(new Pressable(new Box(new BoxStyle({ height: Sizing.height('medium', context.density), padding: EdgeInsets.symmetric(12, 0), width: SizeValue.fill, background: selected ? theme.colors('primary').subtle : highlighted ? theme.surfaceSubtle : null, hover: selected ? null : new StyleDiff({ background: theme.surfaceSubtle }) }), row), () => this.choose(index), { role: 'option', selected: selected }));
        }
        let panel = new Box(new BoxStyle({ background: theme.surface, cornerRadius: new CornerRadii(theme.shape('medium')), borderWidth: 1, borderColor: theme.border, elevation: 2, padding: EdgeInsets.symmetric(0, 4), clip: true }), list);
        let trigger = this.disabled ? field : new Pressable(field, this.toggle.bind(this), { expanded: this._open });
        let select: VisualNode = new Anchored(trigger, panel, { open: this._open && !this.disabled, onDismiss: () => this.setState(() => this._open = false), matchAnchorWidth: true, panelRole: 'listbox', activeIndex: this._open ? this._highlight : -1 });
        if (this._open && !this.disabled && this.options.length > 0) {
            select = new Shortcut(select, KeyChord.arrowDown, () => this.setState(() => this._highlight = Math.min(this.options.length - 1, this._highlight + 1)));
            select = new Shortcut(select, KeyChord.arrowUp, () => this.setState(() => this._highlight = Math.max(0, this._highlight - 1)));
            select = new Shortcut(select, KeyChord.enter, () => this.choose(this._highlight));
        }
        return select;
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof Select && (fresh = next, true)))) return;
        this.options = fresh.options;
        this.selectedIndex = fresh.selectedIndex;
        this.onChanged = fresh.onChanged;
        this.placeholder = fresh.placeholder;
    }

    toggle() {
        return this.setState(() => {
            this._open = !this._open;
            if (this._open) this._highlight = this.selectedIndex >= 0 && this.selectedIndex < this.options.length ? this.selectedIndex : 0;
        });
    }

    choose(index: number) {
        if (index < 0 || index >= this.options.length) return;
        this.onChanged?.(index);
        this.setState(() => this._open = false);
    }
}

