import { $eq, Box, BoxStyle, BuildContext, ColorToken, Component, ComponentContext, CornerRadii, HtmlElement, Positioned, Pressable, Radius, Stack, StatelessComponent, VisualNode } from "@equantic/runtime";

export class Switch extends StatelessComponent {
    constructor(on?: any, onChanged: any = null, props?: any) {
        super(props);
        if (on !== undefined) this.on = on;
        if (onChanged !== undefined) this.onChanged = onChanged;
        this.on = on;this.onChanged = onChanged;
    }

    build(context: BuildContext) {
        let theme = context.theme;let trackFill = this.on ? theme.colors('primary').base : theme.borderStrong;if (this.disabled) trackFill = trackFill.withOpacity(theme.disabledOpacity);let track = new Box(new BoxStyle({ width: 52, height: 32, background: trackFill, cornerRadius: new CornerRadii(Radius.full) }));let thumb = new Box(new BoxStyle({ width: 26, height: 26, background: theme.surface, cornerRadius: new CornerRadii(Radius.full), elevation: 1 }));let stack = new Stack();stack.add(track);stack.add(this.on ? new Positioned(thumb, 3, 3) : new Positioned(thumb, 3, null, null, 3));return new Pressable(stack, this.disabled ? null : this.onChanged, { disabled: this.disabled, label: this.on ? 'On' : 'Off' });
    }

}

