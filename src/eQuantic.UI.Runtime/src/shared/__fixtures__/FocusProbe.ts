import { $eq, FocusNode } from "@equantic/runtime";

export class FocusProbe {
    constructor(name: string, heard: string[], node: FocusNode) {
        this._name = null!;
        this._heard = null!;
        this._node = null!;
        this._handlers = $eq.collections.dictionary<string, () => void>();
        this.onFocusChange = null!;
        this._name = name;
        this._heard = heard;
        this._node = node;
        this.onFocusChange = (hasFocus: boolean) => $eq.collections.listAdd(this._heard, `${this._name}.onFocusChange ${(hasFocus ? 'true' : 'false')}`);
    }

    _name!: string;
    _heard!: string[];
    _node!: FocusNode;
    _handlers!: any;
    onFocusChange!: (bool: boolean) => void;

    watch() {
        return this.add(`${this._name}.changed`);
    }

    add(handler: string, removes: string | null = null, adds: string | null = null) {
        let action!: () => void; 
        if (!(($0) => ($0.has(handler) ? ((action = $0.get(handler)), true) : ((action = null!), false)))(this._handlers)) {
            let first = true;
            action = () => {
                $eq.collections.listAdd(this._heard, handler);
                if (!first) return;
                first = false;
                if (removes != null) this.remove(removes);
                if (adds != null) this.add(adds);
            };
            $eq.mapSet(this._handlers, handler, action);
        }
        this._node.changed = $eq.delegates.combine(this._node.changed, action);
    }

    remove(handler: string) {
        let action!: () => void; 
        if ((($0) => ($0.has(handler) ? ((action = $0.get(handler)), true) : ((action = null!), false)))(this._handlers)) this._node.changed = $eq.delegates.remove(this._node.changed, action);
    }

    toJSON() {
        return $eq.json(this, ['onFocusChange']);
    }

    toString(): string {
        return 'eQuantic.UI.Web.Tests.Fixtures.FocusProbe';
    }
}

