import { $eq, Box, BoxStyle, BuildContext, CornerRadii, EdgeInsets, Positioned, Stack, StatelessComponent, Text, TypeStyle, VariantValue, VisualNode, VisualNodeExtensions } from "../runtime-exports";

export class Badge extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.Badge';
    declare count: number;
    declare max: number;
    declare variant: VariantValue;
    declare neutral: boolean;
    declare dot: boolean;
    declare ring: boolean;

    constructor(count: any = 0, max: any = 99, variant: any = 'destructive', props?: any) {
        super();
        if (count !== undefined) this.count = count;
        if (max !== undefined) this.max = max;
        if (variant !== undefined) this.variant = variant;
        if (this.count === undefined) this.count = 0;
        if (this.max === undefined) this.max = 0;
        if (this.variant === undefined) this.variant = 'primary';
        if (this.neutral === undefined) this.neutral = false;
        if (this.dot === undefined) this.dot = false;
        if (this.ring === undefined) this.ring = false;
        this.count = count;
        this.max = max;
        this.variant = variant;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let slot = theme.badge;
        let fill = this.neutral ? slot?.neutralFill ?? theme.surfaceSubtle : theme.colors(this.variant).base;
        let textColor = this.neutral ? slot?.neutralColor ?? theme.textSecondary : theme.colors(this.variant).onBase;
        let ring = this.ring ? slot?.ringWidth ?? 2 : 0;
        let ringColor = slot?.ringColor ?? theme.surface;
        if (this.dot) {
            let dot = Math.fround((slot?.dotSize ?? 8) + Math.fround(ring * 2));
            return new Box(new BoxStyle({ width: dot, height: dot, background: fill, cornerRadius: new CornerRadii(theme.shape('full')), borderWidth: ring, borderColor: ringColor }));
        }
        let label = this.count > this.max ? `${$eq.text.format(this.max, null, undefined, undefined, 'int32')}+` : `${$eq.text.format(this.count, null, undefined, undefined, 'int32')}`;
        let text = new Text(label, 'caption', textColor, 1, 'start', false, false, null, 0, { styleOverride: new TypeStyle(slot?.countLabelSize ?? 10, slot?.countLabelLineHeight ?? 12, slot?.countLabelWeight ?? 'bold', 0, Math.fround(1.3)) });
        let content = VisualNodeExtensions.centered(text);
        let height = Math.fround((slot?.countHeight ?? 16) + Math.fround(ring * 2));
        return new Box(new BoxStyle({ height: height, minWidth: height, padding: EdgeInsets.symmetric(slot?.countPaddingX ?? 4, 0), background: fill, cornerRadius: new CornerRadii(theme.shape('full')), borderWidth: ring, borderColor: ringColor }), content);
    }

    static asDot(variant: VariantValue = 'destructive') {
        return new Badge(0, 99, variant, { dot: true });
    }

    static over(content: VisualNode, count: number) {
        let stack = new Stack();
        stack.add(content);
        stack.add(new Positioned(new Badge(count, 99, 'destructive', { ring: true }), -4, -8));
        return stack;
    }
}

