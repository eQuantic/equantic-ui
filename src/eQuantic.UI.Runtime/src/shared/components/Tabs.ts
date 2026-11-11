import { $eq, Adjustable, Box, BoxStyle, BuildContext, Column, CornerRadii, EdgeInsets, Flexible, Pressable, Row, SizeValue, StatelessComponent, StyleDiff, Text, TypeStyle } from "../runtime-exports";

export class Tabs extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.Tabs';
    declare labels: string[];
    declare selected: number;
    declare onSelect: ((int: number) => void) | null;

    constructor(labels?: any, selected?: any, onSelect: any = null, props?: any) {
        super();
        if (labels !== undefined) this.labels = labels;
        if (selected !== undefined) this.selected = selected;
        if (onSelect !== undefined) this.onSelect = onSelect;
        if (this.selected === undefined) this.selected = 0;
        this.labels = labels;
        this.selected = selected;
        this.onSelect = onSelect;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let primary = theme.colors('primary');
        let slot = theme.navigation;
        let indicator = slot?.indicatorThickness ?? 3;
        let row = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, height: slot?.tabHeight ?? 48 });
        for (let i = 0; i < $eq.collections.count(this.labels); i++) {
            let isActive = i === this.selected;
            let index = i;
            let label = new Text($eq.collections.item(this.labels, i), 'caption', isActive ? primary.base : slot?.inactiveColor ?? theme.textMuted, 1, 'start', false, false, null, 0, { styleOverride: new TypeStyle(slot?.tabLabelSize ?? 14, slot?.tabLabelLineHeight ?? 18, isActive ? slot?.tabSelectedLabelWeight ?? 'bold' : slot?.tabLabelWeight ?? 'semiBold', slot?.tabLabelTracking ?? Math.fround(0.1), Math.fround(1.3)) });
            let labelRow = new Row(0, 'start', 'center', false, null, null, { main: 'center', height: SizeValue.fill });
            labelRow.add(label);
            let cell = new Column(0, 'start', 'stretch', false, null, null, { height: SizeValue.fill });
            cell.add(new Flexible(labelRow));
            cell.add(new Box(new BoxStyle({ width: SizeValue.fill, height: indicator, padding: EdgeInsets.symmetric(slot?.indicatorInset ?? 16, 0) }), isActive ? new Box(new BoxStyle({ width: SizeValue.fill, height: indicator, background: primary.base, cornerRadius: new CornerRadii(slot?.indicatorRadius ?? 2, slot?.indicatorRadius ?? 2, 0, 0) })) : null));
            let surface = new Box(new BoxStyle({ width: SizeValue.fill, height: SizeValue.fill, pressed: new StyleDiff({ background: slot?.quietFill ?? theme.surfaceSubtle }) }), cell);
            row.add(new Flexible(new Pressable(surface, this.onSelect == null ? null : () => this.onSelect!(index), { label: $eq.collections.item(this.labels, i), role: 'tab', selected: isActive })));
        }
        if (this.onSelect == null || $eq.collections.count(this.labels) === 0) return row;
        let count = $eq.collections.count(this.labels);
        return new Adjustable(row, (direction: number) => this.onSelect!($eq.num.intRem(this.selected + direction + count, count)), { role: 'tablist' });
    }
}

