import { $eq, AdaptiveNode, ChildSlots, NodeSlots, VisualNode } from "../runtime-exports";
import { Element } from "./Element";

export class NodeElement extends Element {
    constructor(configuration: VisualNode) {
        const $$_children: (Element | null)[] = [];
        const $$_deferred: number = -1;
        const $$_shownInFrame: number = -1;
        super(configuration);
        this._children = $$_children;
        this._spare = null;
        this._deferred = $$_deferred;
        this._shownInFrame = $$_shownInFrame;
        this.state = null;
        this._partIds = null;
    }

    _children!: (Element | null)[];
    _spare!: (Element | null)[] | null;
    _deferred!: number;
    _shownInFrame!: number;
    state!: any;
    _partIds!: number[] | null;

    childAt(slot: number) {
        return slot >>> 0 < this._children.length >>> 0 ? this._children[slot] : null;
    }

    visitChildren(visitor: (element: Element) => void) {
        for (let slot = 0; slot < this._children.length; slot = slot + 1 | 0) {
            let child = this._children[slot];
            if (!(child == null)) visitor(child);
        }
    }

    partId(part: number) {
        $eq.guard.throwIfNegative(part, 'part', ($value) => $eq.text.format($value, null, undefined, undefined, 'int32'));
        let ids = this._partIds ?? (this._partIds = []);
        while (ids.length <= part) $eq.collections.listAdd(ids, this.owner.nextId());
        return ids[part];
    }

    showDeferred(configuration: VisualNode) {
        if (this._deferred < 0) throw $eq.exceptions.create(['System.InvalidOperationException', 'System.SystemException', 'System.Exception'], `A '${this.configuration.nodeKind}' node has no slot the realizer shows.`);
        this.owner.enterLayer(this);
        try {
            let shown = this.updateChild(this.childAt(this._deferred), configuration, this._deferred, this.owner.rebuildingAll)!;
            $eq.collections.listSet(this._children, this._deferred, shown);
            this._shownInFrame = this.owner.frame;
            this.owner.buildDeferred(this);
            return shown;
        } finally {
            this.owner.exitLayer();
        }
    }

    firstBuild() {
        this.updateChildren(false);
        if (this._deferred >= 0) this.owner.trackDeferred(this);
    }

    update(configuration: VisualNode, force: boolean) {
        if ((configuration === this.configuration) && !force) return;
        this.configuration = configuration;
        this.updateChildren(force);
    }

    holdsDeferred(child: Element) {
        return this._deferred >= 0 && (this.childAt(this._deferred) === child);
    }

    sweepDeferred(frame: number) {
        let hidden: any; 
        if (this._deferred < 0 || this._shownInFrame === frame || !((hidden = this.childAt(this._deferred)) != null)) return;
        this.owner.deactivate(hidden);
        $eq.collections.listSet(this._children, this._deferred, null);
    }

    updateChildren(force: boolean) {
        let slots = ChildSlots.of(this.configuration, this.owner.windowWidth, this.owner.holdsEveryArm);
        this._deferred = slots.deferred;
        if (slots.isList && this.anyKey(slots)) {
            this.updateKeyed(slots, force);
            return;
        }
        let count = slots.count;
        while (this._children.length < count) $eq.collections.listAdd(this._children, null);
        if (!this.owner.holdsEveryArm && this.configuration instanceof AdaptiveNode) this.carryTheArmAcross(slots);
        for (let slot = 0; slot < count; slot = slot + 1 | 0) {
            if (slot === this._deferred) continue;
            $eq.collections.listSet(this._children, slot, this.updateChild(this._children[slot], slots.item(slot), slot, force));
        }
        for (let slot = this._children.length - 1 | 0; slot >= count; slot = slot - 1 | 0) {
            let gone: any; 
            if ((gone = this._children[slot]) != null) this.owner.deactivate(gone);
            $eq.collections.listRemoveAt(this._children, slot);
        }
    }

    carryTheArmAcross(slots: NodeSlots) {
        let shown = -1;
        for (let slot = 0; slot < slots.count; slot = slot + 1 | 0) if (!(slots.item(slot) == null)) shown = slot;
        if (shown < 0 || !(this._children[shown] == null)) return;
        let from = -1;
        for (let slot = 0; slot < this._children.length; slot = slot + 1 | 0) {
            if (this._children[slot] == null) continue;
            if (from >= 0) return;
            from = slot;
        }
        if (from >= 0 && Element.canUpdate((this._children[from]!).configuration, slots.item(shown)!)) {
            $eq.collections.listSet(this._children, shown, this._children[from]);
            $eq.collections.listSet(this._children, from, null);
        }
    }

