import { Badge, Box, BoxStyle, BuildContext, Column, CornerRadii, Flexible, Icon, IconsValue, NavItem, Pressable, Row, SizeValue, StatelessComponent, Text, TypeStyle } from "@equantic/runtime";

export class BottomNavigation extends StatelessComponent {
    declare $items: NavItem[];
    get items() { return this.$items; }
    set items(value) { this.$items = (value.length < 3 || value.length > 5) ? (() => { throw new Error('BottomNavigation takes 3-5 destinations (spec B4): 2 → Tabs, 6+ → Drawer.'); })() : value; }
    declare selected: number;
    declare onSelect: any;
    constructor(items?: any, selected?: any, onSelect: any = null, props?: any) {
        super();
        if (items !== undefined) this.items = items;
        if (selected !== undefined) this.selected = selected;
        if (onSelect !== undefined) this.onSelect = onSelect;
        if (this.selected === undefined) this.selected = 0;
        this.items = items;this.selected = selected;this.onSelect = onSelect;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let primary = theme.colors('primary');let row = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, height: SizeValue.fill });for (let i = 0; i < this.items.length; i++) {let item = this.items[i];let isActive = i === this.selected;let index = i;let filled: any; let glyph: IconsValue = isActive && (filled = item.selectedIcon) != null ? filled : item.icon;let tint = isActive ? primary.onSubtle : theme.textMuted;let icon = new Icon(glyph, 24, tint);let iconNode = item.badgeCount > 0 ? Badge.over(icon, item.badgeCount) : icon;let pillContent = iconNode.centered();let pill = new Box(new BoxStyle({ width: 56, height: 26, background: isActive ? primary.subtle : null, cornerRadius: new CornerRadii(theme.shape('full')) }), pillContent);let column = new Column(2, 'start', 'stretch', false, null, null, { height: SizeValue.fill, main: 'center', cross: 'center' });column.add(pill);column.add(new Text(item.label, 'caption', tint, 1, 'start', false, false, null, { styleOverride: new TypeStyle(11, 14, 'bold', 0.1, 1.3) }));row.add(new Flexible(new Pressable(column, this.onSelect == null ? null : () => this.onSelect(index), { label: item.label, pressedBackground: theme.surfaceSubtle })));}return new Box(new BoxStyle({ width: SizeValue.fill, height: 56, background: theme.surface }), row);
    }

}

