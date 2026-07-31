import { $eq, Box, BoxStyle, BuildContext, CornerRadii, Icon, IconSize, Image, Positioned, Row, SizeValue, Stack, StatelessComponent, Text, TypeStyle, VisualNode } from "@equantic/runtime";

export class Avatar extends StatelessComponent {
    static tintPalette: string[] = ['primary', 'success', 'info', 'warning', 'destructive'];
    declare initials: string;
    declare imageSource: string;
    declare size: string;
    declare name: string;
    declare status: string;
    constructor(initials?: any, size: any = 'medium', name: any = null, props?: any) {
        super(props);
        if (initials !== undefined) this.initials = initials;
        if (size !== undefined) this.size = size;
        if (name !== undefined) this.name = name;
        this.initials = initials;this.size = size;this.name = name;
    }

    build(context: BuildContext) {
        let theme = context.theme;let side = (() => { const _s = this.size; if (_s === 'small') return 24; if (_s === 'medium') return 32; if (_s === 'large') return 40; return 56; })();let labelSize = (() => { const _s = this.size; if (_s === 'small') return 10; if (_s === 'medium') return 13; if (_s === 'large') return 16; return 22; })();let source; if (((this.imageSource != null) && (source = this.imageSource, true))) {let photo = new Image(source, side, side, 'cover', this.name ?? this.initials, { cornerRadius: new CornerRadii(theme.shape('full')) });return this.status === 'none' ? photo : this.withStatusDot(photo, side, theme);}let seed = this.name ?? this.initials;let tint = theme.colors(Avatar.tintPalette[seed.length % Avatar.tintPalette.length]);let clipped = this.initials.length > 2 ? this.initials.substring(0, 0 + 2) : this.initials;let hasInitials = clipped.length > 0;let glyphSize = (() => { const _s = this.size; if (_s === 'small') return IconSize.sm; if (_s === 'medium') return IconSize.dense; if (_s === 'large') return IconSize.md; return IconSize.lg; })();let face = hasInitials ? new Text(clipped, 'caption', tint.onSubtle, 1, { styleOverride: new TypeStyle(labelSize, labelSize, 'semiBold', 0, 1.3) }) : new Icon('person', glyphSize, theme.textMuted);let content = new Row(0, { main: 'center', height: SizeValue.fill });content.add(face);let circle = new Box(new BoxStyle({ width: side, height: side, background: hasInitials ? tint.subtle : theme.surfaceSubtle, cornerRadius: new CornerRadii(theme.shape('full')) }), content);return this.status === 'none' ? circle : this.withStatusDot(circle, side, theme);
    }

    withStatusDot(face: VisualNode, side: number, theme: any) {
        let dotSide = $eq.math.round(side / 3.3);let dotFill = this.status === 'online' ? theme.colors('success').base : theme.textMuted;let dot = new Box(new BoxStyle({ width: dotSide, height: dotSide, background: dotFill, cornerRadius: new CornerRadii(theme.shape('full')), borderWidth: 2, borderColor: theme.surface }));let stack = new Stack();stack.add(face);stack.add(new Positioned(dot, null, 0, 0));return stack;
    }

}

