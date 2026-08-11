import { Anchored, Box, BoxStyle, BuildContext, CornerRadii, EdgeInsets, StatelessComponent, VisualNode } from "../runtime-exports";

export class Popover extends StatelessComponent {
    declare trigger: VisualNode;
    declare content: VisualNode;
    declare open: boolean;
    declare onDismiss: (() => void) | null;
    declare placement: string;
    constructor(trigger?: any, content?: any, open?: any, onDismiss: any = null, props?: any) {
        super();
        if (trigger !== undefined) this.trigger = trigger;
        if (content !== undefined) this.content = content;
        if (open !== undefined) this.open = open;
        if (onDismiss !== undefined) this.onDismiss = onDismiss;
        if (this.open === undefined) this.open = false;
        if (this.placement === undefined) this.placement = 'bottomStart';
        this.trigger = trigger;this.content = content;this.open = open;this.onDismiss = onDismiss;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let panel = new Box(new BoxStyle({ background: theme.surface, cornerRadius: new CornerRadii(theme.shape('medium')), borderWidth: 1, borderColor: theme.border, elevation: 2, padding: EdgeInsets.all(12) }), this.content);if (context.inFlow) return panel;return new Anchored(this.trigger, panel, { placement: this.placement, open: this.open, onDismiss: this.onDismiss });
    }

}

