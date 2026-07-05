import { $eq, Box, BoxStyle, BuildContext, ColorToken, Component, ComponentContext, CornerRadii, HtmlElement, Icon, IconSize, Pressable, Radius, Row, SizeValue, StatelessComponent, VariantColors, VisualNode } from "@equantic/runtime";

export class IconButton extends StatelessComponent {
    constructor(glyph?: any, label?: any, kind: any = 'standard', size: any = 'medium', onPressed: any = null, props?: any) {
        super(props);
        if (glyph !== undefined) this.glyph = glyph;
        if (label !== undefined) this.label = label;
        if (kind !== undefined) this.kind = kind;
        if (size !== undefined) this.size = size;
        if (onPressed !== undefined) this.onPressed = onPressed;
        this.glyph = glyph;this.label = label;this.kind = kind;this.size = size;this.onPressed = onPressed;
    }

    build(context: BuildContext) {
        let theme = context.theme;let primary = theme.colors('primary');let side = (() => { const _s = this.size; if (_s === 'small') return 32; if (_s === 'medium') return 40; if (_s === 'large') return 48; return 56; })();let iconSize = (() => { const _s = this.size; if (_s === 'small') return IconSize.sm; if (_s === 'medium') return IconSize.dense; return IconSize.md; })();let fill = (() => { const _s = this.kind; if (_s === 'tonal') return primary.subtle; if (_s === 'filled') return primary.base; return null; })();let tint = (() => { const _s = this.kind; if (_s === 'filled') return primary.onBase; if (_s === 'tonal') return primary.onSubtle; return this.selected ? primary.base : theme.textSecondary; })();if (this.disabled) {let opacity = theme.disabledOpacity;fill = fill?.withOpacity(opacity);tint = tint.withOpacity(opacity);}let filledGlyph; let glyph = this.selected && ((this.selectedGlyph != null) && (filledGlyph = this.selectedGlyph, true)) ? filledGlyph : this.glyph;let content = new Row(0, { main: 'center', height: SizeValue.fill });content.add(new Icon(glyph, iconSize, tint));let box = new Box(new BoxStyle({ width: side, height: side, background: fill, cornerRadius: new CornerRadii(Radius.full), borderWidth: this.kind === 'outline' ? 1 : 0, borderColor: theme.borderStrong }), content);let pressedFill = (() => { const _s = this.kind; if (_s === 'filled') return primary.pressed; if (_s === 'tonal') return primary.pressed.withOpacity(0.24); return theme.surfaceSubtle; })();return new Pressable(box, this.disabled ? null : this.onPressed, { disabled: this.disabled, label: this.label, pressedBackground: this.disabled ? null : pressedFill });
    }

}

