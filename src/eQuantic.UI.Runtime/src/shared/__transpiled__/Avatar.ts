import { $eq, Box, BoxStyle, BuildContext, Component, ComponentContext, CornerRadii, HtmlElement, Radius, Row, SizeValue, StatelessComponent, Text, TypeStyle, VariantColors, VisualNode } from "@equantic/runtime";

export class Avatar extends StatelessComponent {
    static tintPalette: Variant[] = ['primary', 'success', 'info', 'warning', 'destructive'];
    constructor(initials?: any, size: any = 'medium', name: any = null, props?: any) {
        super(props);
        if (initials !== undefined) this.initials = initials;
        if (size !== undefined) this.size = size;
        if (name !== undefined) this.name = name;
        this.initials = initials;this.size = size;this.name = name;
    }

    build(context: BuildContext) {
        let theme = context.theme;let side = (() => { const _s = this.size; if (_s === 'small') return 24; if (_s === 'medium') return 32; if (_s === 'large') return 40; return 56; })();let labelSize = (() => { const _s = this.size; if (_s === 'small') return 10; if (_s === 'medium') return 13; if (_s === 'large') return 16; return 22; })();let seed = this.name ?? this.initials;let tint = theme.colors(Avatar.tintPalette[seed.length % Avatar.tintPalette.length]);let clipped = this.initials.length > 2 ? this.initials.substring(0, 0 + 2) : this.initials;let label = new Text(clipped, 'caption', tint.onSubtle, 1, { styleOverride: new TypeStyle(labelSize, labelSize, 'semiBold', 0, 1.3) });let content = new Row(0, { main: 'center', height: SizeValue.fill });content.add(label);return new Box(new BoxStyle({ width: side, height: side, background: tint.subtle, cornerRadius: new CornerRadii(Radius.full) }), content);
    }

}

