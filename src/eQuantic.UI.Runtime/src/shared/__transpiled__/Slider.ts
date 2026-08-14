import { $eq, Adjustable, Box, BoxStyle, BuildContext, ColorToken, Column, CornerRadii, Draggable, Flexible, Motion, Pressable, Row, SizeValue, StatelessComponent, TransitionSpec, VariantValue } from "@equantic/runtime";

export class Slider extends StatelessComponent {
    static trackHeight: number = 4;
    static thumbSize: number = 20;
    declare value: number;
    declare onChanged: any;
    declare min: number;
    declare max: number;
    declare step: number;
    declare disabled: boolean;
    declare variant: VariantValue;
    declare label: string;
    constructor(value?: any, onChanged: any = null, props?: any) {
        super();
        if (value !== undefined) this.value = value;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (this.value === undefined) this.value = 0;
        if (this.min === undefined) this.min = 0;
        if (this.max === undefined) this.max = 1;
        if (this.step === undefined) this.step = 0;
        if (this.disabled === undefined) this.disabled = false;
        if (this.variant === undefined) this.variant = 'primary';
        if (this.label === undefined) this.label = '';
        this.value = value;this.onChanged = onChanged;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let span = this.max - this.min;let fraction = span <= 0 ? 0 : Math.min(Math.max((this.value - this.min) / span, 0), 1);let step = this.step > 0 ? this.step : span / 10;let accent = theme.colors(this.variant).base;let fill = this.disabled ? theme.borderStrong : accent;let thumb = new Box(new BoxStyle({ width: Slider.thumbSize, height: Slider.thumbSize, background: theme.surface, cornerRadius: new CornerRadii(theme.shape('full')), borderWidth: 2, borderColor: fill, elevation: 2, transition: TransitionSpec.of(1, Motion.press) }));let row = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, cross: 'center' });row.add(new Flexible(Slider.trackHalf(fill, true, !this.disabled, () => this.onChanged?.(Math.max(this.min, this.value - step))), Slider.weight(fraction)));row.add(thumb);row.add(new Flexible(Slider.trackHalf(theme.borderStrong, false, !this.disabled, () => this.onChanged?.(Math.min(this.max, this.value + step))), Slider.weight(1 - fraction)));let surface = this.disabled ? row : new Draggable(row, null, { axis: 'horizontal', normalized: true, follows: false, min: 0, max: 1, restOffset: fraction, onMoved: (f: number) => this.onChanged?.(this.quantize(this.min + f * span, step)) });let box = new Box(new BoxStyle({ width: SizeValue.fill, minWidth: 120, height: 48, opacity: this.disabled ? theme.disabledOpacity : 1 }), surface);return this.disabled ? box : new Adjustable(box, (direction: number) => this.onChanged?.(this.quantize(this.value + direction * step, step)), { label: this.label });
    }

    quantize(value: number, step: number) {
        return Math.min(Math.max(this.step > 0 ? this.min + $eq.math.round((value - this.min) / step) * step : value, this.min), this.max);
    }

    static weight(fraction: number) {
        return Math.max(1, (Math.trunc($eq.math.round(fraction * 1000)) | 0));
    }

    static trackHalf(color: ColorToken, filled: boolean, enabled: boolean, onPressed: () => void) {
        let bar = new Box(new BoxStyle({ width: SizeValue.fill, height: Slider.trackHeight, background: color, cornerRadius: filled ? new CornerRadii(Slider.trackHeight / 2, 0, 0, Slider.trackHeight / 2) : new CornerRadii(0, Slider.trackHeight / 2, Slider.trackHeight / 2, 0), transition: TransitionSpec.of(1, Motion.press) }));let centered = new Column(0, 'start', 'stretch', false, null, null, { height: SizeValue.fill, main: 'center' });centered.add(bar);let target = new Box(new BoxStyle({ width: SizeValue.fill, height: SizeValue.fill }), centered);return enabled ? new Pressable(target, onPressed, { label: filled ? 'Decrease' : 'Increase' }) : target;
    }

}

