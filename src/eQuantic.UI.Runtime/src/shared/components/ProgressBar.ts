import { $eq, Box, BoxStyle, BuildContext, Component, ComponentContext, CornerRadii, Flexible, HtmlElement, LoopMotion, Radius, Row, SizeValue, Spacer, StatelessComponent, VisualNode } from "../runtime-exports";

export class ProgressBar extends StatelessComponent {
    static sweepFromX: number = -0.35;
    static sweepToX: number = 1.05;
    static sweepDurationMs: number = 1200;
    constructor(value: any = null, variant: any = 'primary', props?: any) {
        super(props);
        if (value !== undefined) this.value = value;
        if (variant !== undefined) this.variant = variant;
        this.value = value;this.variant = variant;
    }

    build(context: BuildContext) {
        let theme = context.theme;let height = this.prominent ? 8 : 4;let value; if (((this.value != null) && (value = this.value, true))) {let clamped = Math.min(Math.max(value, 0), 1);let filledWeight = Math.trunc($eq.math.round(clamped * 1000));let track = new Row(0, { width: SizeValue.fill, height: height, background: theme.surfaceSubtle, cornerRadius: new CornerRadii(Radius.full) });if (filledWeight > 0) {track.add(new Flexible(new Box(new BoxStyle({ height: height, background: theme.colors(this.variant).base, cornerRadius: new CornerRadii(Radius.full) })), filledWeight));}if (filledWeight < 1000) {track.add(new Spacer(1000 - filledWeight));}return track;}let segment = new Row(0, { width: SizeValue.fill, height: height });segment.add(new Flexible(new Box(new BoxStyle({ height: height, background: theme.colors(this.variant).base, cornerRadius: new CornerRadii(Radius.full) })), 300));segment.add(new Spacer(700));return new Box(new BoxStyle({ width: SizeValue.fill, height: height, background: theme.surfaceSubtle, cornerRadius: new CornerRadii(Radius.full), clip: true }), new LoopMotion(segment, 'slideX', ProgressBar.sweepFromX, ProgressBar.sweepToX, ProgressBar.sweepDurationMs));
    }

}

