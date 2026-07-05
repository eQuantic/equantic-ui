import { $eq, Box, BoxStyle, BuildContext, Component, ComponentContext, CornerRadii, Flexible, HtmlElement, Radius, Row, SizeValue, Spacer, StatelessComponent, VisualNode } from "@equantic/runtime";
import { MathF } from "./MathF";

export class ProgressBar extends StatelessComponent {
    constructor(value?: any, variant: any = 'primary', props?: any) {
        super(props);
        if (value !== undefined) this.value = value;
        if (variant !== undefined) this.variant = variant;
        this.value = value;this.variant = variant;
    }

    build(context: BuildContext) {
        let theme = context.theme;let height = this.prominent ? 8 : 4;let clamped = Math.min(Math.max(this.value, 0), 1);let filledWeight = Math.trunc($eq.math.round(clamped * 1000));let track = new Row(0, { width: SizeValue.fill, height: height, background: theme.surfaceSubtle, cornerRadius: new CornerRadii(Radius.full) });if (filledWeight > 0) {track.add(new Flexible(new Box(new BoxStyle({ height: height, background: theme.colors(this.variant).base, cornerRadius: new CornerRadii(Radius.full) })), filledWeight));}if (filledWeight < 1000) {track.add(new Spacer(1000 - filledWeight));}return track;
    }

}

