import { Box, BoxStyle, BuildContext, CornerRadii, Icon, Pressable, Sizing, StatelessComponent } from "../runtime-exports";

export class IconButton extends StatelessComponent {
    declare glyph: string;
    declare label: string;
    declare kind: string;
    declare size: string;
    declare onPressed: (() => void) | null;
    declare disabled: boolean;
    declare selected: boolean;
    declare selectedGlyph: any;
    constructor(glyph?: any, label?: any, kind: any = 'standard', size: any = 'medium', onPressed: any = null, props?: any) {
        super();
        if (glyph !== undefined) this.glyph = glyph;
        if (label !== undefined) this.label = label;
        if (kind !== undefined) this.kind = kind;
        if (size !== undefined) this.size = size;
        if (onPressed !== undefined) this.onPressed = onPressed;
        if (this.glyph === undefined) this.glyph = 'search';
        if (this.kind === undefined) this.kind = 'standard';
        if (this.size === undefined) this.size = 'small';
        this.glyph = glyph;this.label = label;this.kind = kind;this.size = size;this.onPressed = onPressed;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let primary = theme.colors('primary');let side = Sizing.height(this.size, context.density);let iconSize = (() => { const _s = this.size; if (_s === 'small') return 16; if (_s === 'medium') return 20; return 24; })();let fill = (() => { const _s = this.kind; if (_s === 'tonal') return primary.subtle; if (_s === 'filled') return primary.base; return null; })();let tint = (() => { const _s = this.kind; if (_s === 'filled') return primary.onBase; if (_s === 'tonal') return primary.onSubtle; return this.selected ? primary.base : theme.textSecondary; })();if (this.disabled) {let opacity = theme.disabledOpacity;let filled: any; if ((filled = fill) != null) fill = filled.withOpacity(opacity);tint = tint.withOpacity(opacity);}let filledGlyph: any; let glyph = this.selected && (filledGlyph = this.selectedGlyph) != null ? filledGlyph : this.glyph;let content = new Icon(glyph, iconSize, tint).centered();let box = new Box(new BoxStyle({ width: side, height: side, background: fill, cornerRadius: new CornerRadii(theme.shape('full')), borderWidth: this.kind === 'outline' ? 1 : 0, borderColor: theme.borderStrong }), content);let pressedFill = (() => { const _s = this.kind; if (_s === 'filled') return primary.pressed; if (_s === 'tonal') return primary.pressed.withOpacity(0.24); return theme.surfaceSubtle; })();return new Pressable(box, this.disabled ? null : this.onPressed, { disabled: this.disabled, label: this.label, pressedBackground: this.disabled ? null : pressedFill });
    }

}