    anyKey(slots: NodeSlots) {
        for (let i = 0; i < slots.count; i = i + 1 | 0) if ((($0) => ($0 != null && $0.length > 0))((slots.item(i)?.key ?? null))) return true;
        for (let i = 0; i < this._children.length; i = i + 1 | 0) if ((($0) => ($0 != null && $0.length > 0))((this._children[i]?.configuration.key ?? null))) return true;
        return false;
    }

    updateKeyed(slots: NodeSlots, force: boolean) {
        let stamp = this.owner.nextStamp();
        let byKey = this.owner.keyScratch;
        byKey.clear();
        let repeated = false;
        for (const old of $eq.collections.listWalk(this._children)) {
            let key: any; 
            if ((($0) => (($0 != null && $0.length > 0) && (key = $0, true)))((old?.configuration.key ?? null)) && !byKey.tryAdd(key, old)) repeated = true;
        }
        let next = this._spare ?? (this._spare = []);
        $eq.collections.listClear(next);
        let count = slots.count;
        for (let i = 0; i < count; i = i + 1 | 0) {
            let node = slots.item(i)!;
            let claimed: Element | null = null;
            let key: any; 
            if ((($0) => (($0 != null && $0.length > 0) && (key = $0, true)))(node.key)) {
                let keyed: any; 
                if ((byKey.has(key) ? ((keyed = byKey.get(key)), true) : ((keyed = null), false))) {
                    claimed = keyed.matchStamp !== stamp && Element.canUpdate(keyed.configuration, node) ? keyed : repeated ? this.nextUnclaimed(node, stamp) : null;
                }
            } else { let indexed: any; if (i < this._children.length && (indexed = this._children[i]) != null && (($0) => !(($0 != null && $0.length > 0)))(indexed.configuration.key) && indexed.matchStamp !== stamp && Element.canUpdate(indexed.configuration, node)) {
                claimed = indexed;
            } }
            if (!(claimed == null)) claimed.matchStamp = stamp;
            $eq.collections.listAdd(next, claimed);
        }
        byKey.clear();
        for (const old of $eq.collections.listWalk(this._children)) {
            if (!(old == null) && old.matchStamp !== stamp) this.owner.deactivate(old);
        }
        [this._children, this._spare] = [next, this._children];
        $eq.collections.listClear(this._spare);
        for (let i = 0; i < count; i = i + 1 | 0) {
            let node = slots.item(i)!;
            let kept: any; 
            if ((kept = this._children[i]) != null) {
                kept.slot = i;
                kept.update(node, force);
            } else $eq.collections.listSet(this._children, i, this.inflate(node, i));
        }
    }

    nextUnclaimed(node: VisualNode, stamp: number) {
        for (const old of $eq.collections.listWalk(this._children)) {
            if (!(old == null) && old.matchStamp !== stamp && Element.canUpdate(old.configuration, node)) return old;
        }
        return null;
    }

    onDeactivated() {
        let state = this.state;
        this.state = null;
        ((($as: any) => $eq.types.implements($as, 'System.IDisposable') ? $as : null)(state))?.dispose();
    }

    forgetChild(child: Element) {
        for (let slot = 0; slot < this._children.length; slot = slot + 1 | 0) {
            if ((this._children[slot] === child)) $eq.collections.listSet(this._children, slot, null);
        }
    }

    fillForgotten() {
        let slots = ChildSlots.of(this.configuration, this.owner.windowWidth, this.owner.holdsEveryArm);
        for (let slot = 0; slot < slots.count && slot < this._children.length; slot = slot + 1 | 0) {
            let node: any; 
            if (slot !== this._deferred && this._children[slot] == null && (node = slots.item(slot)) != null) $eq.collections.listSet(this._children, slot, this.inflate(node, slot));
        }
    }

    deactivateChildren() {
        for (let i = 0; i < this._children.length; i = i + 1 | 0) {
            this._children[i]?.deactivate();
            $eq.collections.listSet(this._children, i, null);
        }
    }

    toString(): string {
        return 'eQuantic.UI.Primitives.NodeElement';
    }
}

