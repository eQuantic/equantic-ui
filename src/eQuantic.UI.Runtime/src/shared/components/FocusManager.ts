import { $eq, $TraversalEdgeBehaviorValue, FocusAttachment, FocusEvent, FocusMove, FocusNode, FocusRequest, FocusScopeNode, TraversalStop } from "../runtime-exports";

export class FocusManager {
    constructor(leavesViewAtRoot: boolean) {
        this._leavesViewAtRoot = false;
        this._none = [];
        this._stops = [];
        this._modal = null;
        this._modalPosition = null;
        this._nodes = [];
        this._spare = [];
        this._frame = 0;
        this._primary = null;
        this._chain = null!;
        this._primaryNode = null;
        this._request = null;
        this.requestAsked = null;
        this._leavesViewAtRoot = leavesViewAtRoot;
        this._chain = this._none;
    }

    _leavesViewAtRoot!: boolean;
    _none!: FocusAttachment[];
    _stops!: TraversalStop[];
    _modal!: FocusScopeNode | null;
    _modalPosition!: any;
    _nodes!: FocusNode[];
    _spare!: FocusNode[];
    _frame!: number;
    _primary!: any;
    _chain!: FocusAttachment[];
    _primaryNode!: FocusNode | null;
    _request!: FocusRequest | null;
    requestAsked!: (() => void) | null;

    get primaryFocus(): FocusNode | null {
        return this._primaryNode;
    }

    get primaryPosition(): any {
        return this._primary;
    }

    attach(stops: TraversalStop[], modal: FocusScopeNode | null = null, modalPosition: any = null) {
        this._stops = stops;
        this._modal = modal;
        this._modalPosition = modalPosition;
        this._frame = this._frame + 1 | 0;
        let shown = this._spare;
        $eq.collections.listClear(shown);
        let wanting: FocusNode[] | null = null;
        for (const stop of $eq.collections.sequenceWalk(stops)) {
            let parent: FocusNode | null = null;
            let scope: FocusScopeNode | null = null;
            for (const attachment of $eq.collections.sequenceWalk(stop.chain)) {
                let node = attachment.node;
                if (node.frame !== this._frame) {
                    let first = !(node.manager === this);
                    this.show(node, parent, scope, shown);
                    node.onFocusChange = attachment.onFocusChange;
                    if (first && attachment.autofocus) {
                        wanting ?? (wanting = []);
                        $eq.collections.listAdd(wanting, node);
                    }
                }
                parent = node;
                let own: any; 
                if ((node instanceof FocusScopeNode && (own = node, true))) scope = own;
            }
        }
        if (modal != null && modal.frame !== this._frame) this.show(modal, null, null, shown);
        for (const node of $eq.collections.listWalk(this._nodes)) {
            if (node.frame === this._frame) continue;
            node.manager = null;
            node.parent = null;
            node.enclosingScope = null;
            $eq.collections.listClear(node.children);
        }
        this._spare = this._nodes;
        this._nodes = shown;
        for (const node of $eq.collections.listWalk(shown)) { let scope: any; let child: any; if ((node instanceof FocusScopeNode && (scope = node, true)) && (child = scope.focusedChild) != null && !(child.manager === this)) scope.focusedChild = null; }
        if (this._primary != null && this.indexOfStop(this._primary) < 0 && !this.isModalPosition(this._primary)) this._primary = null;
        this.settle(this.chainOf(this._primary));
        let pending: FocusRequest | null = null;
        for (const node of $eq.collections.listWalk(shown)) {
            if (node.pendingAsk <= 0) continue;
            if (pending == null || node.pendingAsk > pending.ticket) pending = new FocusRequest('focus', node, node.pendingAsk);
            node.pendingAsk = 0;
        }
        if (pending != null && (this._request == null || pending.ticket > this._request.ticket)) this._request = pending;
        if (this._request != null || wanting == null) return;
        for (const node of $eq.collections.listWalk(wanting)) if (!this.focusIsInside(node.enclosingScope)) this._request = new FocusRequest('focus', node, 0);
    }

