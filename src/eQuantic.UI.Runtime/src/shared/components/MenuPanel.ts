import { $eq, Box, BoxStyle, BuildContext, Column, ControlMetrics, CornerRadii, EdgeInsets, Icon, IconGlyph, KeyChord, MenuItem, Pressable, Row, Shortcut, SizeValue, StyleDiff, Text, VisualNode } from "../runtime-exports";

export class MenuPanel {
    static build(context: BuildContext, items: MenuItem[], open: boolean, highlight: number, choose: (int: number) => void) {
        let theme = context.theme;
        let slot = theme.menu;
        let list = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        for (let i = 0; i < $eq.collections.count(items); i = i + 1 | 0) {
            let item = $eq.collections.item(items, i);
            let index = i;
            let row = new Row(slot?.itemGap ?? 8, 'start', 'center', false, null, null, { cross: 'center', width: SizeValue.fill, height: SizeValue.fill });
            let glyph: any; 
            if ((glyph = item.icon) != null) row.add(new Icon(IconGlyph.fromIcons(glyph), 20, item.destructive ? theme.colors('destructive').base : theme.textSecondary));
            row.add(new Text(item.label, 'bodyM', item.destructive ? theme.colors('destructive').base : theme.textPrimary, 1));
            let surface = new Box(new BoxStyle({ height: ControlMetrics.height(context, 'medium'), padding: slot?.itemPadding ?? EdgeInsets.symmetric(12, 0), width: SizeValue.fill, opacity: item.disabled ? theme.disabledOpacity : null, background: open && index === highlight && !item.disabled ? theme.surfaceSubtle : null, hover: item.disabled ? null : new StyleDiff({ background: theme.surfaceSubtle }) }), row);
            list.add(new Pressable(surface, item.disabled ? null : () => choose(index), { disabled: item.disabled, role: 'menuItem' }));
        }
        return new Box(new BoxStyle({ minWidth: slot?.menuMinWidth ?? 180, background: theme.surface, cornerRadius: new CornerRadii(theme.shape(slot?.panelShape ?? 'medium')), borderWidth: slot?.panelBorderWidth ?? 1, borderColor: theme.border, elevation: slot?.panelElevation ?? 2, padding: slot?.menuPadding ?? EdgeInsets.symmetric(0, 4), clip: true }), list);
    }

    static withKeys(menu: VisualNode, live: boolean, down: () => void, up: () => void, enter: () => void) {
        menu = new Shortcut(menu, KeyChord.arrowDown, down, { enabled: live });
        menu = new Shortcut(menu, KeyChord.arrowUp, up, { enabled: live });
        return new Shortcut(menu, KeyChord.enter, enter, { enabled: live });
    }

    static firstEnabled(items: MenuItem[]) {
        for (let i = 0; i < $eq.collections.count(items); i = i + 1 | 0) if (!$eq.collections.item(items, i).disabled) return i;
        return 0;
    }

    static step(items: MenuItem[], highlight: number, direction: number) {
        for (let i = highlight + direction | 0; i >= 0 && i < $eq.collections.count(items); i = i + direction | 0) if (!$eq.collections.item(items, i).disabled) return i;
        return highlight;
    }
}

