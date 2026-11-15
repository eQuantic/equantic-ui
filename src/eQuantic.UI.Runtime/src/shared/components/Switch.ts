import { Box, BoxStyle, BuildContext, ControlMetrics, CornerRadii, DensityValue, Draggable, Positioned, Pressable, Sizing, Stack, StatelessComponent } from "../runtime-exports";

export class Switch extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.Switch';
    declare on: boolean;
    declare onChanged: (() => void) | null;
    declare disabled: boolean;
    declare label: any;

    constructor(on?: any, onChanged: any = null, props?: any) {
        super();
        if (on !== undefined) this.on = on;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (this.on === undefined) this.on = false;
        if (this.disabled === undefined) this.disabled = false;
        this.on = on;
        this.onChanged = onChanged;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let slot = theme.selectionControls;
        let trackFill = this.on ? theme.colors('primary').base : slot?.switchOffTrack ?? theme.borderStrong;
        if (this.disabled) trackFill = trackFill.withOpacity(theme.disabledOpacity);
        let density: DensityValue = context.density;
        let width = ControlMetrics.atDensity((slot?.switchWidth ?? null), density) ?? Sizing.switchWidth(density);
        let height = ControlMetrics.atDensity((slot?.switchHeight ?? null), density) ?? Sizing.switchHeight(density);
        let knob = ControlMetrics.atDensity((slot?.switchThumb ?? null), density) ?? Sizing.switchThumb(density);
        let inset = slot?.switchInset ?? 3;
        let travel = Math.fround(Math.fround(width - knob) - Math.fround(inset * 2));
        let track = new Box(new BoxStyle({ width: width, height: height, background: trackFill, cornerRadius: new CornerRadii(theme.shape('full')) }));
        let thumb = new Box(new BoxStyle({ width: knob, height: knob, background: slot?.thumbFill ?? theme.surface, cornerRadius: new CornerRadii(theme.shape('full')), elevation: slot?.switchThumbElevation ?? 1 }));
        let draggableThumb = this.disabled ? thumb : new Draggable(thumb, (offset: number) => this.release(offset, travel), { axis: 'horizontal', min: this.on ? -travel : 0, max: this.on ? 0 : travel });
        let stack = new Stack();
        stack.add(track);
        stack.add(this.on ? new Positioned(draggableThumb, inset, inset) : new Positioned(draggableThumb, inset, null, null, inset));
        return new Pressable(stack, this.disabled ? null : this.onChanged, { disabled: this.disabled, role: 'switch', selected: this.on, label: this.label });
    }

    release(offset: number, travel: number) {
        let next = this.on ? offset > Math.fround(-travel / 2) : offset >= Math.fround(travel / 2);
        if (next !== this.on) this.onChanged?.();
    }
}

