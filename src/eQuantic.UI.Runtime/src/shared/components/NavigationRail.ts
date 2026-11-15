import { $eq, Badge, Box, BoxStyle, BuildContext, Column, CornerRadii, EdgeInsets, Icon, IconGlyph, IconsValue, MainAlignValue, NavItem, Pressable, SizeValue, StatelessComponent, StyleDiff, Text, TypeStyle, VisualNodeExtensions } from "../runtime-exports";

export class NavigationRail extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.NavigationRail';
    declare $items: NavItem[];

    get items() {
        return this.$items;
    }

    set items(value) {
        this.$items = (($0) => ($0 < 3 || $0 > 7))($eq.collections.count(value)) ? $eq.exceptions.raise($eq.exceptions.create(['System.ArgumentException', 'System.SystemException', 'System.Exception'], 'NavigationRail takes 3-7 destinations (spec B4): 2 → Tabs, 8+ → Drawer.', { paramName: 'Items' })) : value;
    }

    declare selected: number;
    declare onSelect: ((int: number) => void) | null;
    declare leading: any;
    declare alignment: MainAlignValue;

    constructor(items?: any, selected?: any, onSelect: any = null, props?: any) {
        super();
        if (items !== undefined) this.items = items;
        if (selected !== undefined) this.selected = selected;
        if (onSelect !== undefined) this.onSelect = onSelect;
        if (this.selected === undefined) this.selected = 0;
        if (this.alignment === undefined) this.alignment = 'start';
        this.$items = null!;
        this.items = items;
        this.selected = selected;
        this.onSelect = onSelect;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let primary = theme.colors('primary');
        let slot = theme.navigation;
        let gap = slot?.railGap ?? 8;
        let destinations = new Column(gap, 'start', 'stretch', false, null, null, { width: SizeValue.fill, cross: 'center' });
        for (let i = 0; i < $eq.collections.count(this.items); i++) {
            let item = $eq.collections.item(this.items, i);
            let isActive = i === this.selected;
            let index = i;
            let filled: any; 
            let glyph: IconsValue = isActive && (filled = item.selectedIcon) != null ? filled : item.icon;
            let tint = isActive ? primary.onSubtle : slot?.inactiveColor ?? theme.textMuted;
            let icon = new Icon(IconGlyph.fromIcons(glyph), slot?.railIconSize ?? 20, tint);
            let iconNode = item.badgeCount > 0 ? Badge.over(icon, item.badgeCount) : icon;
            let pill = new Box(new BoxStyle({ width: slot?.railPillWidth ?? 52, height: slot?.railPillHeight ?? 30, background: isActive ? primary.subtle : null, cornerRadius: new CornerRadii(slot?.pillRadius ?? theme.shape('full')), pressed: isActive ? null : new StyleDiff({ background: primary.subtle.withOpacity(slot?.pillPressOpacity ?? Math.fround(0.4)) }) }), VisualNodeExtensions.centered(iconNode));
            let column = new Column(slot?.pillLabelGap ?? 2, 'start', 'stretch', false, null, null, { width: SizeValue.fill, height: slot?.railItemHeight ?? 56, main: 'center', cross: 'center' });
            column.add(pill);
            column.add(new Text(item.label, 'caption', tint, 1, 'start', false, false, null, 0, { styleOverride: isActive ? new TypeStyle(slot?.railSelectedLabelSize ?? 12, slot?.railSelectedLabelLineHeight ?? 16, slot?.railSelectedLabelWeight ?? 'bold', 0, Math.fround(1.3)) : null }));
            destinations.add(new Pressable(column, this.onSelect == null ? null : () => this.onSelect!(index), { label: item.label, role: 'destination', selected: isActive }));
        }
        let rail = new Column(gap, 'start', 'stretch', false, null, null, { width: SizeValue.fill, height: SizeValue.fill, cross: 'center', main: this.alignment });
        let leading: any; 
        if ((leading = this.leading) != null) rail.add(leading);
        rail.add(destinations);
        return new Box(new BoxStyle({ width: slot?.railWidth ?? 80, height: SizeValue.fill, padding: EdgeInsets.symmetric(0, slot?.railPaddingY ?? 12), background: slot?.background ?? theme.surface, borderWidth: slot?.railBorderWidth ?? 1, borderColor: slot?.railBorderColor ?? theme.border, borderSides: 2 }), rail);
    }
}

