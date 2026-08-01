import { Badge, Box, BoxStyle, BuildContext, Column, CornerRadii, Flexible, Icon, IconSize, Positioned, Pressable, Row, SizeValue, Stack, StatelessComponent, Text, TypeStyle } from "@equantic/runtime";

export class BottomNavigation extends StatelessComponent {
    declare items: any;
    declare selected: number;
    declare onSelect: any;
    constructor(items?: any, selected?: any, onSelect: any = null, props?: any) {
        super(props);
        if (items !== undefined) this.items = items;
        if (selected !== undefined) this.selected = selected;
        if (onSelect !== undefined) this.onSelect = onSelect;
        this.items = items;this.selected = selected;this.onSelect = onSelect;
    }

    build(context: BuildContext) {
        let theme = context.theme;let primary = theme.colors('primary');if ((this.items.length < 3 || this.items.length > 5)) throw new Error('BottomNavigation takes 3-5 destinations (spec B4): 2 → Tabs, 6+ → Drawer.');let row = new Row(0, { width: SizeValue.fill, height: SizeValue.fill });for (let i = 0; i < this.items.length; i++) {let item = this.items[i];let isActive = i === this.selected;let index = i;let filled; let glyph = isActive && ((item.selectedIcon != null) && (filled = item.selectedIcon, true)) ? filled : item.icon;let tint = isActive ? primary.onSubtle : theme.textMuted;let icon = new Icon(glyph, IconSize.md, tint);let iconNode = item.badgeCount > 0 ? BottomNavigation.badgedIcon(icon, item.badgeCount) : icon;let pillContent = new Row(0, { main: 'center', height: SizeValue.fill });pillContent.add(iconNode);let pill = new Box(new BoxStyle({ width: 56, height: 26, background: isActive ? primary.subtle : null, cornerRadius: new CornerRadii(theme.shape('full')) }), pillContent);let column = new Column(2, { height: SizeValue.fill, main: 'center', cross: 'center' });column.add(pill);column.add(new Text(item.label, 'caption', tint, 1, { styleOverride: new TypeStyle(11, 14, 'bold', 0.1, 1.3) }));row.add(new Flexible(new Pressable(column, this.onSelect == null ? null : () => this.onSelect(index), { label: item.label, pressedBackground: theme.surfaceSubtle })));}return new Box(new BoxStyle({ width: SizeValue.fill, height: 56, background: theme.surface }), row);
    }

    static badgedIcon(icon: Icon, count: number) {
        let stack = new Stack();stack.add(icon);stack.add(new Positioned(new Badge(count, 99, 'destructive', { ring: true }), -4, -8));return stack;
    }

}

