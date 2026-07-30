import { $eq, Box, BoxStyle, BuildContext, Column, Component, ComponentContext, CornerRadii, EdgeInsets, HtmlElement, Overlay, Presence, Pressable, Radius, Row, SizeValue, Space, StatelessComponent, Text, VisualNode } from "../runtime-exports";

export class Toast extends StatelessComponent {
    constructor(message?: any, status: any = 'info', actionLabel: any = null, onAction: any = null, props?: any) {
        super(props);
        if (message !== undefined) this.message = message;
        if (status !== undefined) this.status = status;
        if (actionLabel !== undefined) this.actionLabel = actionLabel;
        if (onAction !== undefined) this.onAction = onAction;
        this.message = message;this.status = status;this.actionLabel = actionLabel;this.onAction = onAction;
    }

    build(context: BuildContext) {
        let theme = context.theme;let row = new Row(Space.s3, { cross: 'center' });row.add(new Box(new BoxStyle({ width: 8, height: 8, background: theme.colors(this.status).base, cornerRadius: new CornerRadii(Radius.full) })));row.add(new Text(this.message, 'bodyM', theme.textInverse, 2));let label; if (((this.actionLabel != null) && (label = this.actionLabel, true))) {row.add(new Pressable(new Box(new BoxStyle({ padding: EdgeInsets.symmetric(Space.s2, Space.s1) }), new Text(label, 'label', theme.textInverse)), this.onAction, { label: label }));}let pill = new Box(new BoxStyle({ background: theme.textPrimary, cornerRadius: new CornerRadii(theme.shape('full')), elevation: 3, padding: EdgeInsets.symmetric(Space.s4, Space.s3), maxWidth: 480 }), row);let anchor = new Column(0, { width: SizeValue.fill, height: SizeValue.fill, main: 'end', cross: 'center', padding: EdgeInsets.symmetric(Space.s4, Space.s6) });anchor.add(new Presence(pill, 'slideUp'));return new Overlay(anchor, { modal: false });
    }

}

