import { $eq, Box, BoxStyle, BuildContext, Column, Component, ComponentContext, CornerRadii, EdgeInsets, Flexible, HtmlElement, Pressable, Row, SizeValue, Space, StatelessComponent, Text, TypeStyle, VariantColors, VisualNode } from "../runtime-exports";

export class Tabs extends StatelessComponent {
    constructor(labels?: any, selected?: any, onSelect: any = null, props?: any) {
        super(props);
        if (labels !== undefined) this.labels = labels;
        if (selected !== undefined) this.selected = selected;
        if (onSelect !== undefined) this.onSelect = onSelect;
        this.labels = labels;this.selected = selected;this.onSelect = onSelect;
    }

    build(context: BuildContext) {
        let theme = context.theme;let primary = theme.colors('primary');let row = new Row(0, { width: SizeValue.fill, height: 48 });for (let i = 0; i < this.labels.length; i++) {let isActive = i === this.selected;let index = i;let label = new Text(this.labels[i], 'caption', isActive ? primary.base : theme.textMuted, 1, { styleOverride: new TypeStyle(14, 18, isActive ? 'bold' : 'semiBold', 0.1, 1.3) });let labelRow = new Row(0, { main: 'center', height: SizeValue.fill });labelRow.add(label);let cell = new Column(0, { height: SizeValue.fill });cell.add(new Flexible(labelRow));cell.add(new Box(new BoxStyle({ width: SizeValue.fill, height: 3, padding: EdgeInsets.symmetric(Space.s4, 0) }), isActive ? new Box(new BoxStyle({ width: SizeValue.fill, height: 3, background: primary.base, cornerRadius: new CornerRadii(2, 2, 0, 0) })) : null));row.add(new Flexible(new Pressable(cell, this.onSelect === null ? null : () => this.onSelect(index), { label: this.labels[i], pressedBackground: theme.surfaceSubtle })));}return row;
    }

}

