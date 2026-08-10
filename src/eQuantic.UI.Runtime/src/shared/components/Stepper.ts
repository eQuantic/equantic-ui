import { Box, BoxStyle, BuildContext, CornerRadii, Icon, Pressable, Row, SizeValue, Sizing, StatelessComponent, Text } from "../runtime-exports";

export class Stepper extends StatelessComponent {
    declare value: number;
    declare onChanged: any;
    declare min: number;
    declare max: number;
    declare step: number;
    declare size: string;
    declare disabled: boolean;
    declare suffix: string;
    declare label: string;
    constructor(value?: any, onChanged: any = null, props?: any) {
        super();
        if (value !== undefined) this.value = value;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (this.min === undefined) this.min = -2147483648;
        if (this.max === undefined) this.max = 2147483647;
        if (this.step === undefined) this.step = 1;
        if (this.size === undefined) this.size = 'medium';
        if (this.suffix === undefined) this.suffix = '';
        if (this.label === undefined) this.label = '';
        this.value = value;this.onChanged = onChanged;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let height = Sizing.height(this.size, context.density);let canDecrement = !this.disabled && this.value - this.step >= this.min;let canIncrement = !this.disabled && this.value + this.step <= this.max;let row = new Row(0, 'start', 'center', false, null, null, { height: SizeValue.fill, cross: 'center' });row.add(Stepper.arm(theme, 'minus', height, canDecrement, () => this.onChanged?.(this.value - this.step), `Decrease ${this.label}`));let reading = new Row(0, 'start', 'center', false, null, null, { height: SizeValue.fill, main: 'center', cross: 'center' });reading.add(new Text(`${this.value}${this.suffix}`, 'label', this.disabled ? theme.textMuted : theme.textPrimary, 1, 'start', false, false, null, { align: 'center', tabular: true, styleOverride: theme.type('label').withSize(Sizing.labelSize(this.size, context.density)) }));row.add(new Box(new BoxStyle({ minWidth: height, height: SizeValue.fill }), reading));row.add(Stepper.arm(theme, 'plus', height, canIncrement, () => this.onChanged?.(this.value + this.step), `Increase ${this.label}`));return new Box(new BoxStyle({ width: SizeValue.hug, height: height, background: this.disabled ? theme.surfaceSubtle : theme.surface, cornerRadius: new CornerRadii(Sizing.radius(this.size)), borderWidth: 1, borderColor: theme.borderStrong, opacity: this.disabled ? theme.disabledOpacity : 1 }), row);
    }

    static arm(theme: any, glyph: string, height: number, enabled: boolean, onPressed: () => void, label: string) {
        let centered = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, height: SizeValue.fill, main: 'center', cross: 'center' });centered.add(new Icon(glyph, 20, enabled ? theme.textPrimary : theme.textMuted));let box = new Box(new BoxStyle({ width: height, height: SizeValue.fill }), centered);return new Pressable(box, enabled ? onPressed : null, { disabled: !enabled, label: label });
    }

}

