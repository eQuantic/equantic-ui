import { Box, BoxStyle, BuildContext, Column, CornerRadii, EdgeInsets, Icon, IconsValue, Row, SizeValue, Spacer, StatelessComponent, Text, TypeStyle } from "@equantic/runtime";

export class EmptyState extends StatelessComponent {
    declare icon: IconsValue;
    declare title: string;
    declare body: any;
    declare action: any;
    declare secondaryAction: any;

    constructor(icon?: any, title?: any, body: any = null, props?: any) {
        super();
        if (icon !== undefined) this.icon = icon;
        if (title !== undefined) this.title = title;
        if (body !== undefined) this.body = body;
        if (this.icon === undefined) this.icon = 'search';
        this.icon = icon;
        this.title = title;
        this.body = body;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let wellContent = new Icon(this.icon, 32, theme.textMuted).centered();
        let well = new Box(new BoxStyle({ width: 64, height: 64, background: theme.surfaceSubtle, cornerRadius: new CornerRadii(theme.shape('full')) }), wellContent);
        let column = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill, cross: 'center', padding: EdgeInsets.symmetric(16, 48) });
        column.add(well);
        column.add(Spacer.fixed(16));
        column.add(new Text(this.title, 'title', theme.textPrimary, 2, 'start', false, false, null, { styleOverride: new TypeStyle(20, 26, 'semiBold', 0, 1.3) }));
        let body: any; 
        if ((body = this.body) != null) {
            column.add(Spacer.fixed(6));
            column.add(new Text(body, 'bodyM', theme.textSecondary, 2));
        }
        if (!(this.action == null) || !(this.secondaryAction == null)) {
            column.add(Spacer.fixed(20));
            let actions = new Row(8, 'start', 'center', false, null, null, { main: 'center' });
            let action: any; 
            if ((action = this.action) != null) actions.add(action);
            let secondary: any; 
            if ((secondary = this.secondaryAction) != null) actions.add(secondary);
            column.add(actions);
        }
        return column;
    }
}

