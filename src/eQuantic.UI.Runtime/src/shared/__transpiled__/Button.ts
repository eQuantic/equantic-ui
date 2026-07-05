import { Box, BoxStyle, BuildContext, ButtonStyles, ColorToken, Component, ComponentContext, CornerRadii, EdgeInsets, HtmlElement, Pressable, Row, SizeValue, StatelessComponent, Text, TypeStyle, VariantColors, VisualNode } from "@equantic/runtime";

export class Button extends StatelessComponent {
    constructor(label?: any, variant: any = 'primary', size: any = 'medium', onPressed: any = null, props?: any) {
        super(props);
        if (label !== undefined) this.label = label;
        if (variant !== undefined) this.variant = variant;
        if (size !== undefined) this.size = size;
        if (onPressed !== undefined) this.onPressed = onPressed;
        this.label = label;this.variant = variant;this.size = size;this.onPressed = onPressed;
    }

    build(context: BuildContext) {
        let theme = context.theme;let colors = theme.colors(this.variant);let [height, padX, gap, labelSize, , , ] = ButtonStyles.metrics(this.size);let radius = theme.shape(this.size === 'xLarge' ? 'large' : 'medium');if (this.variant === 'link') padX = 6;let fill = ((this.variant === 'outline' || this.variant === 'ghost') || this.variant === 'link') ? null : colors.base;let textColor = colors.onBase;let borderWidth = this.variant === 'outline' ? 1 : 0;let borderColor = theme.borderStrong;if (this.disabled) {let opacity = theme.disabledOpacity;fill = fill?.withOpacity(opacity);textColor = textColor.withOpacity(opacity);borderColor = borderColor.withOpacity(opacity);}let label = new Text(this.label, 'label', textColor, 1, { styleOverride: new TypeStyle(labelSize, labelSize, 'semiBold', 0.1, 1.3) });let content = new Row(gap, { height: SizeValue.fill, main: 'center' });content.add(label);let container = new Box(new BoxStyle({ height: height, width: this.expand ? SizeValue.fill : SizeValue.hug, minWidth: ButtonStyles.minWidth, padding: EdgeInsets.symmetric(padX, 0), background: fill, cornerRadius: new CornerRadii(radius), borderWidth: borderWidth, borderColor: borderColor }), content);let pressedFill = (() => { const _s = this.variant; if (_s === 'link') return null; if ((_s === 'outline' || _s === 'ghost')) return theme.surfaceSubtle; return colors.pressed; })();return new Pressable(container, this.disabled ? null : this.onPressed, { disabled: this.disabled, label: this.label, pressedBackground: this.disabled ? null : pressedFill });
    }

}

