import { Box, BoxStyle, BuildContext, Column, CornerRadii, Pressable, Row, SizeValue, Sizing, StatelessComponent, Text } from "../runtime-exports";

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
        this.options = options;this.selected = selected;this.onChanged = onChanged;this.label = label;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let primary = theme.colors('primary');let column = new Column(4, 'start', 'stretch', false, null, null, { width: SizeValue.fill });let groupLabel: any; if (((this.label != null) && (groupLabel = this.label, true))) column.add(new Text(groupLabel, 'caption', theme.textMuted, 1));for (let i = 0; i < this.options.length; i++) {let isSelected = i === this.selected;let index = i;let circleContent = isSelected ? new Box(new BoxStyle({ width: Sizing.radioDot(context.density), height: Sizing.radioDot(context.density), background: primary.base, cornerRadius: new CornerRadii(theme.shape('full')) })).centered() : null;let circle = new Box(new BoxStyle({ width: Sizing.selectionBox(context.density), height: Sizing.selectionBox(context.density), cornerRadius: new CornerRadii(theme.shape('full')), borderWidth: 2, borderColor: isSelected ? primary.base : theme.borderStrong }), circleContent);let row = new Row(12, 'start', 'center', false, null, null, { cross: 'center', width: SizeValue.fill, height: 44 });row.add(circle);row.add(new Text(this.options[i], 'bodyM', this.disabled ? theme.textMuted : theme.textPrimary, 1));column.add(new Pressable(row, this.disabled || this.onChanged == null ? null : () => this.onChanged(index), { disabled: this.disabled, label: this.options[i] }));}return column;
    }

}

