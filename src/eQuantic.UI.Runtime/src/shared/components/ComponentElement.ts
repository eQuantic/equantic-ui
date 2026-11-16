import { $eq, BuildContext, ComponentContext, InheritedComponent, InheritedElement, StatefulComponent, UiComponent, VisualNode } from "../runtime-exports";
import { Element } from "./Element";

export class ComponentElement extends Element {
    constructor(component: UiComponent) {
        const $$frameReaderIndex: number = -1;
        const $$routeReaderIndex: number = -1;
        super(component);
        this._child = null;
        this._context = null;
        this._dependencies = null;
        this.component = null!;
        this.dirty = false;
        this.builtInFrame = 0;
        this.dependenciesChanged = false;
        this.frameReaderIndex = $$frameReaderIndex;
        this.routeReaderIndex = $$routeReaderIndex;
        this.component = component;
    }

    _child!: Element | null;
    _context!: BuildContext | null;
    _dependencies!: Set<InheritedElement> | null;
    component!: UiComponent;
    dirty!: boolean;
    builtInFrame!: number;
    dependenciesChanged!: boolean;
    frameReaderIndex!: number;
    routeReaderIndex!: number;

    get context(): BuildContext {
        return this._context ?? (this._context = new ComponentContext(this));
    }

    dependOn(matches: (value: InheritedComponent) => boolean) {
        let found = this.nearest(matches);
        if (found == null) return null;
        if (!this.isActive) return found.inherited;
        found.addDependent(this);
        $eq.collections.setAdd(this._dependencies ?? (this._dependencies = $eq.collections.hashSet('item')), found);
        return found.inherited;
    }

    find(matches: (value: InheritedComponent) => boolean) {
        return (this.nearest(matches)?.inherited ?? null);
    }

    nearest(matches: (value: InheritedComponent) => boolean) {
        for (let at = this.nearestInherited; !(at == null); at = at.above) {
            if (matches(at.inherited)) return at;
        }
        return null;
    }

    dependencyChanged() {
        if (!this.isActive) return;
        this.dependenciesChanged = true;
        if (this.dirty) return;
        this.dirty = true;
        this.owner.scheduleDependent(this);
    }

    childAt(slot: number) {
        return slot === 0 ? this._child : null;
    }

    visitChildren(visitor: (element: Element) => void) {
        if (!(this._child == null)) visitor(this._child);
    }

    firstBuild() {
        let stateful: any; 
        if (this.owner.live && (($0) => ($0 instanceof StatefulComponent && (stateful = $0, true)))(this.component) && stateful.enterPosition(this)) this.owner.queueMount(this);
        this.rebuild(false);
    }

    update(configuration: VisualNode, force: boolean) {
        if ((configuration === this.configuration)) {
            if (!force && !this.dirty) return;
        } else {
            this.configuration = configuration;
            let next = (configuration as UiComponent);
            let retained: any; 
            if ((($0) => ($0 instanceof StatefulComponent && (retained = $0, true)))(this.component)) {
                if (!(retained === next)) retained.adoptConfig(next);
            } else {
                this.component = next;
            }
        }
        this.rebuild(force);
    }

    rebuild(force: boolean) {
        this.dirty = false;
        this.builtInFrame = this.owner.frame;
        if (this.dependenciesChanged) {
            this.dependenciesChanged = false;
            let stateful: any; 
            if ((($0) => ($0 instanceof StatefulComponent && (stateful = $0, true)))(this.component)) stateful.notifyDependenciesChanged();
        }
        let built = this.buildContained();
        this._child = this.updateChild(this._child, built, 0, force);
    }

    buildContained() {
        return this.owner.buildComponent(this);
    }

    markNeedsBuild() {
        if (!this.isActive) return;
        this.owner.countMark();
        if (this.dirty) return;
        this.dirty = true;
        this.owner.schedule(this);
    }

    deactivateChildren() {
        if (this._child == null) return;
        this._child.deactivate();
        this._child = null;
    }

    onDeactivated() {
        this.owner.forgetFrameReader(this);
        this.owner.forgetRouteReader(this);
        if (!(this._dependencies == null)) {
            for (const dependency of this._dependencies) dependency.removeDependent(this);
            this._dependencies = null;
        }
        let stateful: any; 
        if (this.owner.live && (($0) => ($0 instanceof StatefulComponent && (stateful = $0, true)))(this.component) && stateful.leavePosition(this)) this.owner.queueUnmount(stateful);
    }

    toString(): string {
        return 'eQuantic.UI.Primitives.ComponentElement';
    }
}

