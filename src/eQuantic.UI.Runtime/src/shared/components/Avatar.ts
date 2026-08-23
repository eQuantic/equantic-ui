import { $eq, Box, BoxStyle, BuildContext, CornerRadii, Icon, Image, Positioned, SizeVariantValue, Sizing, Stack, StatelessComponent, Text, TypeStyle, VariantValue, VisualNode } from "../runtime-exports";

export class Avatar extends StatelessComponent {
    static tintPalette: VariantValue[] = ['primary', 'success', 'info', 'warning', 'destructive'];
    declare initials: string;
    declare imageSource: any;
    declare size: SizeVariantValue;
    declare name: any;
    declare status: string;

    constructor(initials?: any, size: any = 'medium', name: any = null, props?: any) {
        super();
        if (initials !== undefined) this.initials = initials;
        if (size !== undefined) this.size = size;
        if (name !== undefined) this.name = name;
        if (this.size === undefined) this.size = 'small';
        if (this.status === undefined) this.status = 'none';
        this.initials = initials;
        this.size = size;
        this.name = name;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let side = Sizing.avatar(this.size);
        let labelSize = (() => { const _s = this.size; if (_s === 'small') return 10; if (_s === 'medium') return 13; if (_s === 'large') return 16; return 22; })();
        let source: any; 
        if ((source = this.imageSource) != null) {
            let photo = new Image(source, side, side, 'cover', this.name ?? this.initials, { cornerRadius: new CornerRadii(theme.shape('full')) });
            return this.status === 'none' ? photo : this.withStatusDot(photo, side, theme);
        }
        let seed = this.name ?? this.initials;
        let tint = theme.colors(Avatar.tintPalette[seed.length % Avatar.tintPalette.length]);
        let clipped = this.initials.length > 2 ? $eq.text.substring(this.initials, 0, 2) : this.initials;
        let hasInitials = clipped.length > 0;
        let glyphSize = (() => { const _s = this.size; if (_s === 'small') return 16; if (_s === 'medium') return 20; if (_s === 'large') return 24; return 32; })();
        let face = hasInitials ? new Text(clipped, 'caption', tint.onSubtle, 1, 'start', false, false, null, 0, { styleOverride: new TypeStyle(labelSize, labelSize, 'semiBold', 0, Math.fround(1.3)) }) : new Icon('person', glyphSize, theme.textMuted);
        let content = face.centered();
        let circle = new Box(new BoxStyle({ width: side, height: side, background: hasInitials ? tint.subtle : theme.surfaceSubtle, cornerRadius: new CornerRadii(theme.shape('full')) }), content);
        return this.status === 'none' ? circle : this.withStatusDot(circle, side, theme);
    }

    withStatusDot(face: VisualNode, side: number, theme: any) {
        let dotSide = $eq.math.round(side / Math.fround(3.3));
        let dotFill = this.status === 'online' ? theme.colors('success').base : theme.textMuted;
        let dot = new Box(new BoxStyle({ width: dotSide, height: dotSide, background: dotFill, cornerRadius: new CornerRadii(theme.shape('full')), borderWidth: 2, borderColor: theme.surface }));
        let stack = new Stack();
        stack.add(face);
        stack.add(new Positioned(dot, null, 0, 0));
        return stack;
    }
}

