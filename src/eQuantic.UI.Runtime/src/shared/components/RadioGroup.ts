import { $eq, Adjustable, Box, BoxStyle, BuildContext, Column, ControlMetrics, CornerRadii, Pressable, Row, SizeValue, Sizing, StatelessComponent, Text, VisualNode, VisualNodeExtensions } from "../runtime-exports";

export class RadioGroup extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.RadioGroup';
    declare options: string[];
    declare selected: number;
    declare onChanged: ((int: number) => void) | null;
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
        this.options = options;
        this.selected = selected;
        this.onChanged = onChanged;
        this.label = label;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let primary = theme.colors('primary');
        let slot = theme.selectionControls;
        let side = ControlMetrics.atDensity((slot?.radioSize ?? null), context.density) ?? (ControlMetrics.atDensity((slot?.box ?? null), context.density) ?? Sizing.selectionBox(context.density));
        let dot = ControlMetrics.atDensity((slot?.radioDot ?? null), context.density) ?? Sizing.radioDot(context.density);
        let options = new Column(4, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        for (let i = 0; i < $eq.collections.count(this.options); i++) {
            let isSelected = i === this.selected;
            let index = i;
            let circleContent = isSelected ? VisualNodeExtensions.centered(new Box(new BoxStyle({ width: dot, height: dot, background: primary.base, cornerRadius: new CornerRadii(theme.shape('full')) }))) : null;
            let circle = new Box(new BoxStyle({ width: side, height: side, cornerRadius: new CornerRadii(theme.shape('full')), borderWidth: slot?.borderWidth ?? 2, borderColor: isSelected ? primary.base : slot?.borderColor ?? theme.borderStrong }), circleContent);
            let row = new Row(slot?.labelGap ?? 12, 'start', 'center', false, null, null, { cross: 'center', width: SizeValue.fill, height: slot?.radioRowHeight ?? 44 });
            row.add(circle);
            row.add(new Text($eq.collections.item(this.options, i), 'bodyM', this.disabled ? theme.textMuted : theme.textPrimary, 1));
            options.add(new Pressable(row, this.disabled || this.onChanged == null || isSelected ? null : () => this.onChanged!(index), { disabled: this.disabled, label: $eq.collections.item(this.options, i), role: 'radio', selected: isSelected }));
        }
        let group: VisualNode = options;
        if (!this.disabled && !(this.onChanged == null) && $eq.collections.count(this.options) > 0) {
            let count = $eq.collections.count(this.options);
            group = new Adjustable(options, (direction: number) => this.onChanged!($eq.num.intRem(this.selected + direction + count, count)), { role: 'radiogroup', label: this.label ?? '' });
        }
        let column = new Column(4, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        let groupLabel: any; 
        if ((groupLabel = this.label) != null) column.add(new Text(groupLabel, 'caption', theme.textMuted, 1));
        column.add(group);
        return column;
    }
}

