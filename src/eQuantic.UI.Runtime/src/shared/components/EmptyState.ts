import { Box, BoxStyle, BuildContext, Button, Column, CornerRadii, EdgeInsets, Icon, IconSize, Row, SizeValue, Space, Spacer, StatelessComponent, Text, TypeStyle } from "../runtime-exports";

export class EmptyState extends StatelessComponent {
    declare icon: string;
    declare title: string;
    declare body: string;
    declare action: Button;
    declare secondaryAction: Button;
    constructor(icon?: any, title?: any, body: any = null, props?: any) {
        super(props);
        if (icon !== undefined) this.icon = icon;
        if (title !== undefined) this.title = title;
        if (body !== undefined) this.body = body;
        if (this.icon === undefined) this.icon = 'search';
        this.icon = icon;this.title = title;this.body = body;
    }

    build(context: BuildContext) {
        let theme = context.theme;let wellContent = new Row(0, { main: 'center', height: SizeValue.fill });wellContent.add(new Icon(this.icon, IconSize.lg, theme.textMuted));let well = new Box(new BoxStyle({ width: 64, height: 64, background: theme.surfaceSubtle, cornerRadius: new CornerRadii(theme.shape('full')) }), wellContent);let column = new Column(0, { width: SizeValue.fill, cross: 'center', padding: EdgeInsets.symmetric(Space.s4, Space.s12) });column.add(well);column.add(Spacer.fixed(Space.s4));column.add(new Text(this.title, 'title', theme.textPrimary, 2, { styleOverride: new TypeStyle(20, 26, 'semiBold', 0, 1.3) }));let body; if (((this.body != null) && (body = this.body, true))) {column.add(Spacer.fixed(6));column.add(new Text(body, 'bodyM', theme.textSecondary, 2));}if (!(this.action == null) || !(this.secondaryAction == null)) {column.add(Spacer.fixed(Space.s5));let actions = new Row(Space.s2, { main: 'center' });let action; if (((this.action != null) && (action = this.action, true))) actions.add(action);let secondary; if (((this.secondaryAction != null) && (secondary = this.secondaryAction, true))) actions.add(secondary);column.add(actions);}return column;
    }

}

