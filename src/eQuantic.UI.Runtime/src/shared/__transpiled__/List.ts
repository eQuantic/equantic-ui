import { BuildContext, Column, Divider, SizeValue, StatelessComponent } from "@equantic/runtime";

export class List extends StatelessComponent {
    declare items: any;
    declare dividers: boolean;
    constructor(items?: any, dividers: any = true, props?: any) {
        super(props);
        if (items !== undefined) this.items = items;
        if (dividers !== undefined) this.dividers = dividers;
        this.items = items;this.dividers = dividers;
    }

    build(_context: BuildContext) {
        let column = new Column(0, { width: SizeValue.fill });for (let i = 0; i < this.items.length; i++) {column.add(this.items[i]);if (this.dividers && i < this.items.length - 1) column.add(new Divider('leading'));}return column;
    }

}

