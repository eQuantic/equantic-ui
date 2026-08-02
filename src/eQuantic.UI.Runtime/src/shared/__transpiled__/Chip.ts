import { Box, BoxStyle, BuildContext, CornerRadii, EdgeInsets, Icon, Pressable, Row, SizeValue, Sizing, StatelessComponent, Text, TypeStyle } from "@equantic/runtime";

export class Chip extends StatelessComponent {
    declare label: string;
    declare kind: string;
    declare selected: boolean;
    declare onPressed: () => void;
    declare onRemove: () => void;
    declare variant: string;
    constructor(label?: any, kind: any = 'filter', selected: any = false, onPressed: any = null, onRemove: any = null, props?: any) {
        super(props);
        if (label !== undefined) this.label = label;
        if (kind !== undefined) this.kind = kind;
        if (selected !== undefined) this.selected = selected;
        if (onPressed !== undefined) this.onPressed = onPressed;
        if (onRemove !== undefined) this.onRemove = onRemove;
        if (this.kind === undefined) this.kind = 'filter';
        if (this.variant === undefined) this.variant = 'secondary';
        this.label = label;this.kind = kind;this.selected = selected;this.onPressed = onPressed;this.onRemove = onRemove;
    }

    build(context: BuildContext) {
        let theme = context.theme;let primary = theme.colors('primary');let tint = theme.colors(this.variant);let fill = (() => { const _s = this.kind; if ((_s === 'filter') && (this.selected)) return primary.subtle; if (_s === 'filter') return theme.surfaceSubtle; if (_s === 'tag') return tint.subtle; return theme.surfaceSubtle; })();let textColor = (() => { const _s = this.kind; if ((_s === 'filter') && (this.selected)) return primary.onSubtle; if (_s === 'tag') return tint.onSubtle; return theme.textSecondary; })();let label = new Text(this.label, 'caption', textColor, 1, { styleOverride: new TypeStyle(13, 16, 'semiBold', 0, 1.3) });let content = new Row(6, { main: 'center', height: SizeValue.fill });if (this.kind === 'filter' && this.selected) {content.add(new Icon('check', 16, textColor));}content.add(label);if (this.kind === 'input' && this.onRemove != null) {content.add(new Pressable(new Icon('close', 20, textColor), this.onRemove, { label: 'Remove' }));}let box = new Box(new BoxStyle({ height: Sizing.height('small'), padding: EdgeInsets.symmetric(12, 0), background: fill, cornerRadius: new CornerRadii(theme.shape('full')), borderWidth: this.kind === 'filter' && this.selected ? 1 : 0, borderColor: primary.base }), content);return this.kind === 'filter' && this.onPressed != null ? new Pressable(box, this.onPressed, { label: this.label, pressedBackground: this.selected ? primary.pressed.withOpacity(0.24) : theme.surfaceSubtle }) : box;
    }

}

