import { Box, BoxStyle, BuildContext, CornerRadii, EdgeInsets, Flexible, Motion, Pressable, Row, SizeValue, Sizing, StatelessComponent, Text, TransitionSpec, TypeStyle } from "../runtime-exports";

export class SegmentedControl extends StatelessComponent {
    declare segments: any;
    declare selectedIndex: number;
    declare onChanged: any;
    declare size: string;
    declare disabled: boolean;
    declare stretch: boolean;
    constructor(segments?: any, selectedIndex?: any, onChanged: any = null, props?: any) {
        super(props);
        if (segments !== undefined) this.segments = segments;
        if (selectedIndex !== undefined) this.selectedIndex = selectedIndex;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (this.size === undefined) this.size = 'medium';
        if (this.stretch === undefined) this.stretch = true;
        this.segments = segments;this.selectedIndex = selectedIndex;this.onChanged = onChanged;
    }

    build(context: BuildContext) {
        let theme = context.theme;let height = Sizing.height(this.size);let inset = 3;let trackRadius = Sizing.radius(this.size);let row = new Row(0, { width: this.stretch ? SizeValue.fill : SizeValue.hug, height: SizeValue.fill, cross: 'stretch' });for (let i = 0; i < this.segments.length; i++) {let index = i;let selected = index === this.selectedIndex;let label = new Row(0, { width: this.stretch ? SizeValue.fill : SizeValue.hug, height: SizeValue.fill, main: 'center', cross: 'center' });label.add(new Text(this.segments[index], 'label', selected ? theme.textPrimary : theme.textSecondary, 1, { styleOverride: SegmentedControl.labelStyle(theme, this.size), transition: TransitionSpec.of(1, Motion.press) }));let segment = new Box(new BoxStyle({ width: this.stretch ? SizeValue.fill : SizeValue.hug, height: SizeValue.fill, padding: this.stretch ? undefined : EdgeInsets.symmetric(Sizing.paddingX(this.size), 0), background: selected ? theme.surface : null, cornerRadius: new CornerRadii(trackRadius - inset), elevation: selected ? 1 : 0, transition: TransitionSpec.of(1 | 8, Motion.press) }), label);let press = new Pressable(segment, this.disabled || selected ? null : () => this.onChanged?.(index), { disabled: this.disabled, label: this.segments[index], selected: selected });row.add(this.stretch ? new Flexible(press, 1) : press);}return new Box(new BoxStyle({ width: this.stretch ? SizeValue.fill : SizeValue.hug, height: height, padding: EdgeInsets.all(inset), background: theme.surfaceSubtle, cornerRadius: new CornerRadii(trackRadius), opacity: this.disabled ? theme.disabledOpacity : 1 }), row);
    }

    static labelStyle(theme: any, size: string) {
        let role = theme.type('label');return new TypeStyle(Sizing.labelSize(size), role.lineHeight, role.weight, role.tracking, role.maxScale);
    }

}

