import { Adjustable, Box, BoxStyle, BuildContext, Column, CornerRadii, Pressable, Row, SizeValue, Sizing, StatelessComponent, Text, VisualNode } from "../runtime-exports";

export class RadioGroup extends StatelessComponent {
    declare options: string[];
    declare selected: number;
    declare onChanged: any;
    declare label: any;
    declare disabled: boolean;
    constructor(options?: any, selected?: any, onChanged: any = null, label: any = null, props?: any) {
        super();
        if (options !== undefined) this.options = options;
        if (selected !== undefined) this.selected = selected;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (label !== undefined) this.label = label;
        if (this.selected === undefined) this.selected = 0;
        if (this.disabled === undefined) this.disabled = false;
        this.options = options;this.selected = selected;this.onChanged = onChanged;this.label = label;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let primary = theme.colors('primary');let options = new Column(4, 'start', 'stretch', false, null, null, { width: SizeValue.fill });for (let i = 0; i < this.options.length; i++) {let isSelected = i === this.selected;let index = i;let circleContent = isSelected ? new Box(new BoxStyle({ width: Sizing.radioDot(context.density), height: Sizing.radioDot(context.density), background: primary.base, cornerRadius: new CornerRadii(theme.shape('full')) })).centered() : null;let circle = new Box(new BoxStyle({ width: Sizing.selectionBox(context.density), height: Sizing.selectionBox(context.density), cornerRadius: new CornerRadii(theme.shape('full')), borderWidth: 2, borderColor: isSelected ? primary.base : theme.borderStrong }), circleContent);let row = new Row(12, 'start', 'center', false, null, null, { cross: 'center', width: SizeValue.fill, height: 44 });row.add(circle);row.add(new Text(this.options[i], 'bodyM', this.disabled ? theme.textMuted : theme.textPrimary, 1));options.add(new Pressable(row, this.disabled || this.onChanged == null || isSelected ? null : () => this.onChanged(index), { disabled: this.disabled, label: this.options[i], role: 'radio', selected: isSelected }));}let group: VisualNode = options;if (!this.disabled && !(this.onChanged == null) && this.options.length > 0) {let count = this.options.length;group = new Adjustable(options, (direction: number) => this.onChanged((this.selected + direction + count) % count), { role: 'radiogroup', label: this.label });}let column = new Column(4, 'start', 'stretch', false, null, null, { width: SizeValue.fill });let groupLabel: any; if ((groupLabel = this.label) != null) column.add(new Text(groupLabel, 'caption', theme.textMuted, 1));column.add(group);return column;
    }

}

