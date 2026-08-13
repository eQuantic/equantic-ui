import { Box, BoxStyle, BuildContext, CornerRadii, Draggable, Positioned, Pressable, Sizing, Stack, StatelessComponent } from "@equantic/runtime";

export class Switch extends StatelessComponent {
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
        this.on = on;this.onChanged = onChanged;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let trackFill = this.on ? theme.colors('primary').base : theme.borderStrong;if (this.disabled) trackFill = trackFill.withOpacity(theme.disabledOpacity);let density = context.density;let travel = Sizing.switchTravel(density);let inset = 3;let track = new Box(new BoxStyle({ width: Sizing.switchWidth(density), height: Sizing.switchHeight(density), background: trackFill, cornerRadius: new CornerRadii(theme.shape('full')) }));let thumb = new Box(new BoxStyle({ width: Sizing.switchThumb(density), height: Sizing.switchThumb(density), background: theme.surface, cornerRadius: new CornerRadii(theme.shape('full')), elevation: 1 }));let draggableThumb = this.disabled ? thumb : new Draggable(thumb, (offset: number) => this.release(offset, travel), { axis: 'horizontal', min: this.on ? -travel : 0, max: this.on ? 0 : travel });let stack = new Stack();stack.add(track);stack.add(this.on ? new Positioned(draggableThumb, inset, inset) : new Positioned(draggableThumb, inset, null, null, inset));return new Pressable(stack, this.disabled ? null : this.onChanged, { disabled: this.disabled, role: 'switch', selected: this.on, label: this.label });
    }

    release(offset: number, travel: number) {
        let next = this.on ? offset > -travel / 2 : offset >= travel / 2;if (next !== this.on) this.onChanged?.();
    }

}

