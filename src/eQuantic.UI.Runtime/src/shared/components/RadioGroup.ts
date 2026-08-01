import { Box, BoxStyle, BuildContext, Column, CornerRadii, Pressable, Row, SizeValue, StatelessComponent, Text } from "../runtime-exports";

export class RadioGroup extends StatelessComponent {
    declare options: any;
    declare selected: number;
    declare onChanged: any;
    declare label: string;
    declare disabled: boolean;
    constructor(options?: any, selected?: any, onChanged: any = null, label: any = null, props?: any) {
        super(props);
        if (options !== undefined) this.options = options;
        if (selected !== undefined) this.selected = selected;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (label !== undefined) this.label = label;
        this.options = options;this.selected = selected;this.onChanged = onChanged;this.label = label;
    }

    build(context: BuildContext) {
        let theme = context.theme;let primary = theme.colors('primary');let column = new Column(4, { width: SizeValue.fill });let groupLabel; if (((this.label != null) && (groupLabel = this.label, true))) column.add(new Text(groupLabel, 'caption', theme.textMuted, 1));for (let i = 0; i < this.options.length; i++) {let isSelected = i === this.selected;let index = i;let circleContent = new Row(0, { main: 'center', height: SizeValue.fill });if (isSelected) {circleContent.add(new Box(new BoxStyle({ width: 10, height: 10, background: primary.base, cornerRadius: new CornerRadii(theme.shape('full')) })));}let circle = new Box(new BoxStyle({ width: 22, height: 22, cornerRadius: new CornerRadii(theme.shape('full')), borderWidth: 2, borderColor: isSelected ? primary.base : theme.borderStrong }), circleContent);let row = new Row(12, { cross: 'center', width: SizeValue.fill, height: 44 });row.add(circle);row.add(new Text(this.options[i], 'bodyM', this.disabled ? theme.textMuted : theme.textPrimary, 1));column.add(new Pressable(row, this.disabled || this.onChanged == null ? null : () => this.onChanged(index), { disabled: this.disabled, label: this.options[i] }));}return column;
    }

}

