import { Box, BoxStyle, BuildContext, CornerRadii, Draggable, Positioned, Pressable, Stack, StatelessComponent } from "../runtime-exports";

export class Switch extends StatelessComponent {
    static travel: number = 52 - 26 - 3 - 3;
    declare on: boolean;
    declare onChanged: (() => void) | null;
    declare disabled: boolean;
    constructor(on?: any, onChanged: any = null, props?: any) {
        super(props);
        if (on !== undefined) this.on = on;
        if (onChanged !== undefined) this.onChanged = onChanged;
        this.on = on;this.onChanged = onChanged;
    }

    build(context: BuildContext) {
        let theme = context.theme;let trackFill = this.on ? theme.colors('primary').base : theme.borderStrong;if (this.disabled) trackFill = trackFill.withOpacity(theme.disabledOpacity);let track = new Box(new BoxStyle({ width: 52, height: 32, background: trackFill, cornerRadius: new CornerRadii(theme.shape('full')) }));let thumb = new Box(new BoxStyle({ width: 26, height: 26, background: theme.surface, cornerRadius: new CornerRadii(theme.shape('full')), elevation: 1 }));let draggableThumb = this.disabled ? thumb : new Draggable(thumb, this.onReleased.bind(this), { axis: 'horizontal', min: this.on ? -Switch.travel : 0, max: this.on ? 0 : Switch.travel });let stack = new Stack();stack.add(track);stack.add(this.on ? new Positioned(draggableThumb, 3, 3) : new Positioned(draggableThumb, 3, null, null, 3));return new Pressable(stack, this.disabled ? null : this.onChanged, { disabled: this.disabled, label: this.on ? 'On' : 'Off' });
    }

    onReleased(offset: number) {
        let next = this.on ? offset > -Switch.travel / 2 : offset >= Switch.travel / 2;if (next !== this.on) this.onChanged?.();
    }

}

