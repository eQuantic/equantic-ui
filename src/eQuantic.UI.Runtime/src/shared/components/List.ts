import { $eq, BuildContext, Column, Component, ComponentContext, Divider, HtmlElement, ListItem, SizeValue, StatelessComponent, VisualNode } from "../runtime-exports";

export class List extends StatelessComponent {
    constructor(items?: any, dividers: any = true, props?: any) {
        super(props);
        if (items !== undefined) this.items = items;
        if (dividers !== undefined) this.dividers = dividers;
        this.items = items;this.dividers = dividers;
    }

    build(context: BuildContext) {
        let column = new Column(0, { width: SizeValue.fill });for (let i = 0; i < this.items.length; i++) {column.add(this.items[i]);if (this.dividers && i < this.items.length - 1) column.add(new Divider('leading'));}return column;
    }

}

