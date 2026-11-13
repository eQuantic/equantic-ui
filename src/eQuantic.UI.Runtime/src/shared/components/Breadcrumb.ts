import { $eq, BuildContext, Crumb, Icon, IconGlyph, Link, Row, StatelessComponent, Text } from "../runtime-exports";

export class Breadcrumb extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.Breadcrumb';
    declare crumbs: Crumb[];

    constructor(crumbs?: any, props?: any) {
        super();
        if (crumbs !== undefined) this.crumbs = crumbs;
        this.crumbs = crumbs;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let slot = theme.navigation;
        let row = new Row(slot?.crumbGap ?? 4, 'start', 'center', false, null, null, { cross: 'center' });
        for (let i = 0; i < $eq.collections.count(this.crumbs); i++) {
            let crumb = $eq.collections.item(this.crumbs, i);
            let last = i === $eq.collections.count(this.crumbs) - 1;
            if (i > 0) row.add(new Icon(IconGlyph.fromIcons('chevronRight'), slot?.separatorSize ?? 16, slot?.separatorColor ?? theme.borderStrong));
            let text = new Text(crumb.label, 'caption', last ? slot?.currentCrumbColor ?? theme.textPrimary : slot?.crumbColor ?? theme.textSecondary, 1);
            let destination: any; 
            row.add(!last && (destination = crumb.destination) != null ? new Link(destination, text, { label: crumb.label }) : text);
        }
        return row;
    }
}

