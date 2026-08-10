import { BuildContext, Crumb, Icon, Link, Row, StatelessComponent, Text } from "@equantic/runtime";

export class Breadcrumb extends StatelessComponent {
    declare crumbs: Crumb[];
    constructor(crumbs?: any, props?: any) {
        super();
        if (crumbs !== undefined) this.crumbs = crumbs;
        this.crumbs = crumbs;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let row = new Row(4, 'start', 'center', false, null, null, { cross: 'center' });for (let i = 0; i < this.crumbs.length; i++) {let crumb = this.crumbs[i];let last = i === this.crumbs.length - 1;if (i > 0) row.add(new Icon('chevronRight', 16, theme.borderStrong));let text = new Text(crumb.label, 'caption', last ? theme.textPrimary : theme.textSecondary, 1);let destination: any; row.add(!last && (destination = crumb.destination) != null ? new Link(destination, text, { label: crumb.label }) : text);}return row;
    }

}

