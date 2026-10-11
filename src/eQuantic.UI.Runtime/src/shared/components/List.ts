import { $eq, BuildContext, Column, Divider, ListItem, SizeValue, StatelessComponent } from "../runtime-exports";

export class List extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.List';
    declare items: ListItem[];
    declare dividers: boolean;

    constructor(items?: any, dividers: any = true, props?: any) {
        super();
        if (items !== undefined) this.items = items;
        if (dividers !== undefined) this.dividers = dividers;
        if (this.dividers === undefined) this.dividers = false;
        this.items = items;
        this.dividers = dividers;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(_context: BuildContext) {
        let column = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        for (let i = 0; i < $eq.collections.count(this.items); i++) {
            column.add($eq.collections.item(this.items, i));
            if (this.dividers && i < $eq.collections.count(this.items) - 1) column.add(new Divider('leading', 'horizontal', { leadingInset: $eq.collections.item(this.items, i).contentInset }));
        }
        return column;
    }
}

