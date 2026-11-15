import { Box, BoxStyle, BuildContext, ControlMetrics, CornerRadii, EdgeInsets, Icon, Pressable, Row, SizeValue, SizeVariantValue, Spinner, StatelessComponent, StyleDiff, Text, TypeStyle, VariantValue } from "../runtime-exports";

export class Button extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.Button';
    declare label: string;
    declare variant: VariantValue;
    declare size: SizeVariantValue;
    declare onPressed: (() => void) | null;
    declare disabled: boolean;
    declare expand: boolean;
    declare initialFocus: boolean;
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
        if (this.disabled === undefined) this.disabled = false;
        if (this.expand === undefined) this.expand = false;
        if (this.initialFocus === undefined) this.initialFocus = false;
        if (this.loading === undefined) this.loading = false;
        this.label = label;
        this.variant = variant;
        this.size = size;
        this.onPressed = onPressed;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let colors = theme.colors(this.variant);
        let slot = theme.button;
        let ladder = (slot?.controls ?? null);
        let height = ControlMetrics.height(context, this.size, ladder);
        let padX = ControlMetrics.paddingX(context, this.size, ladder);
        let gap = ControlMetrics.gap(context, this.size, ladder);
        let labelSize = ControlMetrics.labelSize(context, this.size, ladder);
        let iconSize = ControlMetrics.icon(context, this.size, ladder);
        let radius = ControlMetrics.radius(context, this.size, ladder, theme.shape(this.size === 'xLarge' ? 'large' : 'medium'));
        if (this.variant === 'link') padX = slot?.linkPaddingX ?? 6;
        let fill = (($0) => (($0 === 'outline' || $0 === 'ghost') || $0 === 'link'))(this.variant) ? null : colors.base;
        let textColor = colors.onBase;
        let borderWidth = this.variant === 'outline' ? slot?.outlineWidth ?? 1 : 0;
        let borderColor = slot?.outlineColor ?? theme.borderStrong;
        let quietFill = slot?.quietFill ?? theme.surfaceSubtle;
        let inert = this.disabled || this.loading;
        if (inert) {
            let opacity = theme.disabledOpacity;
            let filled: any; 
            if ((filled = fill) != null) fill = filled.withOpacity(opacity);
            textColor = textColor.withOpacity(opacity);
            borderColor = borderColor.withOpacity(opacity);
        }
        let label = new Text(this.label, 'label', textColor, 1, 'start', false, false, null, 0, { styleOverride: TypeStyle.ofSize(labelSize, slot?.labelWeight ?? 'semiBold', slot?.labelTracking ?? Math.fround(0.1)) });
        let content = new Row(gap, 'start', 'center', false, null, null, { height: SizeValue.fill, main: 'center', cross: 'center' });
        if (this.loading) content.add(new Spinner(iconSize, textColor)); else { let leading: any; if ((leading = this.leading) != null) content.add(new Icon(leading, iconSize, textColor)); }
        content.add(label);
        let pressedFill = (() => { const $s = this.variant; if ($s === 'link') return null; if (($s === 'outline' || $s === 'ghost')) return quietFill; return colors.pressed; })();
        let hoverFill = (() => { const $s = this.variant; if ($s === 'link') return null; if (($s === 'outline' || $s === 'ghost')) return quietFill; return colors.hover; })();
        let container = new Box(new BoxStyle({ height: height, width: this.expand ? SizeValue.fill : SizeValue.hug, minWidth: slot?.minWidth ?? 64, padding: EdgeInsets.symmetric(padX, 0), background: fill, cornerRadius: new CornerRadii(radius), borderWidth: borderWidth, borderColor: borderColor, hover: inert || hoverFill == null ? null : new StyleDiff({ background: hoverFill }), pressed: inert || pressedFill == null ? null : new StyleDiff({ background: pressedFill }) }), content);
        return new Pressable(container, inert ? null : this.onPressed, { disabled: inert, label: this.label, initialFocus: this.initialFocus });
    }
}

