import { $eq, BuildContext, ComponentElement, Element, NodeElement, StatefulComponent, UiComponent, VisualNode } from "../runtime-exports";

export class BuildOwner {
    constructor(buildComponent: (arg1: UiComponent, arg2: BuildContext, arg3: number) => VisualNode = undefined!, live: boolean = undefined!, holdsEveryArm: boolean = undefined!) {
        if (arguments.length >= 1 && arguments.length <= 2) {
            let $c0: any, $c1: any, $c2: any;
            {
                let buildComponent: any = arguments[0], holdsEveryArm: any = arguments[1] === undefined ? false : arguments[1];
                $c0 = buildComponent;
                $c1 = true;
                $c2 = holdsEveryArm;
            }
            buildComponent = $c0;
            live = $c1;
            holdsEveryArm = $c2;
        }
        this._dirty = [];
        this._dirtyNextFrame = [];
        this._dirtyDeferred = [];
        this._deferredScratch = [];
        this._mounting = [];
        this._unmounting = [];
        this._deferred = [];
        this._frameReaders = [];
        this._context = null;
        this._building = 0;
        this._inFrameBuild = false;
        this._dirtyCursor = -1;
        this._showing = null;
        this._deferredCursor = -1;
        this._stamp = 0;
        this._ids = 0;
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
        this.keyScratch = $eq.collections.dictionary();
        $eq.guard.throwIfNull(buildComponent, 'buildComponent');
        this._buildComponent = buildComponent;
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
    _context!: BuildContext | null;
    _building!: number;
    _inFrameBuild!: boolean;
    _dirtyCursor!: number;
    _showing!: NodeElement | null;
    _deferredCursor!: number;
    _stamp!: number;
    _ids!: number;
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
    keyScratch!: any;

    get hasDirty(): boolean {
        return this._dirty.length > 0 || this._dirtyNextFrame.length > 0;
    }

    get context(): BuildContext {
        return this._context ?? $eq.exceptions.raise($eq.exceptions.create(['System.InvalidOperationException', 'System.SystemException', 'System.Exception'], 'A component built before its owner\'s frame began.'));
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

    static structural(buildComponent: (arg1: UiComponent, arg2: BuildContext, arg3: number) => VisualNode, holdsEveryArm: boolean = false) {
        return new BuildOwner(buildComponent, false, holdsEveryArm);
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
        return this.marks++;
    }

    build(root: VisualNode, context: BuildContext, windowWidth: number, rebuildAll: boolean) {
        let previous = this._context;
        this._context = context;
        this.windowWidth = windowWidth;
        this.frame++;
        this.rebuildingAll = rebuildAll;
        this.enterBuild();
        this._inFrameBuild = true;
        try {
            if (!(previous == null) && BuildOwner.frameValuesDiffer(previous, context)) this.markFrameReaders();
            if (this.root == null || !Element.canUpdate(this.root.configuration, root)) {
                if (!(this.root == null)) this.deactivate(this.root);
                this.root = Element.create(root);
                this.root.mount(this, null, 0);
            } else if (rebuildAll || !(this.root.configuration === root)) {
                this.root.update(root, rebuildAll);
            }
            this.buildDirty();
        } finally {
            this._inFrameBuild = false;
            this.exitBuild();
        }
        return this.root;
    }

    static frameValuesDiffer(before: BuildContext, now: BuildContext) {
        return !(before.theme === now.theme) || before.density !== now.density || before.typeScale !== now.typeScale;
    }

    readFrame(reader: ComponentElement) {
        if (reader.frameReaderIndex >= 0) return;
        reader.frameReaderIndex = this._frameReaders.length;
        this._frameReaders.push(reader);
    }

    forgetFrameReader(reader: ComponentElement) {
        let index = reader.frameReaderIndex;
        if (index < 0) return;
        let last = this._frameReaders[this._frameReaders.length - 1];
        this._frameReaders[index] = last;
        last.frameReaderIndex = index;
        $eq.collections.listRemoveAt(this._frameReaders, this._frameReaders.length - 1);
        reader.frameReaderIndex = -1;
    }

    markFrameReaders() {
        for (const reader of this._frameReaders) reader.dependencyChanged();
    }

    endFrame() {
        for (let i = this._deferred.length - 1; i >= 0; i--) {
            let holder = this._deferred[i];
            if (!holder.isActive) {
                $eq.collections.listRemoveAt(this._deferred, i);
                continue;
            }
            holder.sweepDeferred(this.frame);
        }
        this.rebuildingAll = false;
        for (const element of this._dirtyDeferred) {
            if (element.isActive && element.dirty) this._dirtyNextFrame.push(element);
        }
        this._dirtyDeferred.splice(0);
        this.flushLifecycle();
        if (this._dirtyNextFrame.length === 0) return;
        for (const element of this._dirtyNextFrame) this._dirty.push(element);
        this._dirtyNextFrame.splice(0);
        this.buildRequested?.();
    }

    unmountAll() {
        let root: any; 
        if ((root = this.root) != null) {
            this.deactivate(root);
            this.root = null;
        }
        this._deferred.splice(0);
        this._dirty.splice(0);
        this._dirtyNextFrame.splice(0);
        this._dirtyDeferred.splice(0);
        this._mounting.splice(0);
        this.flushLifecycle();
    }

    enterBuild() {
        return this._building++;
    }

    exitBuild() {
        return this._building--;
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
        return ++this._stamp;
    }

    nextId() {
        return ++this._ids;
    }

    deactivate(element: Element) {
        return element.deactivate();
    }

    queueMount(element: ComponentElement) {
        return this._mounting.push(element);
    }

    queueUnmount(component: StatefulComponent) {
        return this._unmounting.push(component);
    }

    trackDeferred(element: NodeElement) {
        return this._deferred.push(element);
    }

    scheduleDependent(element: ComponentElement) {
        if (!(this._showing == null)) {
            if (this._deferredCursor >= 0 && (BuildOwner.deferredHolderOf(element) === this._showing)) BuildOwner.insertBehind(this._deferredScratch, this._deferredCursor, element); else this._dirtyDeferred.push(element);
            return;
        }
        if (!this._inFrameBuild) {
            this.schedule(element);
            return;
        }
        if (this._dirtyCursor < 0) {
            this._dirty.push(element);
            return;
        }
        BuildOwner.insertBehind(this._dirty, this._dirtyCursor, element);
    }

    static insertBehind(list: ComponentElement[], cursor: number, element: ComponentElement) {
        let at = list.length;
        while (at > cursor + 1 && list[at - 1].depth > element.depth) at--;
        $eq.collections.listInsert(list, at, element);
    }

    schedule(element: ComponentElement) {
        if (this._building > 0) {
            this._dirtyNextFrame.push(element);
            return;
        }
        this._dirty.push(element);
        this.buildRequested?.();
    }

    buildDirty() {
        if (this._dirty.length === 0) return;
        $eq.collections.listSortBy(this._dirty, BuildOwner.byDepth, 'System.Comparison`1[eQuantic.UI.Primitives.ComponentElement]');
        for (let i = 0; i < this._dirty.length; i++) {
            this._dirtyCursor = i;
            let element = this._dirty[i];
            if (!this.waitsToBuild(element)) continue;
            if (!(BuildOwner.deferredHolderOf(element) == null)) {
                this._dirtyDeferred.push(element);
                continue;
            }
            element.rebuild(false);
        }
        this._dirtyCursor = -1;
        this._dirty.splice(0);
    }

    buildDeferred(holder: NodeElement) {
        if (this._dirtyDeferred.length === 0) return;
        let mine = this._deferredScratch;
        mine.splice(0);
        for (let i = this._dirtyDeferred.length - 1; i >= 0; i--) {
            let element = this._dirtyDeferred[i];
            if (element.isActive && !(BuildOwner.deferredHolderOf(element) === holder)) continue;
            $eq.collections.listRemoveAt(this._dirtyDeferred, i);
            if (element.isActive) mine.push(element);
        }
        $eq.collections.listSortBy(mine, BuildOwner.byDepth, 'System.Comparison`1[eQuantic.UI.Primitives.ComponentElement]');
        try {
            for (let i = 0; i < mine.length; i++) {
                this._deferredCursor = i;
                if (this.waitsToBuild(mine[i])) mine[i].rebuild(false);
            }
        } finally {
            this._deferredCursor = -1;
        }
        mine.splice(0);
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
        for (const component of this._unmounting) component.notifyUnmounted();
        this._unmounting.splice(0);
        for (const element of this._mounting) {
            let stateful: any; 
            if ((($0) => (($0 instanceof StatefulComponent && $0.mountedAt != null && $0.mountedAt.isActive === true) && (stateful = $0, true)))(element.component)) stateful.notifyMounted();
        }
        this._mounting.splice(0);
    }

    toString(): string {
        return 'eQuantic.UI.Primitives.BuildOwner';
    }
}