    focused(position: any) {
        this._primary = position;
        this.settle(this.chainOf(position));
    }

    traverse(forward: boolean) {
        let at = this.indexOfStop(this._primary);
        let scope: FocusScopeNode | null = at >= 0 ? FocusManager.innermostScope($eq.collections.item(this._stops, at).chain) : null;
        let entering = at < 0;
        if (this._modal != null && (at < 0 || !this.under(at, this._modal))) {
            scope = this._modal;
            entering = true;
        }
        if (entering) {
            let enter = forward ? this.nextTraversable(scope, -1) : this.previousTraversable(scope, $eq.collections.count(this._stops));
            if (enter >= 0) return new FocusMove($eq.collections.item(this._stops, enter).position, false);
            return new FocusMove(scope != null && (scope === this._modal) ? this._modalPosition : null, false);
        }
        let next = forward ? this.nextTraversable(scope, at) : this.previousTraversable(scope, at);
        if (next >= 0) return new FocusMove($eq.collections.item(this._stops, next).position, false);
        return this.pastEdge(scope, forward ? this.lastUnder(scope) : this.firstUnder(scope), forward);
    }

    takeRequest() {
        let request = this._request;
        this._request = null;
        if (request == null) return null;
        if (request.kind === 'unfocus') return request.node.hasFocus ? new FocusMove(null, false) : null;
        if (request.kind === 'focus') {
            let at = this.landing(request.node);
            if (at < 0) return null;
            let position = $eq.collections.item(this._stops, at).position;
            return (position === this._primary) ? null : new FocusMove(position, false);
        }
        let move = this.traverse(request.kind === 'next');
        if (move.leavesView) return null;
        if (move.to == null) return this._primary == null ? null : move;
        return (move.to === this._primary) ? null : move;
    }

    ask(request: FocusRequest) {
        this._request = request;
        this.requestAsked?.();
    }

    askToMove(node: FocusNode, forward: boolean) {
        let move = this.traverse(forward);
        this.ask(new FocusRequest(forward ? 'next' : 'previous', node, FocusNode.nextAsk()));
        return !move.leavesView && move.to != null && !(move.to === this._primary);
    }

    show(node: FocusNode, parent: FocusNode | null, scope: FocusScopeNode | null, shown: FocusNode[]) {
        node.frame = this._frame;
        node.manager = this;
        node.parent = parent;
        node.enclosingScope = scope;
        $eq.collections.listClear(node.children);
        (parent == null ? null : $eq.collections.listAdd(parent.children, node));
        $eq.collections.listAdd(shown, node);
    }

    focusIsInside(scope: FocusScopeNode | null) {
        return this._primary != null && (scope == null || FocusManager.contains(this._chain, scope));
    }

    settle(next: FocusAttachment[]) {
        let previous = this._chain;
        let owner = FocusManager.ownerOf(next);
        if (owner == null && $eq.collections.count(next) > 0 && this._modalPosition != null && (this._primary === this._modalPosition)) owner = this._modal;
        if (FocusManager.sameNodes(previous, next) && (owner === this._primaryNode)) {
            this._chain = next;
            return;
        }
        this._chain = next;
        this._primaryNode = owner;
        for (let i = 0; (i + 1 | 0) < $eq.collections.count(next); i = i + 1 | 0) { let scope: any; if ((($0) => ($0 instanceof FocusScopeNode && (scope = $0, true)))($eq.collections.item(next, i).node)) scope.focusedChild = $eq.collections.item(next, i + 1 | 0).node; }
        let events: FocusEvent[] = [];
        let seen: FocusNode[] = [];
        let depth = Math.max($eq.collections.count(previous), $eq.collections.count(next));
        for (let d = 0; d < depth; d = d + 1 | 0) {
            if (d < $eq.collections.count(previous)) FocusManager.observe($eq.collections.item(previous, d).node, next, owner, seen, events);
            if (d < $eq.collections.count(next)) FocusManager.observe($eq.collections.item(next, d).node, next, owner, seen, events);
        }
        for (const heard of $eq.collections.listWalk(events)) {
            heard.node.raiseChanged();
            if (heard.focusChanged) heard.node.onFocusChange?.(heard.hasFocus);
        }
    }

