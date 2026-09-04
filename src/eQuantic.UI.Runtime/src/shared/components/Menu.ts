import { Anchored, AnchorPlacementValue, Box, BoxStyle, BuildContext, Column, CornerRadii, EdgeInsets, Icon, KeyChord, MenuItem, Pressable, Row, Shortcut, SizeValue, Sizing, StatefulComponent, StyleDiff, Text, UiComponent, VisualNode } from "../runtime-exports";

export class Menu extends StatefulComponent {
    _open: boolean = false;
    _highlight: number = 0;
    declare trigger: VisualNode;
    declare items: MenuItem[];
    declare onSelect: any;
    declare placement: AnchorPlacementValue;

    constructor(trigger?: any, items?: any, onSelect: any = null, props?: any) {
        super();
        if (trigger !== undefined) this.trigger = trigger;
        if (items !== undefined) this.items = items;
        if (onSelect !== undefined) this.onSelect = onSelect;
        if (this.placement === undefined) this.placement = 'bottomStart';
        this.trigger = trigger;
        this.items = items;
        this.onSelect = onSelect;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let list = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        for (let i = 0; i < this.items.length; i++) {
            let item = this.items[i];
            let index = i;
            let row = new Row(8, 'start', 'center', false, null, null, { cross: 'center', width: SizeValue.fill, height: SizeValue.fill });
            let glyph: any; 
            if ((glyph = item.icon) != null) row.add(new Icon(glyph, 20, item.destructive ? theme.colors('destructive').base : theme.textSecondary));
            row.add(new Text(item.label, 'bodyM', item.destructive ? theme.colors('destructive').base : theme.textPrimary, 1));
            let surface = new Box(new BoxStyle({ height: Sizing.height('medium', context.density), padding: EdgeInsets.symmetric(12, 0), width: SizeValue.fill, opacity: item.disabled ? theme.disabledOpacity : null, background: this._open && index === this._highlight && !item.disabled ? theme.surfaceSubtle : null, hover: item.disabled ? null : new StyleDiff({ background: theme.surfaceSubtle }) }), row);
            list.add(new Pressable(surface, item.disabled ? null : () => this.choose(index), { disabled: item.disabled, role: 'menuItem' }));
        }
        let panel = new Box(new BoxStyle({ minWidth: 180, background: theme.surface, cornerRadius: new CornerRadii(theme.shape('medium')), borderWidth: 1, borderColor: theme.border, elevation: 2, padding: EdgeInsets.symmetric(0, 4), clip: true }), list);
        let menu: VisualNode = new Anchored(new Pressable(this.trigger, this.toggle.bind(this), { expanded: this._open }), panel, { placement: this.placement, open: this._open, onDismiss: () => this.setState(() => this._open = false), panelRole: 'menu', activeIndex: this._open ? this._highlight : -1 });
        if (this._open && this.items.length > 0) {
            menu = new Shortcut(menu, KeyChord.arrowDown, () => this.setState(() => this._highlight = this.step(+1)));
            menu = new Shortcut(menu, KeyChord.arrowUp, () => this.setState(() => this._highlight = this.step(-1)));
            menu = new Shortcut(menu, KeyChord.enter, () => this.choose(this._highlight));
        }
        return menu;
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof Menu && (fresh = next, true)))) return;
        this.trigger = fresh.trigger;
        this.items = fresh.items;
        this.onSelect = fresh.onSelect;
    }

    toggle() {
        return this.setState(() => {
            this._open = !this._open;
            if (this._open) this._highlight = this.firstEnabled();
        });
    }

    choose(index: number) {
        if (index < 0 || index >= this.items.length || this.items[index].disabled) return;
        this.onSelect?.(index);
        this.setState(() => this._open = false);
    }

    firstEnabled() {
        for (let i = 0; i < this.items.length; i++) if (!this.items[i].disabled) return i;
        return 0;
    }

    step(direction: number) {
        for (let i = this._highlight + direction; i >= 0 && i < this.items.length; i += direction) if (!this.items[i].disabled) return i;
        return this._highlight;
    }
}

