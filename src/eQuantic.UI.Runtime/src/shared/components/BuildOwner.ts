import { $eq, BuildContext, ComponentElement, Element, ElementIds, NodeElement, StatefulComponent, UiComponent, VisualNode, WindowSizeClassValue } from "../runtime-exports";

export class BuildOwner {
    constructor(buildComponent: (arg1: UiComponent, arg2: BuildContext, arg3: number) => VisualNode = undefined!, ids: ElementIds | null = undefined!, live: boolean = undefined!) {
        if (arguments.length >= 1 && arguments.length <= 2) {
            let $c0: any, $c1: any, $c2: any;
            {
                let buildComponent: any = arguments[0], ids: any = arguments[1] === undefined ? null : arguments[1];
                $c0 = buildComponent;
                $c1 = ids;
                $c2 = true;
            }
            buildComponent = $c0;
            ids = $c1;
            live = $c2;
        }
        this._dirty = [];
        this._dirtyNextFrame = [];
        this._dirtyDeferred = [];
        this._deferredScratch = [];
        this._mounting = [];
        this._unmounting = [];
        this._deferred = [];
        this._context = null;
        this._building = 0;
        this._stamp = 0;
        this._ids = null!;
        this._buildComponent = null!;
        this.buildRequested = null;
        this.root = null;
        this.frame = 0;
        this.rebuildingAll = false;
        this.marks = 0;
        this.live = false;
        this.sizeClass = 'compact';
        this.keyScratch = $eq.collections.dictionary();
        $eq.guard.throwIfNull(buildComponent, 'buildComponent');
        this._buildComponent = buildComponent;
        this._ids = ids ?? new ElementIds();
        this.live = live;
    }

    _dirty!: ComponentElement[];
    _dirtyNextFrame!: ComponentElement[];
    _dirtyDeferred!: ComponentElement[];
    _deferredScratch!: ComponentElement[];
    _mounting!: ComponentElement[];
    _unmounting!: StatefulComponent[];
    _deferred!: NodeElement[];
    _context!: BuildContext | null;
    _building!: number;
    _stamp!: number;
    _ids!: ElementIds;
    _buildComponent!: (arg1: UiComponent, arg2: BuildContext, arg3: number) => VisualNode;
    buildRequested!: (() => void) | null;
    root!: Element | null;
    frame!: number;
    rebuildingAll!: boolean;
    marks!: number;
    live!: boolean;
    sizeClass!: WindowSizeClassValue;
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

    static structural(buildComponent: (arg1: UiComponent, arg2: BuildContext, arg3: number) => VisualNode, ids: ElementIds | null = null) {
        return new BuildOwner(buildComponent, ids, false);
    }

    buildComponent(component: UiComponent, context: BuildContext, depth: number) {
        return this._buildComponent(component, context, depth);
    }

    countMark() {
        return this.marks++;
    }

    build(root: VisualNode, context: BuildContext, sizeClass: WindowSizeClassValue, rebuildAll: boolean) {
        this._context = context;
        this.sizeClass = sizeClass;
        this.frame++;
        this.rebuildingAll = rebuildAll;
        this.enterBuild();
        try {
            if (this.root == null || !Element.canUpdate(this.root.configuration, root)) {
                if (!(this.root == null)) this.deactivate(this.root);
                this.root = Element.create(root);
                this.root.mount(this, null);
            } else if (rebuildAll || !(this.root.configuration === root)) {
                this.root.update(root, rebuildAll);
            }
            this.buildDirty();
        } finally {
            this.exitBuild();
        }
        return this.root;
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

    nextStamp() {
        return ++this._stamp;
    }

    nextId() {
        return this._ids.next();
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
            let element = this._dirty[i];
            if (!this.waitsToBuild(element)) continue;
            if (!(BuildOwner.deferredHolderOf(element) == null)) {
                this._dirtyDeferred.push(element);
                continue;
            }
            element.rebuild(false);
        }
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
        for (const element of mine) {
            if (this.waitsToBuild(element)) element.rebuild(false);
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