    static observe(node: FocusNode, next: FocusAttachment[], owner: FocusNode | null, seen: FocusNode[], events: FocusEvent[]) {
        if ($eq.collections.contains(seen, node, 'item')) return;
        $eq.collections.listAdd(seen, node);
        let hasFocus = FocusManager.contains(next, node);
        let hasPrimary = (node === owner);
        let focusChanged = node.hasFocus !== hasFocus;
        let changed = focusChanged || node.hasPrimaryFocus !== hasPrimary;
        node.hasFocus = hasFocus;
        node.hasPrimaryFocus = hasPrimary;
        if (changed) $eq.collections.listAdd(events, new FocusEvent(node, hasFocus, focusChanged));
    }

    landing(node: FocusNode) {
        if (!(node.manager === this) || !node.canRequestFocus) return -1;
        let scope: any; 
        if ((node instanceof FocusScopeNode && (scope = node, true))) {
            let child: any; 
            if ((child = scope.focusedChild) != null && !(child === scope)) {
                let inChild = this.landing(child);
                if (inChild >= 0) return inChild;
            }
            for (let i = 0; i < $eq.collections.count(this._stops); i = i + 1 | 0) if (this.inModal(i) && this.under(i, scope) && this.traversable(i)) return i;
            return -1;
        }
        for (let i = 0; i < $eq.collections.count(this._stops); i = i + 1 | 0) if (this.inModal(i) && (FocusManager.ownerOf($eq.collections.item(this._stops, i).chain) === node)) return i;
        for (let i = 0; i < $eq.collections.count(this._stops); i = i + 1 | 0) if (this.inModal(i) && FocusManager.contains($eq.collections.item(this._stops, i).chain, node) && this.requestable(i)) return i;
        return -1;
    }

    pastEdge(scope: FocusScopeNode | null, edge: number, forward: boolean) {
        if (scope == null) {
            if (this._leavesViewAtRoot) return new FocusMove(null, true);
            return this.wrapped(null, forward);
        }
        let behavior: $TraversalEdgeBehaviorValue = scope.traversalEdgeBehavior;
        if ((scope === this._modal) && behavior === 'parentScope') behavior = 'closedLoop';
        if (behavior === 'stop') return new FocusMove(this._primary, false);
        if (behavior === 'closedLoop') return this.wrapped(scope, forward);
        let outer = FocusManager.scopeOutside($eq.collections.item(this._stops, this.indexOfStop(this._primary)).chain, scope);
        let next = forward ? this.nextTraversable(outer, edge) : this.previousTraversable(outer, edge);
        if (next >= 0) return new FocusMove($eq.collections.item(this._stops, next).position, false);
        return this.pastEdge(outer, forward ? this.lastUnder(outer) : this.firstUnder(outer), forward);
    }

    wrapped(scope: FocusScopeNode | null, forward: boolean) {
        let at = forward ? this.nextTraversable(scope, -1) : this.previousTraversable(scope, $eq.collections.count(this._stops));
        return at >= 0 ? new FocusMove($eq.collections.item(this._stops, at).position, false) : new FocusMove(this._primary, false);
    }

    chainOf(position: any) {
        if (position == null) return this._none;
        let at = this.indexOfStop(position);
        if (at >= 0) return $eq.collections.item(this._stops, at).chain;
        if (this._modal != null && this.isModalPosition(position)) {
            let chain: FocusAttachment[] = [];
            $eq.collections.listAdd(chain, new FocusAttachment(this._modal, null, false));
            return chain;
        }
        return this._none;
    }

