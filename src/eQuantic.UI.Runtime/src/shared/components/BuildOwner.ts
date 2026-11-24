import { $eq, BuildContext, ComponentElement, DevelopmentNotices, Element, ElementIds, GlobalKey, NodeElement, StatefulComponent, UiComponent, VisualNode } from "../runtime-exports";

export class BuildOwner {
    constructor(buildComponent: (arg1: UiComponent, arg2: BuildContext, arg3: number) => VisualNode = undefined!, holdsEveryArm: boolean = undefined!, ids: ElementIds | null = undefined!, live: boolean = undefined!) {
        if (arguments.length >= 1 && arguments.length <= 3) {
            let $c0: any, $c1: any, $c2: any, $c3: any;
            {
                let buildComponent: any = arguments[0], holdsEveryArm: any = arguments[1] === undefined ? false : arguments[1], ids: any = arguments[2] === undefined ? null : arguments[2];
                $c0 = buildComponent;
                $c1 = holdsEveryArm;
                $c2 = ids;
                $c3 = true;
            }
            buildComponent = $c0;
            holdsEveryArm = $c1;
            ids = $c2;
            live = $c3;
        }
        this._dirty = [];
        this._dirtyNextFrame = [];
        this._dirtyDeferred = [];
        this._deferredScratch = [];
        this._mounting = [];
        this._unmounting = [];
        this._deferred = [];
        this._frameReaders = [];
        this._routeReaders = [];
        this._context = null;
        this._building = 0;
        this._inFrameBuild = false;
        this._dirtyCursor = -1;
        this._showing = null;
        this._deferredCursor = -1;
        this._drewOnly = null;
        this._stamp = 0;
        this._ids = null!;
        this._buildComponent = null!;
        this.building = null;
        this.buildRequested = null;
        this.root = null;
        this.frame = 0;
        this.rebuildingAll = false;
        this.marks = 0;
        this.live = false;
        this.holdsEveryArm = false;
        this.windowWidth = 0;
        this.navigationRequested = null;
        this.keepsInstance = null;
        this.keyScratch = $eq.collections.dictionary();
        this._held = $eq.collections.dictionary(null, 'item');
        this._parked = $eq.collections.dictionary(null, 'item');
        this._placed = $eq.collections.hashSet('item');
        this._forgetful = [];
        this._settlingParked = false;
        $eq.guard.throwIfNull(buildComponent, 'buildComponent');
        this._buildComponent = buildComponent;
        this._ids = ids ?? new ElementIds();
        this.live = live;
        this.holdsEveryArm = holdsEveryArm;
    }

    _dirty!: ComponentElement[];
    _dirtyNextFrame!: ComponentElement[];
    _dirtyDeferred!: ComponentElement[];
    _deferredScratch!: ComponentElement[];
    _mounting!: ComponentElement[];
    _unmounting!: StatefulComponent[];
    _deferred!: NodeElement[];
    _frameReaders!: ComponentElement[];
    _routeReaders!: ComponentElement[];
    _context!: BuildContext | null;
    _building!: number;
    _inFrameBuild!: boolean;
    _dirtyCursor!: number;
    _showing!: NodeElement | null;
    _deferredCursor!: number;
    _drewOnly!: Element[] | null;
    _stamp!: number;
    _ids!: ElementIds;
    _buildComponent!: (arg1: UiComponent, arg2: BuildContext, arg3: number) => VisualNode;
    building!: ComponentElement | null;
    buildRequested!: (() => void) | null;
    root!: Element | null;
    frame!: number;
    rebuildingAll!: boolean;
    marks!: number;
    live!: boolean;
    holdsEveryArm!: boolean;
    windowWidth!: number;
    navigationRequested!: ((string: string) => void) | null;
    keepsInstance!: ((value: StatefulComponent) => boolean) | null;
    keyScratch!: any;
    _held!: any;
    _parked!: any;
    _placed!: Set<Element>;
    _forgetful!: Element[];
    _settlingParked!: boolean;

    get hasDirty(): boolean {
        return this._dirty.length > 0 || this._dirtyNextFrame.length > 0;
    }

    get context(): BuildContext {
        return this._context ?? $eq.exceptions.raise($eq.exceptions.create(['System.InvalidOperationException', 'System.SystemException', 'System.Exception'], 'A component built before its owner\'s frame began.'));
    }

    get waitsForALayer(): boolean {
        for (const element of $eq.collections.listWalk(this._dirtyDeferred)) {
            if (element.isActive && element.dirty) return true;
        }
        return false;
    }

    static $slots: any = null;
    static $failure: any = null;

