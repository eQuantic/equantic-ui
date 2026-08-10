import { Box, BoxStyle, BuildContext, Column, CornerRadii, EdgeInsets, Flexible, Icon, Pressable, Row, SizeValue, StatelessComponent, Text, TypeStyle } from "../runtime-exports";

export class Banner extends StatelessComponent {
    declare status: string;
    declare title: string;
    declare body: any;
    declare primaryAction: any;
    declare secondaryAction: any;
    declare onDismiss: (() => void) | null;
    constructor(status?: any, title?: any, body: any = null, props?: any) {
        super();
        if (status !== undefined) this.status = status;
        if (title !== undefined) this.title = title;
        if (body !== undefined) this.body = body;
        if (this.status === undefined) this.status = 'primary';
        this.status = status;this.title = title;this.body = body;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let tint = context.theme.colors(this.status);let glyph = (() => { const _s = this.status; if (_s === 'success') return 'checkCircle'; if (_s === 'warning') return 'warning'; if (_s === 'destructive') return 'error'; return 'info'; })();let column = new Column(4);column.add(new Text(this.title, 'caption', tint.onSubtle, 2, 'start', false, false, null, { styleOverride: new TypeStyle(13, 18, 'semiBold', 0, 1.3) }));if (this.body != null) {column.add(new Text(this.body, 'caption', tint.onSubtle, 4, 'start', false, false, null, { styleOverride: new TypeStyle(13, 18, 'regular', 0, 1.3) }));}if (this.primaryAction != null || this.secondaryAction != null) {let actions = new Row(8);if (this.primaryAction != null) actions.add(this.primaryAction);if (this.secondaryAction != null) actions.add(this.secondaryAction);column.add(actions);}let content = new Row(10, 'start', 'center', false, null, null, { cross: 'start' });content.add(new Icon(glyph, 20, tint.onSubtle));content.add(new Flexible(column));if (this.onDismiss != null) {content.add(new Pressable(new Icon('close', 20, tint.onSubtle), this.onDismiss, { label: 'Dismiss' }));}return new Box(new BoxStyle({ width: SizeValue.fill, padding: new EdgeInsets(14, 12, 14, 12), background: tint.subtle, cornerRadius: new CornerRadii(context.theme.shape('large')) }), content);
    }

}

