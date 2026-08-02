import { BuildContext, Crumb, Icon, Link, Row, StatelessComponent, Text } from "@equantic/runtime";

export class Breadcrumb extends StatelessComponent {
    declare crumbs: Crumb[];
    constructor(crumbs?: any, props?: any) {
        super(props);
        if (crumbs !== undefined) this.crumbs = crumbs;
        this.crumbs = crumbs;
    }

    build(context: BuildContext) {
        let theme = context.theme;let row = new Row(4, { cross: 'center' });for (let i = 0; i < this.crumbs.length; i++) {let crumb = this.crumbs[i];let last = i === this.crumbs.length - 1;if (i > 0) row.add(new Icon('chevronRight', 16, theme.borderStrong));let text = new Text(crumb.label, 'caption', last ? theme.textPrimary : theme.textSecondary, 1);let href; row.add(!last && ((crumb.href != null) && (href = crumb.href, true)) ? new Link(href, text, { label: crumb.label }) : text);}return row;
    }

}

