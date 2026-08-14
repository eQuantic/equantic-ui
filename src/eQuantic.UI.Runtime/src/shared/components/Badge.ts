import { Box, BoxStyle, BuildContext, CornerRadii, EdgeInsets, Positioned, Stack, StatelessComponent, Text, TypeStyle, VariantValue, VisualNode } from "../runtime-exports";

export class Badge extends StatelessComponent {
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
        this.count = count;this.max = max;this.variant = variant;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let fill = this.neutral ? theme.surfaceSubtle : theme.colors(this.variant).base;let textColor = this.neutral ? theme.textSecondary : theme.colors(this.variant).onBase;if (this.dot) {return new Box(new BoxStyle({ width: this.ring ? 12 : 8, height: this.ring ? 12 : 8, background: fill, cornerRadius: new CornerRadii(theme.shape('full')), borderWidth: this.ring ? 2 : 0, borderColor: theme.surface }));}let label = this.count > this.max ? `${this.max}+` : `${this.count}`;let text = new Text(label, 'caption', textColor, 1, 'start', false, false, null, { styleOverride: new TypeStyle(10, 12, 'bold', 0, 1.3) });let content = text.centered();return new Box(new BoxStyle({ height: this.ring ? 20 : 16, minWidth: this.ring ? 20 : 16, padding: EdgeInsets.symmetric(4, 0), background: fill, cornerRadius: new CornerRadii(theme.shape('full')), borderWidth: this.ring ? 2 : 0, borderColor: theme.surface }), content);
    }

    static asDot(variant: VariantValue = 'destructive') {
        return new Badge(0, 99, variant, { dot: true });
    }

    static over(content: VisualNode, count: number) {
        let stack = new Stack();stack.add(content);stack.add(new Positioned(new Badge(count, 99, 'destructive', { ring: true }), -4, -8));return stack;
    }

}

