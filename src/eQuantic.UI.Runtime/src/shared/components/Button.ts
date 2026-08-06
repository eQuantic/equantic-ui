import { Box, BoxStyle, BuildContext, ButtonStyles, CornerRadii, EdgeInsets, Icon, Pressable, Row, SizeValue, Spinner, StatelessComponent, Text, TypeStyle } from "../runtime-exports";

export class Button extends StatelessComponent {
    declare label: string;
    declare variant: string;
    declare size: string;
    declare onPressed: (() => void) | null;
    declare disabled: boolean;
    declare expand: boolean;
    declare leading: any;
    declare loading: boolean;
    constructor(label?: any, variant: any = 'primary', size: any = 'medium', onPressed: any = null, props?: any) {
        super();
        if (label !== undefined) this.label = label;
        if (variant !== undefined) this.variant = variant;
        if (size !== undefined) this.size = size;
        if (onPressed !== undefined) this.onPressed = onPressed;
        if (this.variant === undefined) this.variant = 'primary';
        if (this.size === undefined) this.size = 'small';
        this.label = label;this.variant = variant;this.size = size;this.onPressed = onPressed;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let colors = theme.colors(this.variant);let [height, padX, gap, labelSize, iconSize, , ] = ButtonStyles.metrics(this.size, context.density);let radius = theme.shape(this.size === 'xLarge' ? 'large' : 'medium');if (this.variant === 'link') padX = 6;let fill = ((this.variant === 'outline' || this.variant === 'ghost') || this.variant === 'link') ? null : colors.base;let textColor = colors.onBase;let borderWidth = this.variant === 'outline' ? 1 : 0;let borderColor = theme.borderStrong;let inert = this.disabled || this.loading;if (inert) {let opacity = theme.disabledOpacity;let filled: any; if (((fill != null) && (filled = fill, true))) fill = filled.withOpacity(opacity);textColor = textColor.withOpacity(opacity);borderColor = borderColor.withOpacity(opacity);}let label = new Text(this.label, 'label', textColor, 1, { styleOverride: TypeStyle.ofSize(labelSize, 'semiBold', 0.1) });let content = new Row(gap, { height: SizeValue.fill, main: 'center', cross: 'center' });if (this.loading) content.add(new Spinner(iconSize, textColor)); else { let leading: any; if (((this.leading != null) && (leading = this.leading, true))) content.add(new Icon(leading, iconSize, textColor)); }content.add(label);let container = new Box(new BoxStyle({ height: height, width: this.expand ? SizeValue.fill : SizeValue.hug, minWidth: 64, padding: EdgeInsets.symmetric(padX, 0), background: fill, cornerRadius: new CornerRadii(radius), borderWidth: borderWidth, borderColor: borderColor }), content);let pressedFill = (() => { const _s = this.variant; if (_s === 'link') return null; if ((_s === 'outline' || _s === 'ghost')) return theme.surfaceSubtle; return colors.pressed; })();return new Pressable(container, inert ? null : this.onPressed, { disabled: inert, label: this.label, pressedBackground: inert ? null : pressedFill });
    }

}

