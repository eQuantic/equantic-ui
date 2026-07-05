import { Box, BoxStyle, BuildContext, ColorToken, Component, ComponentContext, CornerRadii, EdgeInsets, HtmlElement, Radius, SizeValue, Space, StatelessComponent, VisualNode } from "../runtime-exports";

export class Card extends StatelessComponent {
    constructor(child?: any, kind: any = 'elevated', props?: any) {
        super(props);
        if (child !== undefined) this.child = child;
        if (kind !== undefined) this.kind = kind;
        if (this.padding === undefined) this.padding = EdgeInsets.all(Space.s4);
        this.child = child;this.kind = kind;
    }

    build(context: BuildContext) {
        let theme = context.theme;let background = this.kind === 'filled' ? theme.surfaceSubtle : theme.surface;let borderWidth = this.kind === 'filled' ? 0 : 1;return new Box(new BoxStyle({ width: this.width, padding: this.padding, background: background, cornerRadius: new CornerRadii(Radius.lg), borderWidth: borderWidth, borderColor: theme.border }), this.child);
    }

}

