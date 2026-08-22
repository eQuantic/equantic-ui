import { Badge, Box, BoxStyle, BuildContext, Column, CornerRadii, EdgeInsets, Icon, IconsValue, MainAlignValue, NavItem, Pressable, SizeValue, StatelessComponent, Text, TypeStyle } from "../runtime-exports";

export class NavigationRail extends StatelessComponent {
    declare $items: NavItem[];

    get items() {
        return this.$items;
    }

    set items(value) {
        this.$items = (value.length < 3 || value.length > 7) ? (() => { throw new Error('NavigationRail takes 3-7 destinations (spec B4): 2 → Tabs, 8+ → Drawer.'); })() : value;
    }

    declare selected: number;
    declare onSelect: any;
    declare leading: any;
    declare alignment: MainAlignValue;

    constructor(items?: any, selected?: any, onSelect: any = null, props?: any) {
        super();
        if (items !== undefined) this.items = items;
        if (selected !== undefined) this.selected = selected;
        if (onSelect !== undefined) this.onSelect = onSelect;
        if (this.selected === undefined) this.selected = 0;
        if (this.alignment === undefined) this.alignment = 'start';
        this.items = items;
        this.selected = selected;
        this.onSelect = onSelect;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let primary = theme.colors('primary');
        let destinations = new Column(8, 'start', 'stretch', false, null, null, { width: SizeValue.fill, cross: 'center' });
        for (let i = 0; i < this.items.length; i++) {
            let item = this.items[i];
            let isActive = i === this.selected;
            let index = i;
            let filled: any; 
            let glyph: IconsValue = isActive && (filled = item.selectedIcon) != null ? filled : item.icon;
            let tint = isActive ? primary.onSubtle : theme.textMuted;
            let icon = new Icon(glyph, 20, tint);
            let iconNode = item.badgeCount > 0 ? Badge.over(icon, item.badgeCount) : icon;
            let pill = new Box(new BoxStyle({ width: 52, height: 30, background: isActive ? primary.subtle : null, cornerRadius: new CornerRadii(theme.shape('full')) }), iconNode.centered());
            let column = new Column(2, 'start', 'stretch', false, null, null, { width: SizeValue.fill, height: 56, main: 'center', cross: 'center' });
            column.add(pill);
            column.add(new Text(item.label, 'caption', tint, 1, 'start', false, false, null, { styleOverride: isActive ? new TypeStyle(12, 16, 'bold', 0, Math.fround(1.3)) : null }));
            destinations.add(new Pressable(column, this.onSelect == null ? null : () => this.onSelect(index), { label: item.label, pressedBackground: theme.surfaceSubtle, role: 'destination', selected: isActive }));
        }
        let rail = new Column(8, 'start', 'stretch', false, null, null, { width: SizeValue.fill, height: SizeValue.fill, cross: 'center', main: this.alignment });
        let leading: any; 
        if ((leading = this.leading) != null) rail.add(leading);
        rail.add(destinations);
        return new Box(new BoxStyle({ width: 80, height: SizeValue.fill, padding: EdgeInsets.symmetric(0, 12), background: theme.surface, borderWidth: 1, borderColor: theme.border, borderSides: 2 }), rail);
    }
}

