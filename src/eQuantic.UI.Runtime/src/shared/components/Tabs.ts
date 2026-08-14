import { Adjustable, Box, BoxStyle, BuildContext, Column, CornerRadii, EdgeInsets, Flexible, Pressable, Row, SizeValue, StatelessComponent, Text, TypeStyle } from "../runtime-exports";

export class Tabs extends StatelessComponent {
    declare labels: string[];
    declare selected: number;
    declare onSelect: any;
    constructor(labels?: any, selected?: any, onSelect: any = null, props?: any) {
        super();
        if (labels !== undefined) this.labels = labels;
        if (selected !== undefined) this.selected = selected;
        if (onSelect !== undefined) this.onSelect = onSelect;
        if (this.selected === undefined) this.selected = 0;
        this.labels = labels;this.selected = selected;this.onSelect = onSelect;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let primary = theme.colors('primary');let row = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, height: 48 });for (let i = 0; i < this.labels.length; i++) {let isActive = i === this.selected;let index = i;let label = new Text(this.labels[i], 'caption', isActive ? primary.base : theme.textMuted, 1, 'start', false, false, null, { styleOverride: new TypeStyle(14, 18, isActive ? 'bold' : 'semiBold', 0.1, 1.3) });let labelRow = new Row(0, 'start', 'center', false, null, null, { main: 'center', height: SizeValue.fill });labelRow.add(label);let cell = new Column(0, 'start', 'stretch', false, null, null, { height: SizeValue.fill });cell.add(new Flexible(labelRow));cell.add(new Box(new BoxStyle({ width: SizeValue.fill, height: 3, padding: EdgeInsets.symmetric(16, 0) }), isActive ? new Box(new BoxStyle({ width: SizeValue.fill, height: 3, background: primary.base, cornerRadius: new CornerRadii(2, 2, 0, 0) })) : null));row.add(new Flexible(new Pressable(cell, this.onSelect == null ? null : () => this.onSelect(index), { label: this.labels[i], pressedBackground: theme.surfaceSubtle, role: 'tab', selected: isActive })));}if (this.onSelect == null || this.labels.length === 0) return row;let count = this.labels.length;return new Adjustable(row, (direction: number) => this.onSelect((this.selected + direction + count) % count), { role: 'tablist' });
    }

}

