import { $eq, Box, BoxStyle, BuildContext, ControlMetrics, CornerRadii, Icon, IconGlyph, Image, Positioned, SizeVariantValue, Sizing, Stack, StatelessComponent, Text, TypeStyle, VariantValue, VisualNode, VisualNodeExtensions } from "../runtime-exports";

export class Avatar extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.Avatar';
    declare initials: string;
    declare imageSource: any;
    declare size: SizeVariantValue;
    declare name: any;
    declare status: string;
    static $slots: any = null;
    static $failure: any = null;

    static $init(): any {
        if (Avatar.$slots === null) {
            if (Avatar.$failure !== null) throw Avatar.$failure;
            let $slots: any = Avatar.$slots = {};
            try {
                $slots.tintPalette = null;
                $slots.tintPalette = ['primary', 'success', 'info', 'warning', 'destructive'];
            } catch ($error) {
                Avatar.$slots = null;
                throw Avatar.$failure = $eq.exceptions.typeInitialization('eQuantic.UI.Components.Avatar', $error);
            }
        }
        return Avatar.$slots;
    }

    static get tintPalette(): VariantValue[] {
        return Avatar.$init().tintPalette;
    }

    static set tintPalette(value: VariantValue[]) {
        Avatar.$init().tintPalette = value;
    }

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
        let slot = theme.badge;
        let side = Avatar.sideOf(theme, this.size);
        let labelSize = ControlMetrics.atSize((slot?.avatarLabelSize ?? null), this.size) ?? Sizing.avatarInitials(this.size);
        let source: any; 
        if ((source = this.imageSource) != null) {
            let photo = new Image(source, side, side, 'cover', this.name ?? this.initials, { cornerRadius: new CornerRadii(theme.shape('full')) });
            return this.status === 'none' ? photo : this.withStatusDot(photo, side, theme);
        }
        let seed = this.name ?? this.initials;
        let tint = theme.colors(Avatar.tintPalette[$eq.num.intRem(seed.length, Avatar.tintPalette.length)]);
        let clipped = this.initials.length > 2 ? $eq.text.substring(this.initials, 0, 2) : this.initials;
        let hasInitials = clipped.length > 0;
        let glyphSize = ControlMetrics.atSize((slot?.avatarGlyphSize ?? null), this.size) ?? (() => { const $s = this.size; if ($s === 'small') return 16; if ($s === 'medium') return 20; if ($s === 'large') return 24; return 32; })();
        let face = hasInitials ? new Text(clipped, 'caption', tint.onSubtle, 1, 'start', false, false, null, 0, { styleOverride: new TypeStyle(labelSize, labelSize, slot?.avatarLabelWeight ?? 'semiBold', 0, Math.fround(1.3)) }) : new Icon(IconGlyph.fromIcons('person'), glyphSize, slot?.avatarGlyphColor ?? theme.textMuted);
        let content = VisualNodeExtensions.centered(face);
        let circle = new Box(new BoxStyle({ width: side, height: side, background: hasInitials ? tint.subtle : slot?.avatarFill ?? theme.surfaceSubtle, cornerRadius: new CornerRadii(theme.shape('full')) }), content);
        return this.status === 'none' ? circle : this.withStatusDot(circle, side, theme);
    }

    static sideOf(theme: any, size: SizeVariantValue) {
        return ControlMetrics.atSize((theme.badge?.avatarSize ?? null), size) ?? Sizing.avatar(size);
    }

    withStatusDot(face: VisualNode, side: number, theme: any) {
        let slot = theme.badge;
        let dotSide = $eq.math.roundSingle(Math.fround(side / Math.fround(3.3)));
        let dotFill = this.status === 'online' ? theme.colors('success').base : slot?.offlineColor ?? theme.textMuted;
        let dot = new Box(new BoxStyle({ width: dotSide, height: dotSide, background: dotFill, cornerRadius: new CornerRadii(theme.shape('full')), borderWidth: slot?.ringWidth ?? 2, borderColor: slot?.ringColor ?? theme.surface }));
        let stack = new Stack();
        stack.add(face);
        stack.add(new Positioned(dot, null, 0, 0));
        return stack;
    }
}

