import { BuildContext, Column, Divider, ListItem, SizeValue, StatelessComponent } from "@equantic/runtime";

export class List extends StatelessComponent {
    declare items: ListItem[];
    declare dividers: boolean;
    constructor(items?: any, dividers: any = true, props?: any) {
        super();
        if (items !== undefined) this.items = items;
        if (dividers !== undefined) this.dividers = dividers;
        if (this.dividers === undefined) this.dividers = false;
        this.items = items;this.dividers = dividers;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(_context: BuildContext) {
        let column = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });for (let i = 0; i < this.items.length; i++) {column.add(this.items[i]);if (this.dividers && i < this.items.length - 1) column.add(new Divider('leading'));}return column;
    }

}

