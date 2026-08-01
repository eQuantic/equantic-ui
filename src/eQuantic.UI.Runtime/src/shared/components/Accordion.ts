import { Box, BoxStyle, BuildContext, Column, Divider, EdgeInsets, Flexible, Icon, Pressable, Row, SharedStatefulComponent, SizeValue, Spacer, StyleDiff, Text, UiComponent } from "../runtime-exports";

export class Accordion extends SharedStatefulComponent {
    _openSingle: number = -1;
    _openMulti: any = new Set();
    declare items: any;
    declare multiple: boolean;
    constructor(items?: any, openIndex: any = -1, props?: any) {
        super(props);
        if (items !== undefined) this.items = items;
        this.items = items;this._openSingle = openIndex;
    }

    build(context: BuildContext) {
        let theme = context.theme;let column = new Column(0, { width: SizeValue.fill });for (let i = 0; i < this.items.length; i++) {let item = this.items[i];let index = i;let open = this.isOpen(i);let header = new Row(8, { cross: 'center', width: SizeValue.fill, height: SizeValue.fill });header.add(new Text(item.title, 'label'));header.add(new Flexible(new Spacer()));header.add(new Icon(open ? 'chevronUp' : 'chevronDown', 16, theme.textSecondary));column.add(new Pressable(new Box(new BoxStyle({ height: 48, width: SizeValue.fill, padding: EdgeInsets.symmetric(12, 0), hover: new StyleDiff({ background: theme.surfaceSubtle }) }), header), () => this.toggle(index)));let content; if (open && ((item.content != null) && (content = item.content, true))) {column.add(new Box(new BoxStyle({ width: SizeValue.fill, padding: new EdgeInsets(12, 0, 12, 12) }), content));}if (i < this.items.length - 1) column.add(new Divider());}return column;
    }

    adoptConfig(next: UiComponent) {
        let fresh; if ((next instanceof Accordion && (fresh = next, true))) this.items = fresh.items;
    }

    isOpen(index: number) {
        return this.multiple ? this._openMulti.has(index) : this._openSingle === index;
    }

    toggle(index: number) {
        this.setState(() => {if (this.multiple) {if (!this._openMulti.add(index)) this._openMulti.delete(index);} else {this._openSingle = this._openSingle === index ? -1 : index;}});
    }

}

