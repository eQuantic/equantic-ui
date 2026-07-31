import { Box, BoxStyle, BuildContext, CornerRadii, EdgeInsets, Row, SizeValue, Space, StatelessComponent, Text, TypeStyle } from "@equantic/runtime";

export class Badge extends StatelessComponent {
    declare count: number;
    declare max: number;
    declare variant: string;
    declare neutral: boolean;
    declare dot: boolean;
    declare ring: boolean;
    constructor(count: any = 0, max: any = 99, variant: any = 'destructive', props?: any) {
        super(props);
        if (count !== undefined) this.count = count;
        if (max !== undefined) this.max = max;
        if (variant !== undefined) this.variant = variant;
        this.count = count;this.max = max;this.variant = variant;
    }

    build(context: BuildContext) {
        let theme = context.theme;let fill = this.neutral ? theme.surfaceSubtle : theme.colors(this.variant).base;let textColor = this.neutral ? theme.textSecondary : theme.colors(this.variant).onBase;if (this.dot) {return new Box(new BoxStyle({ width: this.ring ? 12 : 8, height: this.ring ? 12 : 8, background: fill, cornerRadius: new CornerRadii(theme.shape('full')), borderWidth: this.ring ? 2 : 0, borderColor: theme.surface }));}let label = this.count > this.max ? `${this.max}+` : `${this.count}`;let text = new Text(label, 'caption', textColor, 1, { styleOverride: new TypeStyle(10, 12, 'bold', 0, 1.3) });let content = new Row(0, { main: 'center', height: SizeValue.fill });content.add(text);return new Box(new BoxStyle({ height: this.ring ? 20 : 16, minWidth: this.ring ? 20 : 16, padding: EdgeInsets.symmetric(Space.s1, 0), background: fill, cornerRadius: new CornerRadii(theme.shape('full')), borderWidth: this.ring ? 2 : 0, borderColor: theme.surface }), content);
    }

    static asDot(variant: Variant) {
        return { dot: true };
    }

}

