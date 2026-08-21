import { Box, BoxStyle, BuildContext, EdgeInsets, Flexible, Row, SizeValue, StatelessComponent, Text, TypeStyle } from "@equantic/runtime";

export class AppBar extends StatelessComponent {
    declare title: string;
    declare leading: any;
    declare $actions: any;
    get actions() { return this.$actions; }
    set actions(value) {
        this.$actions = (value != null && value.length > 3) ? (() => { throw new Error('AppBar takes at most 3 actions (spec B3) — overflow belongs in an ActionSheet.'); })() : value;
    }
    declare scrolled: boolean;
    constructor(title?: any, props?: any) {
        super();
        if (title !== undefined) this.title = title;
        if (this.scrolled === undefined) this.scrolled = false;
        this.title = title;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let row = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, height: SizeValue.fill, cross: 'center' });
        let leading: any; 
        if ((leading = this.leading) != null) row.add(leading);
        let title = new Text(this.title, 'title', theme.textPrimary, 1, 'start', false, false, null, { styleOverride: new TypeStyle(20, 26, 'semiBold', 0, 1.3) });
        let titlePad = new Box(new BoxStyle({ padding: new EdgeInsets(this.leading == null ? 12 : 8, 0, 8, 0) }), title);
        row.add(new Flexible(titlePad));
        let actions: any; 
        if ((actions = this.actions) != null) {
            for (const action of actions) row.add(action);
        }
        return new Box(new BoxStyle({ width: SizeValue.fill, height: 56, padding: EdgeInsets.symmetric(4, 0), background: this.scrolled ? theme.surface : null, elevation: this.scrolled ? 2 : 0 }), row);
    }

}

