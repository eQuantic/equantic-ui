import { Adjustable, Box, BoxStyle, BuildContext, CornerRadii, EdgeInsets, Flexible, Motion, Pressable, Row, SizeValue, Sizing, StatelessComponent, Text, TransitionSpec } from "@equantic/runtime";

export class SegmentedControl extends StatelessComponent {
    declare segments: string[];
    declare selectedIndex: number;
    declare onChanged: any;
    declare size: string;
    declare disabled: boolean;
    declare stretch: boolean;
    constructor(segments?: any, selectedIndex?: any, onChanged: any = null, props?: any) {
        super();
        if (segments !== undefined) this.segments = segments;
        if (selectedIndex !== undefined) this.selectedIndex = selectedIndex;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (this.selectedIndex === undefined) this.selectedIndex = 0;
        if (this.size === undefined) this.size = 'medium';
        if (this.disabled === undefined) this.disabled = false;
        if (this.stretch === undefined) this.stretch = true;
        this.segments = segments;this.selectedIndex = selectedIndex;this.onChanged = onChanged;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let height = Sizing.height(this.size, context.density);let inset = 3;let trackRadius = Sizing.radius(this.size);let row = new Row(0, 'start', 'center', false, null, null, { width: this.stretch ? SizeValue.fill : SizeValue.hug, height: SizeValue.fill, cross: 'stretch' });for (let i = 0; i < this.segments.length; i++) {let index = i;let selected = index === this.selectedIndex;let label = new Row(0, 'start', 'center', false, null, null, { width: this.stretch ? SizeValue.fill : SizeValue.hug, height: SizeValue.fill, main: 'center', cross: 'center' });label.add(new Text(this.segments[index], 'label', selected ? theme.textPrimary : theme.textSecondary, 1, 'start', false, false, null, { styleOverride: theme.type('label').withSize(Sizing.labelSize(this.size, context.density)), transition: TransitionSpec.of(1, Motion.press) }));let segment = new Box(new BoxStyle({ width: this.stretch ? SizeValue.fill : SizeValue.hug, height: SizeValue.fill, padding: this.stretch ? undefined : EdgeInsets.symmetric(Sizing.paddingX(this.size, context.density), 0), background: selected ? theme.surface : null, cornerRadius: new CornerRadii(trackRadius - inset), elevation: selected ? 1 : 0, transition: TransitionSpec.of(1 | 8, Motion.press) }), label);let press = new Pressable(segment, this.disabled || selected ? null : () => this.onChanged?.(index), { disabled: this.disabled, label: this.segments[index], selected: selected });row.add(this.stretch ? new Flexible(press, 1) : press);}let track = new Box(new BoxStyle({ width: this.stretch ? SizeValue.fill : SizeValue.hug, height: height, padding: EdgeInsets.all(inset), background: theme.surfaceSubtle, cornerRadius: new CornerRadii(trackRadius), opacity: this.disabled ? theme.disabledOpacity : 1 }), row);if (this.disabled || this.onChanged == null || this.segments.length === 0) return track;let count = this.segments.length;return new Adjustable(track, (direction: number) => this.onChanged((this.selectedIndex + direction + count) % count), { role: 'radiogroup' });
    }

}