    static $init(): any {
        if (BuildOwner.$slots === null) {
            if (BuildOwner.$failure !== null) throw BuildOwner.$failure;
            let $slots: any = BuildOwner.$slots = {};
            try {
                $slots.byDepth = null;
                $slots.byDepth = ((a, b) => Math.sign(a.depth - b.depth)) satisfies (x: any, y: any) => number;
            } catch ($error) {
                BuildOwner.$slots = null;
                throw BuildOwner.$failure = $eq.exceptions.typeInitialization('eQuantic.UI.Primitives.BuildOwner', $error);
            }
        }
        return BuildOwner.$slots;
    }

    static get byDepth(): (x: any, y: any) => number {
        return BuildOwner.$init().byDepth;
    }

    static set byDepth(value: (x: any, y: any) => number) {
        BuildOwner.$init().byDepth = value;
    }

    static structural(buildComponent: (arg1: UiComponent, arg2: BuildContext, arg3: number) => VisualNode, holdsEveryArm: boolean = false, ids: ElementIds | null = null) {
        return new BuildOwner(buildComponent, holdsEveryArm, ids, false);
    }

    buildComponent(position: ComponentElement) {
        let outer = this.building;
        this.building = position;
        try {
            return this._buildComponent(position.component, position.context, position.componentDepth);
        } finally {
            this.building = outer;
        }
    }

    countMark() {
        return (($old) => (this.marks = $old + 1 | 0, $old))(this.marks);
    }

    hold(key: GlobalKey, element: Element) {
        let holders: any; 
        if (!(($0) => ($0.has(key) ? ((holders = $0.get(key)), true) : ((holders = null!), false)))(this._held)) {
            holders = [];
            $eq.mapSet(this._held, key, holders);
        }
        $eq.collections.listAdd(holders, element);
        if (this._inFrameBuild) $eq.collections.setAdd(this._placed, element);
    }

    letGo(key: GlobalKey, element: Element) {
        let holders: any; 
        if (!(($0) => ($0.has(key) ? ((holders = $0.get(key)), true) : ((holders = null!), false)))(this._held)) return;
        $eq.collections.remove(holders, element, 'item');
        if (holders.length === 0) this._held.delete(key);
    }

    park(key: GlobalKey, element: Element) {
        if (!this._inFrameBuild || this._settlingParked || this._parked.has(key)) return false;
        $eq.mapSet(this._parked, key, element);
        return true;
    }

    retake(key: GlobalKey, configuration: VisualNode, parent: Element) {
        if (!this._inFrameBuild) return null;
        let parked!: Element; 
        if ((($0) => ($0.has(key) ? ((parked = $0.get(key)), true) : ((parked = null!), false)))(this._parked)) {
            this._parked.delete(key);
            if (!Element.canUpdate(parked.configuration, configuration)) {
                parked.leave();
                return null;
            }
            $eq.collections.setAdd(this._placed, parked);
            return parked;
        }
        let holders: any; 
        if (this.holdsEveryArm || !(($0) => ($0.has(key) ? ((holders = $0.get(key)), true) : ((holders = null!), false)))(this._held) || holders.length !== 1) return null;
        let held = holders[0];
        let from: any; 
        if (!held.isActive || !((from = held.parent) != null) || !Element.canUpdate(held.configuration, configuration) || parent.isAtOrBelow(held)) return null;
        if (this._placed.has(held)) {
            DevelopmentNotices.say(`${key} is placed at two positions in one frame: one of them starts again, with none of the ` + 'other\'s state. A global key names one position at a time.');
            return null;
        }
        from.forgetChild(held);
        $eq.collections.listAdd(this._forgetful, from);
        $eq.collections.setAdd(this._placed, held);
        return held;
    }

    settleGlobalKeys() {
        if (this._parked.size > 0) {
            this._settlingParked = true;
            try {
                let leaving: Element[] = $eq.collections.listCopy(this._parked.values());
                this._parked.clear();
                for (const element of $eq.collections.listWalk(leaving)) element.leave();
            } finally {
                this._settlingParked = false;
            }
        }
        for (let i = 0; i < this._forgetful.length; i = i + 1 | 0) {
            if (this._forgetful[i].isActive) this._forgetful[i].fillForgotten();
        }
        $eq.collections.listClear(this._forgetful);
        this._placed.clear();
    }

