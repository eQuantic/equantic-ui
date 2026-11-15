import { $eq, Anchored, BuildContext, ContextRequest, ContextTarget, MenuItem, MenuPanel, StatefulComponent, UiComponent, VisualNode } from "../runtime-exports";

export class ContextMenu extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Components.ContextMenu';
    _open: boolean = false;
    _highlight: number = 0;
    _at: any = null;
    _enabled: boolean = true;
    declare child: VisualNode;
    declare items: MenuItem[];
    declare onSelect: ((int: number) => void) | null;

    get enabled() {
        return this._enabled;
    }

    set enabled(value) {
        this._enabled = value;
    }

    constructor(child?: any, items?: any, onSelect: any = null, props?: any) {
        super();
        if (child !== undefined) this.child = child;
        if (items !== undefined) this.items = items;
        if (onSelect !== undefined) this.onSelect = onSelect;
        this.child = child;
        this.items = items;
        this.onSelect = onSelect;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let panel = MenuPanel.build(context, this.items, this._open, this._highlight, $eq.delegates.bound(this, this.choose));
        let menu: VisualNode = new Anchored(new ContextTarget(this.child, $eq.delegates.bound(this, this.request), { enabled: this._enabled && $eq.collections.count(this.items) > 0 }), panel, { at: this._at, placement: 'bottomStart', open: this._open, onDismiss: () => this.setState(() => this._open = false), panelRole: 'menu', activeIndex: this._open ? this._highlight : -1 });
        return MenuPanel.withKeys(menu, this._open && $eq.collections.count(this.items) > 0, () => this.setState(() => this._highlight = MenuPanel.step(this.items, this._highlight, +1)), () => this.setState(() => this._highlight = MenuPanel.step(this.items, this._highlight, -1)), () => this.choose(this._highlight));
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof ContextMenu && (fresh = next, true)))) return;
        this.child = fresh.child;
        this.items = fresh.items;
        this.onSelect = fresh.onSelect;
        this._enabled = fresh.enabled;
    }

    request(request: ContextRequest) {
        return this.setState(() => {
            this._at = request.at;
            this._open = true;
            this._highlight = MenuPanel.firstEnabled(this.items);
        });
    }

    choose(index: number) {
        if (index < 0 || index >= $eq.collections.count(this.items) || $eq.collections.item(this.items, index).disabled) return;
        this.onSelect?.(index);
        this.setState(() => this._open = false);
    }
}

