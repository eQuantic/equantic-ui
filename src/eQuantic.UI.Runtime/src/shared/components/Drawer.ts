import { Box, BoxStyle, BuildContext, EdgeInsets, KeyChord, Overlay, Positioned, Presence, Pressable, Shortcut, SizeValue, Stack, StatelessComponent, VisualNode } from "../runtime-exports";

export class Drawer extends StatelessComponent {
    declare content: VisualNode;
    declare open: boolean;
    declare onDismiss: (() => void) | null;
    declare edge: string;
    declare width: number;
    constructor(content?: any, open?: any, onDismiss: any = null, props?: any) {
        super();
        if (content !== undefined) this.content = content;
        if (open !== undefined) this.open = open;
        if (onDismiss !== undefined) this.onDismiss = onDismiss;
        if (this.edge === undefined) this.edge = 'start';
        if (this.width === undefined) this.width = 320;
        this.content = content;this.open = open;this.onDismiss = onDismiss;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;if (!this.open && !context.inFlow) return new Box();let layer = new Stack();layer.add(new Pressable(new Box(new BoxStyle({ width: SizeValue.fill, height: SizeValue.fill, background: theme.scrim })), this.onDismiss, { label: 'Dismiss' }));let panel = new Box(new BoxStyle({ width: this.width, height: context.inFlow ? undefined : SizeValue.fill, background: theme.surface, elevation: 3, padding: EdgeInsets.all(16) }), this.content);if (context.inFlow) return panel;layer.add(this.edge === 'start' ? new Positioned(new Presence(panel), 0, null, 0, 0) : new Positioned(new Presence(panel), 0, 0, 0));let overlay = new Overlay(layer);let escape: any; return (escape = this.onDismiss) != null ? new Shortcut(overlay, KeyChord.escape, escape) : overlay;
    }

}