    isModalPosition(position: any) {
        return position != null && this._modalPosition != null && (position === this._modalPosition);
    }

    indexOfStop(position: any) {
        if (position == null) return -1;
        for (let i = 0; i < $eq.collections.count(this._stops); i = i + 1 | 0) if (($eq.collections.item(this._stops, i).position === position)) return i;
        return -1;
    }

    under(stop: number, scope: FocusScopeNode | null) {
        return scope == null || FocusManager.contains($eq.collections.item(this._stops, stop).chain, scope);
    }

    inModal(stop: number) {
        return this._modal == null || FocusManager.contains($eq.collections.item(this._stops, stop).chain, this._modal);
    }

    traversable(stop: number) {
        let owner = FocusManager.ownerOf($eq.collections.item(this._stops, stop).chain);
        return owner == null || owner.canRequestFocus && !owner.skipTraversal;
    }

    requestable(stop: number) {
        let owner = FocusManager.ownerOf($eq.collections.item(this._stops, stop).chain);
        return owner == null || owner.canRequestFocus;
    }

    nextTraversable(scope: FocusScopeNode | null, after: number) {
        for (let i = after + 1 | 0; i < $eq.collections.count(this._stops); i = i + 1 | 0) if (this.under(i, scope) && this.traversable(i)) return i;
        return -1;
    }

    previousTraversable(scope: FocusScopeNode | null, before: number) {
        for (let i = before - 1 | 0; i >= 0; i = i - 1 | 0) if (this.under(i, scope) && this.traversable(i)) return i;
        return -1;
    }

    lastUnder(scope: FocusScopeNode | null) {
        for (let i = $eq.collections.count(this._stops) - 1 | 0; i >= 0; i = i - 1 | 0) if (this.under(i, scope)) return i;
        return -1;
    }

    firstUnder(scope: FocusScopeNode | null) {
        for (let i = 0; i < $eq.collections.count(this._stops); i = i + 1 | 0) if (this.under(i, scope)) return i;
        return -1;
    }

    static sameNodes(a: FocusAttachment[], b: FocusAttachment[]) {
        if ((a === b)) return true;
        if ($eq.collections.count(a) !== $eq.collections.count(b)) return false;
        for (let i = 0; i < $eq.collections.count(a); i = i + 1 | 0) if (!($eq.collections.item(a, i).node === $eq.collections.item(b, i).node)) return false;
        return true;
    }

    static contains(chain: FocusAttachment[], node: FocusNode | null) {
        for (let i = 0; i < $eq.collections.count(chain); i = i + 1 | 0) if (($eq.collections.item(chain, i).node === node)) return true;
        return false;
    }

    static ownerOf(chain: FocusAttachment[]) {
        for (let i = $eq.collections.count(chain) - 1 | 0; i >= 0; i = i - 1 | 0) if (!($eq.collections.item(chain, i).node instanceof FocusScopeNode)) return $eq.collections.item(chain, i).node;
        return null;
    }

    static innermostScope(chain: FocusAttachment[]) {
        for (let i = $eq.collections.count(chain) - 1 | 0; i >= 0; i = i - 1 | 0) { let scope: any; if ((($0) => ($0 instanceof FocusScopeNode && (scope = $0, true)))($eq.collections.item(chain, i).node)) return scope; }
        return null;
    }

    static scopeOutside(chain: FocusAttachment[], scope: FocusScopeNode) {
        let at = $eq.collections.count(chain) - 1 | 0;
        while (at >= 0 && !($eq.collections.item(chain, at).node === scope)) at = at - 1 | 0;
        for (let i = at - 1 | 0; i >= 0; i = i - 1 | 0) { let outer: any; if ((($0) => ($0 instanceof FocusScopeNode && (outer = $0, true)))($eq.collections.item(chain, i).node)) return outer; }
        return null;
    }

    toString(): string {
        return 'eQuantic.UI.Primitives.FocusManager';
    }
}

