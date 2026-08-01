import { Box, BoxStyle, BuildContext, EdgeInsets, Overlay, Positioned, Presence, Pressable, SizeValue, Stack, StatelessComponent, VisualNode } from "@equantic/runtime";

export class Drawer extends StatelessComponent {
    declare content: VisualNode;
    declare open: boolean;
    declare onDismiss: () => void;
    declare edge: string;
    declare width: number;
    constructor(content?: any, open?: any, onDismiss: any = null, props?: any) {
        super(props);
        if (content !== undefined) this.content = content;
        if (open !== undefined) this.open = open;
        if (onDismiss !== undefined) this.onDismiss = onDismiss;
        if (this.edge === undefined) this.edge = 'start';
        if (this.width === undefined) this.width = 320;
        this.content = content;this.open = open;this.onDismiss = onDismiss;
    }

    build(context: BuildContext) {
        let theme = context.theme;if (!this.open) return new Box();let layer = new Stack();layer.add(new Pressable(new Box(new BoxStyle({ width: SizeValue.fill, height: SizeValue.fill, background: theme.scrim })), this.onDismiss, { label: 'Dismiss' }));let panel = new Box(new BoxStyle({ width: this.width, height: SizeValue.fill, background: theme.surface, elevation: 3, padding: EdgeInsets.all(16) }), this.content);layer.add(this.edge === 'start' ? new Positioned(new Presence(panel), 0, null, 0, 0) : new Positioned(new Presence(panel), 0, 0, 0));return new Overlay(layer);
    }

}

