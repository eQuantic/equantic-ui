import { $eq, Box, BoxStyle, BuildContext, ContentStyle, Crumb, Icon, IconGlyph, Link, NavigationTheme, Row, StatelessComponent, Text, VisualNode } from "../runtime-exports";

export class Breadcrumb extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.Breadcrumb';
    declare crumbs: Crumb[];
    declare style: any;
    declare crumbBuilder: ((value: number) => VisualNode) | null;

    constructor(crumbs?: any, style: any = null, crumbBuilder: any = null, props?: any) {
        super();
        if (crumbs !== undefined) this.crumbs = crumbs;
        if (style !== undefined) this.style = style;
        if (crumbBuilder !== undefined) this.crumbBuilder = crumbBuilder;
        this.crumbs = crumbs;
        this.style = style;
        this.crumbBuilder = crumbBuilder;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let slot = NavigationTheme.merge(theme.navigation, this.style);
        let row = new Row(slot?.crumbGap ?? 4, 'start', 'center', false, null, null, { cross: 'center' });
        for (let i = 0; i < $eq.collections.count(this.crumbs); i = i + 1 | 0) {
            let crumb = $eq.collections.item(this.crumbs, i);
            let last = i === ($eq.collections.count(this.crumbs) - 1 | 0);
            if (i > 0) row.add(new Icon(IconGlyph.fromIcons('chevronRight'), slot?.separatorSize ?? 16, slot?.separatorColor ?? theme.borderStrong));
            let tint = last ? slot?.currentCrumbColor ?? theme.textPrimary : slot?.crumbColor ?? theme.textSecondary;
            let builder: any; 
            let content = (builder = this.crumbBuilder) != null ? new Box(new BoxStyle({ foreground: tint, content: new ContentStyle({ role: 'caption', maxLines: 1 }) }), builder(i)) : new Text(crumb.label, 'caption', tint, 1);
            let destination: any; 
            row.add(!last && (destination = crumb.destination) != null ? new Link(destination, content, { label: crumb.label }) : content);
        }
        return row;
    }
}

