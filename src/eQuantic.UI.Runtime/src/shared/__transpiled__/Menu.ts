import { Anchored, Box, BoxStyle, BuildContext, Column, CornerRadii, EdgeInsets, Icon, MenuItem, Pressable, Row, SharedStatefulComponent, SizeValue, Sizing, StyleDiff, Text, UiComponent, VisualNode } from "@equantic/runtime";

export class Menu extends SharedStatefulComponent {
    _open: boolean = false;
    declare trigger: VisualNode;
    declare items: MenuItem[];
    declare onSelect: any;
    declare placement: string;
    constructor(trigger?: any, items?: any, onSelect: any = null, props?: any) {
        super(props);
        if (trigger !== undefined) this.trigger = trigger;
        if (items !== undefined) this.items = items;
        if (onSelect !== undefined) this.onSelect = onSelect;
        if (this.placement === undefined) this.placement = 'bottomStart';
        this.trigger = trigger;this.items = items;this.onSelect = onSelect;
    }

    build(context: BuildContext) {
        let theme = context.theme;let list = new Column(0, { width: SizeValue.fill });for (let i = 0; i < this.items.length; i++) {let item = this.items[i];let index = i;let row = new Row(8, { cross: 'center', width: SizeValue.fill, height: SizeValue.fill });let glyph; if (((item.icon != null) && (glyph = item.icon, true))) row.add(new Icon(glyph, 20, item.destructive ? theme.colors('destructive').base : theme.textSecondary));row.add(new Text(item.label, 'bodyM', item.destructive ? theme.colors('destructive').base : theme.textPrimary));let surface = new Box(new BoxStyle({ height: Sizing.height('medium'), padding: EdgeInsets.symmetric(12, 0), width: SizeValue.fill, opacity: item.disabled ? theme.disabledOpacity : null, hover: item.disabled ? null : new StyleDiff({ background: theme.surfaceSubtle }) }), row);list.add(item.disabled ? surface : new Pressable(surface, () => {this.onSelect?.(index);this.setState(() => this._open = false);}));}let panel = new Box(new BoxStyle({ minWidth: 180, background: theme.surface, cornerRadius: new CornerRadii(theme.shape('medium')), borderWidth: 1, borderColor: theme.border, elevation: 2, padding: EdgeInsets.symmetric(0, 4), clip: true }), list);return new Anchored(new Pressable(this.trigger, () => this.setState(() => this._open = !this._open)), panel, { placement: this.placement, open: this._open, onDismiss: () => this.setState(() => this._open = false) });
    }

    adoptConfig(next: UiComponent) {
        let fresh; if (!((next instanceof Menu && (fresh = next, true)))) return;this.trigger = fresh.trigger;this.items = fresh.items;this.onSelect = fresh.onSelect;
    }

}

