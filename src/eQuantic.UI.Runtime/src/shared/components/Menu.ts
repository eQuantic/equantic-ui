import { $eq, Anchored, AnchorPlacementValue, Box, BoxStyle, BuildContext, Column, ControlMetrics, CornerRadii, EdgeInsets, Icon, IconGlyph, KeyChord, MenuItem, Pressable, Row, Shortcut, SizeValue, StatefulComponent, StyleDiff, Text, UiComponent, VisualNode } from "../runtime-exports";

export class Menu extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Components.Menu';
    _open: boolean = false;
    _highlight: number = 0;
    _placement: AnchorPlacementValue = 'bottomStart';
    declare trigger: VisualNode;
    declare items: MenuItem[];
    declare onSelect: ((int: number) => void) | null;

    get placement() {
        return this._placement;
    }

    set placement(value) {
        this._placement = value;
    }

    constructor(trigger?: any, items?: any, onSelect: any = null, props?: any) {
        super();
        if (trigger !== undefined) this.trigger = trigger;
        if (items !== undefined) this.items = items;
        if (onSelect !== undefined) this.onSelect = onSelect;
        this.trigger = trigger;
        this.items = items;
        this.onSelect = onSelect;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let slot = theme.menu;
        let list = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        for (let i = 0; i < $eq.collections.count(this.items); i++) {
            let item = $eq.collections.item(this.items, i);
            let index = i;
            let row = new Row(slot?.itemGap ?? 8, 'start', 'center', false, null, null, { cross: 'center', width: SizeValue.fill, height: SizeValue.fill });
            let glyph: any; 
            if ((glyph = item.icon) != null) row.add(new Icon(IconGlyph.fromIcons(glyph), 20, item.destructive ? theme.colors('destructive').base : theme.textSecondary));
            row.add(new Text(item.label, 'bodyM', item.destructive ? theme.colors('destructive').base : theme.textPrimary, 1));
            let surface = new Box(new BoxStyle({ height: ControlMetrics.height(context, 'medium'), padding: slot?.itemPadding ?? EdgeInsets.symmetric(12, 0), width: SizeValue.fill, opacity: item.disabled ? theme.disabledOpacity : null, background: this._open && index === this._highlight && !item.disabled ? theme.surfaceSubtle : null, hover: item.disabled ? null : new StyleDiff({ background: theme.surfaceSubtle }) }), row);
            list.add(new Pressable(surface, item.disabled ? null : () => this.choose(index), { disabled: item.disabled, role: 'menuItem' }));
        }
        let panel = new Box(new BoxStyle({ minWidth: slot?.menuMinWidth ?? 180, background: theme.surface, cornerRadius: new CornerRadii(theme.shape(slot?.panelShape ?? 'medium')), borderWidth: slot?.panelBorderWidth ?? 1, borderColor: theme.border, elevation: slot?.panelElevation ?? 2, padding: slot?.menuPadding ?? EdgeInsets.symmetric(0, 4), clip: true }), list);
        let menu: VisualNode = new Anchored(new Pressable(this.trigger, this.toggle.bind(this), { expanded: this._open }), panel, { placement: this.placement, open: this._open, onDismiss: () => this.setState(() => this._open = false), panelRole: 'menu', activeIndex: this._open ? this._highlight : -1 });
        let live = this._open && $eq.collections.count(this.items) > 0;
        menu = new Shortcut(menu, KeyChord.arrowDown, () => this.setState(() => this._highlight = this.step(+1)), { enabled: live });
        menu = new Shortcut(menu, KeyChord.arrowUp, () => this.setState(() => this._highlight = this.step(-1)), { enabled: live });
        menu = new Shortcut(menu, KeyChord.enter, () => this.choose(this._highlight), { enabled: live });
        return menu;
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof Menu && (fresh = next, true)))) return;
        this.trigger = fresh.trigger;
        this.items = fresh.items;
        this.onSelect = fresh.onSelect;
        this._placement = fresh.placement;
    }

    toggle() {
        return this.setState(() => {
            this._open = !this._open;
            if (this._open) this._highlight = this.firstEnabled();
        });
    }

    choose(index: number) {
        if (index < 0 || index >= $eq.collections.count(this.items) || $eq.collections.item(this.items, index).disabled) return;
        this.onSelect?.(index);
        this.setState(() => this._open = false);
    }

    firstEnabled() {
        for (let i = 0; i < $eq.collections.count(this.items); i++) if (!$eq.collections.item(this.items, i).disabled) return i;
        return 0;
    }

    step(direction: number) {
        for (let i = this._highlight + direction; i >= 0 && i < $eq.collections.count(this.items); i += direction) if (!$eq.collections.item(this.items, i).disabled) return i;
        return this._highlight;
    }
}

