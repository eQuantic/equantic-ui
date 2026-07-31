import { Anchored, Box, BoxStyle, BuildContext, Column, CornerRadii, EdgeInsets, Flexible, Icon, IconSize, Pressable, Row, SharedStatefulComponent, SizeValue, Space, Spacer, StyleDiff, Text, UiComponent } from "../runtime-exports";

export class Select extends SharedStatefulComponent {
    _open: boolean = false;
    declare options: any;
    declare selectedIndex: number;
    declare onChanged: any;
    declare placeholder: string;
    declare disabled: boolean;
    constructor(options?: any, selectedIndex: any = -1, onChanged: any = null, placeholder: any = null, props?: any) {
        super(props);
        if (options !== undefined) this.options = options;
        if (selectedIndex !== undefined) this.selectedIndex = selectedIndex;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (placeholder !== undefined) this.placeholder = placeholder;
        this.options = options;this.selectedIndex = selectedIndex;this.onChanged = onChanged;this.placeholder = placeholder;
    }

    build(context: BuildContext) {
        let theme = context.theme;let hasValue = this.selectedIndex >= 0 && this.selectedIndex < this.options.length;let fieldRow = new Row(Space.s2, { cross: 'center', width: SizeValue.fill, height: SizeValue.fill });fieldRow.add(new Text(hasValue ? this.options[this.selectedIndex] : this.placeholder ?? 'Select…', 'bodyM', hasValue ? theme.textPrimary : theme.textMuted, 1));fieldRow.add(new Flexible(new Spacer()));fieldRow.add(new Icon('chevronDown', IconSize.sm, theme.textSecondary));let field = new Box(new BoxStyle({ height: 40, width: SizeValue.fill, padding: EdgeInsets.symmetric(Space.s3, 0), background: theme.surface, cornerRadius: new CornerRadii(theme.shape('medium')), borderWidth: 1, borderColor: theme.borderStrong, opacity: this.disabled ? theme.disabledOpacity : null, hover: this.disabled ? null : new StyleDiff({ borderColor: theme.colors('primary').base }) }), fieldRow);let list = new Column(0, { width: SizeValue.fill });for (let i = 0; i < this.options.length; i++) {let index = i;let selected = i === this.selectedIndex;let row = new Row(Space.s2, { cross: 'center', width: SizeValue.fill, height: SizeValue.fill });row.add(new Text(this.options[i], 'bodyM', selected ? theme.colors('primary').onSubtle : theme.textPrimary, 1));if (selected) {row.add(new Flexible(new Spacer()));row.add(new Icon('check', IconSize.sm, theme.colors('primary').onSubtle));}list.add(new Pressable(new Box(new BoxStyle({ height: 40, padding: EdgeInsets.symmetric(Space.s3, 0), width: SizeValue.fill, background: selected ? theme.colors('primary').subtle : null, hover: selected ? null : new StyleDiff({ background: theme.surfaceSubtle }) }), row), () => {this.onChanged?.(index);this.setState(() => this._open = false);}));}let panel = new Box(new BoxStyle({ background: theme.surface, cornerRadius: new CornerRadii(theme.shape('medium')), borderWidth: 1, borderColor: theme.border, elevation: 2, padding: EdgeInsets.symmetric(0, Space.s1), clip: true }), list);let trigger = this.disabled ? field : new Pressable(field, () => this.setState(() => this._open = !this._open));return new Anchored(trigger, panel, { open: this._open && !this.disabled, onDismiss: () => this.setState(() => this._open = false), matchAnchorWidth: true });
    }

    adoptConfig(next: UiComponent) {
        let fresh; if (!((next instanceof Select && (fresh = next, true)))) return;this.options = fresh.options;this.selectedIndex = fresh.selectedIndex;this.onChanged = fresh.onChanged;this.placeholder = fresh.placeholder;
    }

}