    build(root: VisualNode, context: BuildContext, windowWidth: number, rebuildAll: boolean) {
        let previous = this._context;
        this._context = context;
        this.windowWidth = windowWidth;
        this.frame = this.frame + 1 | 0;
        this._drewOnly = null;
        this.rebuildingAll = rebuildAll;
        this.enterBuild();
        this._inFrameBuild = true;
        try {
            if (!(previous == null) && BuildOwner.frameValuesDiffer(previous, context)) this.markFrameReaders();
            if (!(previous == null) && !(previous.route === context.route)) this.markRouteReaders();
            if (this.root == null || !Element.canUpdate(this.root.configuration, root)) {
                if (!(this.root == null)) this.deactivate(this.root);
                this.root = Element.create(root);
                this.root.mount(this, null, 0);
            } else if (rebuildAll || !(this.root.configuration === root)) {
                this.root.update(root, rebuildAll);
            }
            this.buildDirty();
        } finally {
            try {
                this.settleGlobalKeys();
            } finally {
                this._inFrameBuild = false;
                this.exitBuild();
            }
        }
        return this.root;
    }

    static frameValuesDiffer(before: BuildContext, now: BuildContext) {
        return !(before.theme === now.theme) || before.density !== now.density || before.typeScale !== now.typeScale;
    }

    readFrame(reader: ComponentElement) {
        if (reader.frameReaderIndex >= 0) return;
        reader.frameReaderIndex = this._frameReaders.length;
        $eq.collections.listAdd(this._frameReaders, reader);
    }

    forgetFrameReader(reader: ComponentElement) {
        let index = reader.frameReaderIndex;
        if (index < 0) return;
        let last = this._frameReaders[this._frameReaders.length - 1 | 0];
        $eq.collections.listSet(this._frameReaders, index, last);
        last.frameReaderIndex = index;
        $eq.collections.listRemoveAt(this._frameReaders, this._frameReaders.length - 1 | 0);
        reader.frameReaderIndex = -1;
    }

    markFrameReaders() {
        for (const reader of $eq.collections.listWalk(this._frameReaders)) reader.dependencyChanged();
    }

    readRoute(reader: ComponentElement) {
        if (reader.routeReaderIndex >= 0) return;
        reader.routeReaderIndex = this._routeReaders.length;
        $eq.collections.listAdd(this._routeReaders, reader);
    }

    forgetRouteReader(reader: ComponentElement) {
        let index = reader.routeReaderIndex;
        if (index < 0) return;
        let last = this._routeReaders[this._routeReaders.length - 1 | 0];
        $eq.collections.listSet(this._routeReaders, index, last);
        last.routeReaderIndex = index;
        $eq.collections.listRemoveAt(this._routeReaders, this._routeReaders.length - 1 | 0);
        reader.routeReaderIndex = -1;
    }

    markRouteReaders() {
        for (const reader of $eq.collections.listWalk(this._routeReaders)) reader.dependencyChanged();
    }

    drewOnly(parts: Element[]) {
        return this._drewOnly = parts;
    }

    endFrame() {
        let parts = this._drewOnly;
        this._drewOnly = null;
        for (let i = this._deferred.length - 1 | 0; i >= 0; i = i - 1 | 0) {
            let holder = this._deferred[i];
            if (!holder.isActive) {
                $eq.collections.listRemoveAt(this._deferred, i);
                continue;
            }
            if (!(parts == null) && !BuildOwner.standsIn(holder, parts)) continue;
            holder.sweepDeferred(this.frame);
        }
        this.rebuildingAll = false;
        for (const element of $eq.collections.listWalk(this._dirtyDeferred)) {
            if (element.isActive && element.dirty) $eq.collections.listAdd(this._dirtyNextFrame, element);
        }
        $eq.collections.listClear(this._dirtyDeferred);
        this.flushLifecycle();
        if (this._dirtyNextFrame.length === 0) return;
        for (const element of $eq.collections.listWalk(this._dirtyNextFrame)) $eq.collections.listAdd(this._dirty, element);
        $eq.collections.listClear(this._dirtyNextFrame);
        this.buildRequested?.();
    }

    static standsIn(element: Element, parts: Element[]) {
        for (let at: Element | null = element; !(at == null); at = at.parent) {
            for (const part of $eq.collections.sequenceWalk(parts)) {
                if ((at === part)) return true;
            }
        }
        return false;
    }

    unmountAll() {
        this._drewOnly = null;
        let root: any; 
        if ((root = this.root) != null) {
            this.deactivate(root);
            this.root = null;
        }
        $eq.collections.listClear(this._deferred);
        $eq.collections.listClear(this._dirty);
        $eq.collections.listClear(this._dirtyNextFrame);
        $eq.collections.listClear(this._dirtyDeferred);
        $eq.collections.listClear(this._mounting);
        this.flushLifecycle();
    }

    enterBuild() {
        return (($old) => (this._building = $old + 1 | 0, $old))(this._building);
    }

    exitBuild() {
        return (($old) => (this._building = $old - 1 | 0, $old))(this._building);
    }

    enterLayer(holder: NodeElement) {
        this.enterBuild();
        this._showing = holder;
    }

