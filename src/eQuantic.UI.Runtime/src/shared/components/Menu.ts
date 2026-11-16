import { $eq, Anchored, AnchorPlacementValue, BuildContext, MenuItem, MenuPanel, Pressable, StatefulComponent, UiComponent, VisualNode } from "../runtime-exports";

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
        let panel = MenuPanel.build(context, this.items, this._open, this._highlight, $eq.delegates.bound(this, this.choose));
        let menu: VisualNode = new Anchored(new Pressable(this.trigger, $eq.delegates.bound(this, this.toggle), { expanded: this._open }), panel, { placement: this.placement, open: this._open, onDismiss: () => this.setState(() => this._open = false), panelRole: 'menu', activeIndex: this._open ? this._highlight : -1 });
        return MenuPanel.withKeys(menu, this._open && $eq.collections.count(this.items) > 0, () => this.setState(() => this._highlight = MenuPanel.step(this.items, this._highlight, +1)), () => this.setState(() => this._highlight = MenuPanel.step(this.items, this._highlight, -1)), () => this.choose(this._highlight));
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
            if (this._open) this._highlight = MenuPanel.firstEnabled(this.items);
        });
    }

    choose(index: number) {
        if (index < 0 || index >= $eq.collections.count(this.items) || $eq.collections.item(this.items, index).disabled) return;
        this.onSelect?.(index);
        this.setState(() => this._open = false);
    }
}

