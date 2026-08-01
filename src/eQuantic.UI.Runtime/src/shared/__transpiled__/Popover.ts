import { Anchored, Box, BoxStyle, BuildContext, CornerRadii, EdgeInsets, StatelessComponent, VisualNode } from "@equantic/runtime";

export class Popover extends StatelessComponent {
    declare trigger: VisualNode;
    declare content: VisualNode;
    declare open: boolean;
    declare onDismiss: () => void;
    declare placement: string;
    constructor(trigger?: any, content?: any, open?: any, onDismiss: any = null, props?: any) {
        super(props);
        if (trigger !== undefined) this.trigger = trigger;
        if (content !== undefined) this.content = content;
        if (open !== undefined) this.open = open;
        if (onDismiss !== undefined) this.onDismiss = onDismiss;
        if (this.placement === undefined) this.placement = 'bottomStart';
        this.trigger = trigger;this.content = content;this.open = open;this.onDismiss = onDismiss;
    }

    build(context: BuildContext) {
        let theme = context.theme;let panel = new Box(new BoxStyle({ background: theme.surface, cornerRadius: new CornerRadii(theme.shape('medium')), borderWidth: 1, borderColor: theme.border, elevation: 2, padding: EdgeInsets.all(12) }), this.content);return new Anchored(this.trigger, panel, { placement: this.placement, open: this.open, onDismiss: this.onDismiss });
    }

}

