import { $eq, AccordionItem, Box, BoxStyle, BuildContext, Column, Divider, EdgeInsets, Flexible, Icon, Pressable, Row, SizeValue, Sizing, Spacer, StatefulComponent, StyleDiff, Text, UiComponent } from "@equantic/runtime";

export class Accordion extends StatefulComponent {
    _openSingle: number = -1;
    _openMulti: any = new Set();
    declare items: AccordionItem[];
    declare multiple: boolean;

    constructor(items?: any, openIndex: any = -1, props?: any) {
        super();
        if (items !== undefined) this.items = items;
        if (this.multiple === undefined) this.multiple = false;
        this.items = items;
        this._openSingle = openIndex;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let column = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        for (let i = 0; i < this.items.length; i++) {
            let item = this.items[i];
            let index = i;
            let open = this.isOpen(i);
            let header = new Row(8, 'start', 'center', false, null, null, { cross: 'center', width: SizeValue.fill, height: SizeValue.fill });
            header.add(new Text(item.title, 'label'));
            header.add(new Flexible(new Spacer()));
            header.add(new Icon(open ? 'chevronUp' : 'chevronDown', 16, theme.textSecondary));
            column.add(new Pressable(new Box(new BoxStyle({ height: Sizing.height('large'), width: SizeValue.fill, padding: EdgeInsets.symmetric(12, 0), hover: new StyleDiff({ background: theme.surfaceSubtle }) }), header), () => this.toggle(index), { expanded: open }));
            let content: any; 
            if (open && (content = item.content) != null) {
                column.add(new Box(new BoxStyle({ width: SizeValue.fill, padding: new EdgeInsets(12, 0, 12, 12) }), content));
            }
            if (i < this.items.length - 1) column.add(new Divider());
        }
        return column;
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if ((next instanceof Accordion && (fresh = next, true))) this.items = fresh.items;
    }

    isOpen(index: number) {
        return this.multiple ? this._openMulti.has(index) : this._openSingle === index;
    }

    toggle(index: number) {
        this.setState(() => {
            if (this.multiple) {
                if (!$eq.collections.setAdd(this._openMulti, index)) this._openMulti.delete(index);
            } else {
                this._openSingle = this._openSingle === index ? -1 : index;
            }
        });
    }
}

