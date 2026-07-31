import { Box, BoxStyle, BuildContext, Color, ColorToken, CornerRadii, Flexible, LinearGradient, LoopMotion, Row, SizeValue, StatelessComponent } from "../runtime-exports";

export class Skeleton extends StatelessComponent {
    static shimmerDurationMs: number = 1400;
    declare shape: string;
    declare width: number;
    declare height: number;
    constructor(shape?: any, width?: any, height: any = 0, props?: any) {
        super(props);
        if (shape !== undefined) this.shape = shape;
        if (width !== undefined) this.width = width;
        if (height !== undefined) this.height = height;
        this.shape = shape;this.width = width;this.height = height;
    }

    build(context: BuildContext) {
        let theme = context.theme;let height = (() => { const _s = this.shape; if (_s === 'line') return 12; if (_s === 'circle') return this.width; return this.height > 0 ? this.height : this.width; })();let radius = (() => { const _s = this.shape; if (_s === 'line') return theme.shape('full'); if (_s === 'circle') return theme.shape('full'); return theme.shape('medium'); })();let glint = new Row(0, { width: SizeValue.fill, height: height });glint.add(new Flexible(new Box(new BoxStyle({ height: height, gradient: new LinearGradient(new ColorToken(Color.transparent), theme.surfaceHighlight) })), 1));glint.add(new Flexible(new Box(new BoxStyle({ height: height, gradient: new LinearGradient(theme.surfaceHighlight, new ColorToken(Color.transparent)) })), 1));return new Box(new BoxStyle({ width: this.width, height: height, background: theme.surfaceSubtle, cornerRadius: new CornerRadii(radius), clip: true }), new LoopMotion(glint, 'slideX', -1, 1, Skeleton.shimmerDurationMs, { hideAtRest: true }));
    }

}

