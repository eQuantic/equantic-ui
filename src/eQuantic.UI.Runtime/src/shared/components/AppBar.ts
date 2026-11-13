import { $eq, Box, BoxStyle, BuildContext, EdgeInsets, Flexible, Row, SizeValue, StatelessComponent, Text, TypeStyle } from "../runtime-exports";

export class AppBar extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.AppBar';
    declare title: string;
    declare leading: any;
    declare $actions: any;

    get actions() {
        return this.$actions;
    }

    set actions(value) {
        this.$actions = (value != null && $eq.collections.count(value) > 3) ? $eq.exceptions.raise($eq.exceptions.create(['System.ArgumentException', 'System.SystemException', 'System.Exception'], 'AppBar takes at most 3 actions (spec B3) — overflow belongs in an ActionSheet.', { paramName: 'Actions' })) : value;
    }

    declare scrolled: boolean;

    constructor(title?: any, props?: any) {
        super();
        if (title !== undefined) this.title = title;
        if (this.scrolled === undefined) this.scrolled = false;
        this.$actions = null!;
        this.title = title;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let slot = theme.navigation;
        let row = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, height: SizeValue.fill, cross: 'center' });
        let leading: any; 
        if ((leading = this.leading) != null) row.add(leading);
        let title = new Text(this.title, 'title', slot?.titleColor ?? theme.textPrimary, 1, 'start', false, false, null, 0, { headingLevel: 1, styleOverride: new TypeStyle(slot?.titleSize ?? 20, slot?.titleLineHeight ?? 26, slot?.titleWeight ?? 'semiBold', 0, Math.fround(1.3)) });
        let titleGap = slot?.titleGap ?? 8;
        let titlePad = new Box(new BoxStyle({ padding: new EdgeInsets(this.leading == null ? slot?.titleInset ?? 12 : titleGap, 0, titleGap, 0) }), title);
        row.add(new Flexible(titlePad));
        let actions: any; 
        if ((actions = this.actions) != null) {
            for (const action of actions) row.add(action);
        }
        return new Box(new BoxStyle({ width: SizeValue.fill, height: slot?.appBarHeight ?? 56, padding: EdgeInsets.symmetric(slot?.appBarPaddingX ?? 4, 0), background: this.scrolled ? slot?.background ?? theme.surface : null, elevation: this.scrolled ? slot?.scrolledElevation ?? 2 : 0 }), row);
    }
}