    exitLayer() {
        this._showing = null;
        this.exitBuild();
    }

    nextStamp() {
        return this._stamp = this._stamp + 1 | 0;
    }

    nextId() {
        return this._ids.next();
    }

    deactivate(element: Element) {
        return element.deactivate();
    }

    queueMount(element: ComponentElement) {
        return $eq.collections.listAdd(this._mounting, element);
    }

    queueUnmount(component: StatefulComponent) {
        return $eq.collections.listAdd(this._unmounting, component);
    }

    trackDeferred(element: NodeElement) {
        return $eq.collections.listAdd(this._deferred, element);
    }

    scheduleDependent(element: ComponentElement) {
        if (!(this._showing == null)) {
            if (this._deferredCursor >= 0 && (BuildOwner.deferredHolderOf(element) === this._showing)) BuildOwner.insertBehind(this._deferredScratch, this._deferredCursor, element); else $eq.collections.listAdd(this._dirtyDeferred, element);
            return;
        }
        if (!this._inFrameBuild) {
            this.schedule(element);
            return;
        }
        if (this._dirtyCursor < 0) {
            $eq.collections.listAdd(this._dirty, element);
            return;
        }
        BuildOwner.insertBehind(this._dirty, this._dirtyCursor, element);
    }

    static insertBehind(list: ComponentElement[], cursor: number, element: ComponentElement) {
        let at = list.length;
        while (at > (cursor + 1 | 0) && list[at - 1 | 0].depth > element.depth) at = at - 1 | 0;
        $eq.collections.listInsert(list, at, element);
    }

    schedule(element: ComponentElement) {
        if (this._building > 0) {
            $eq.collections.listAdd(this._dirtyNextFrame, element);
            return;
        }
        $eq.collections.listAdd(this._dirty, element);
        this.buildRequested?.();
    }

    buildDirty() {
        if (this._dirty.length === 0) return;
        $eq.collections.listSortBy(this._dirty, BuildOwner.byDepth, 'System.Comparison`1[eQuantic.UI.Primitives.ComponentElement]');
        for (let i = 0; i < this._dirty.length; i = i + 1 | 0) {
            this._dirtyCursor = i;
            let element = this._dirty[i];
            if (!this.waitsToBuild(element)) continue;
            if (!(BuildOwner.deferredHolderOf(element) == null)) {
                $eq.collections.listAdd(this._dirtyDeferred, element);
                continue;
            }
            element.rebuild(false);
        }
        this._dirtyCursor = -1;
        $eq.collections.listClear(this._dirty);
    }

    buildDeferred(holder: NodeElement) {
        if (this._dirtyDeferred.length === 0) return;
        let mine = this._deferredScratch;
        $eq.collections.listClear(mine);
        for (let i = this._dirtyDeferred.length - 1 | 0; i >= 0; i = i - 1 | 0) {
            let element = this._dirtyDeferred[i];
            if (element.isActive && !(BuildOwner.deferredHolderOf(element) === holder)) continue;
            $eq.collections.listRemoveAt(this._dirtyDeferred, i);
            if (element.isActive) $eq.collections.listAdd(mine, element);
        }
        $eq.collections.listSortBy(mine, BuildOwner.byDepth, 'System.Comparison`1[eQuantic.UI.Primitives.ComponentElement]');
        try {
            for (let i = 0; i < mine.length; i = i + 1 | 0) {
                this._deferredCursor = i;
                if (this.waitsToBuild(mine[i])) mine[i].rebuild(false);
            }
        } finally {
            this._deferredCursor = -1;
        }
        $eq.collections.listClear(mine);
    }

    waitsToBuild(element: ComponentElement) {
        return element.isActive && element.dirty && element.builtInFrame !== this.frame;
    }

    static deferredHolderOf(element: Element) {
        for (let parent: any, child = element; (parent = child.parent) != null; child = parent) {
            let node: any; 
            if ((parent instanceof NodeElement && (node = parent, true)) && node.holdsDeferred(child)) return node;
        }
        return null;
    }

    flushLifecycle() {
        for (const component of $eq.collections.listWalk(this._unmounting)) component.notifyUnmounted();
        $eq.collections.listClear(this._unmounting);
        for (const element of $eq.collections.listWalk(this._mounting)) {
            let stateful: any; 
            if ((($0) => (($0 instanceof StatefulComponent && $0.mountedAt != null && $0.mountedAt.isActive === true) && (stateful = $0, true)))(element.component)) stateful.notifyMounted();
        }
        $eq.collections.listClear(this._mounting);
    }

    toString(): string {
        return 'eQuantic.UI.Primitives.BuildOwner';
    }
}

