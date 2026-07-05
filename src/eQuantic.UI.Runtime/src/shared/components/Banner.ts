import { $eq, Box, BoxStyle, BuildContext, Button, Column, Component, ComponentContext, CornerRadii, EdgeInsets, Flexible, HtmlElement, Icon, IconSize, Pressable, Row, SizeValue, Space, StatelessComponent, Text, TypeStyle, VariantColors, VisualNode } from "../runtime-exports";

export class Banner extends StatelessComponent {
    constructor(status?: any, title?: any, body: any = null, props?: any) {
        super(props);
        if (status !== undefined) this.status = status;
        if (title !== undefined) this.title = title;
        if (body !== undefined) this.body = body;
        this.status = status;this.title = title;this.body = body;
    }

    build(context: BuildContext) {
        let tint = context.theme.colors(this.status);let glyph = (() => { const _s = this.status; if (_s === 'success') return 'checkCircle'; if (_s === 'warning') return 'warning'; if (_s === 'destructive') return 'error'; return 'info'; })();let column = new Column(Space.s1);column.add(new Text(this.title, 'caption', tint.onSubtle, 2, { styleOverride: new TypeStyle(13, 18, 'semiBold', 0, 1.3) }));if (this.body != null) {column.add(new Text(this.body, 'caption', tint.onSubtle, 4, { styleOverride: new TypeStyle(13, 18, 'regular', 0, 1.3) }));}if (this.primaryAction != null || this.secondaryAction != null) {let actions = new Row(Space.s2);if (this.primaryAction != null) actions.add(this.primaryAction);if (this.secondaryAction != null) actions.add(this.secondaryAction);column.add(actions);}let content = new Row(10, { cross: 'start' });content.add(new Icon(glyph, IconSize.dense, tint.onSubtle));content.add(new Flexible(column));if (this.onDismiss != null) {content.add(new Pressable(new Icon('close', IconSize.dense, tint.onSubtle), this.onDismiss, { label: 'Dismiss' }));}return new Box(new BoxStyle({ width: SizeValue.fill, padding: new EdgeInsets(14, 12, 14, 12), background: tint.subtle, cornerRadius: new CornerRadii(context.theme.shape('large')) }), content);
    }

}

