import { $eq, AccordionItem, Box, BoxStyle, BuildContext, Column, Divider, EdgeInsets, Flexible, Icon, IconGlyph, Pressable, Row, SizeValue, Sizing, Spacer, StatefulComponent, StyleDiff, Text, UiComponent } from "../runtime-exports";

export class Accordion extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Components.Accordion';
    _openSingle: number = -1;
    _openMulti: any = $eq.collections.hashSet();
    _multiple: boolean = false;
    declare items: AccordionItem[];

    get multiple() {
        return this._multiple;
    }

    set multiple(value) {
        this._multiple = value;
    }

    constructor(items?: any, openIndex: any = -1, props?: any) {
        super();
        if (items !== undefined) this.items = items;
        this.items = items;
        this._openSingle = openIndex;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let slot = theme.list;
        let paddingX = slot?.sectionPaddingX ?? 12;
        let column = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        for (let i = 0; i < $eq.collections.count(this.items); i++) {
            let item = $eq.collections.item(this.items, i);
            let index = i;
            let open = this.isOpen(i);
            let header = new Row(slot?.headerGap ?? 8, 'start', 'center', false, null, null, { cross: 'center', width: SizeValue.fill, height: SizeValue.fill });
            header.add(new Text(item.title, 'label'));
            header.add(new Flexible(new Spacer()));
            header.add(new Icon(IconGlyph.fromIcons(open ? 'chevronUp' : 'chevronDown'), slot?.chevronSize ?? 16, slot?.chevronColor ?? theme.textSecondary));
            column.add(new Pressable(new Box(new BoxStyle({ height: slot?.headerHeight ?? Sizing.height('large'), width: SizeValue.fill, padding: EdgeInsets.symmetric(paddingX, 0), hover: new StyleDiff({ background: slot?.quietFill ?? theme.surfaceSubtle }) }), header), () => this.toggle(index), { expanded: open }));
            let content: any; 
            if (open && (content = item.content) != null) {
                column.add(new Box(new BoxStyle({ width: SizeValue.fill, padding: new EdgeInsets(paddingX, 0, paddingX, slot?.sectionPaddingBottom ?? 12) }), content));
            }
            if (i < $eq.collections.count(this.items) - 1) column.add(new Divider());
        }
        return column;
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof Accordion && (fresh = next, true)))) return;
        this.items = fresh.items;
        this._multiple = fresh.multiple;
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

