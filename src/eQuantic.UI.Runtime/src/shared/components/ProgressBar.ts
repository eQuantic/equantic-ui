import { $eq, Box, BoxStyle, BuildContext, CornerRadii, Flexible, LoopMotion, Row, SharedStatefulComponent, SizeValue, Spacer, UiComponent } from "../runtime-exports";

export class ProgressBar extends SharedStatefulComponent {
    static sweepFromX: number = -0.35;
    static sweepToX: number = 1.05;
    static sweepDurationMs: number = 1200;
    _snapNext: boolean = false;
    declare value: any;
    declare variant: string;
    declare prominent: boolean;
    constructor(value: any = null, variant: any = 'primary', props?: any) {
        super();
        if (value !== undefined) this.value = value;
        if (variant !== undefined) this.variant = variant;
        if (this.variant === undefined) this.variant = 'primary';
        this.value = value;this.variant = variant;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let height = this.prominent ? 8 : 4;let value: any; if ((value = this.value) != null) {let animate = !this._snapNext;this._snapNext = false;let clamped = Math.min(Math.max(value, 0), 1);let filledWeight = (Math.trunc($eq.math.round(clamped * 1000)) | 0);let track = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, height: height, background: theme.surfaceSubtle, cornerRadius: new CornerRadii(theme.shape('full')) });if (filledWeight > 0) {track.add(new Flexible(new Box(new BoxStyle({ height: height, background: theme.colors(this.variant).base, cornerRadius: new CornerRadii(theme.shape('full')) })), filledWeight, 0, 1, { animateChanges: animate }));}if (filledWeight < 1000) {track.add(new Spacer(1000 - filledWeight, { animateChanges: animate }));}return track;}let segment = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, height: height });segment.add(new Flexible(new Box(new BoxStyle({ height: height, background: theme.colors(this.variant).base, cornerRadius: new CornerRadii(theme.shape('full')) })), 300));segment.add(new Spacer(700));return new Box(new BoxStyle({ width: SizeValue.fill, height: height, background: theme.surfaceSubtle, cornerRadius: new CornerRadii(theme.shape('full')), clip: true }), new LoopMotion(segment, 'slideX', ProgressBar.sweepFromX, ProgressBar.sweepToX, ProgressBar.sweepDurationMs));
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; if (!((next instanceof ProgressBar && (fresh = next, true)))) return;let incoming: any; let current: any; this._snapNext = (incoming = fresh.value) != null && (current = this.value) != null && incoming < current;this.value = fresh.value;this.variant = fresh.variant;
    }

}

