import { Element, InFlow, StatefulComponent, UiComponent, VisualNode } from "../runtime-exports";

export class ComponentElement extends Element {
    constructor(component: UiComponent) {
        super(component);
        this._child = null;
        this.component = null!;
        this.dirty = false;
        this.builtInFrame = 0;
        this._component = component;
    }

    _child!: Element | null;
    component!: UiComponent;
    dirty!: boolean;
    builtInFrame!: number;

    childAt(slot: number) {
        return slot === 0 ? this._child : null;
    }

    firstBuild() {
        let stateful: any; 
        if (this.owner.live && (($0) => ($0 instanceof StatefulComponent && (stateful = $0, true)))(this._component) && stateful.place(this)) this.owner.queueMount(this);
        this.rebuild(false);
    }

    update(configuration: VisualNode, force: boolean) {
        if ((configuration === this.configuration)) {
            if (!force && !this.dirty) return;
        } else {
            this.configuration = configuration;
            let next = configuration;
            let retained: any; 
            if ((($0) => ($0 instanceof StatefulComponent && (retained = $0, true)))(this._component)) {
                if (!(retained === next)) retained.adoptConfig(next);
            } else {
                this._component = next;
            }
        }
        this.rebuild(force);
    }

    rebuild(force: boolean) {
        this.dirty = false;
        this.builtInFrame = this.owner.frame;
        let built = this.buildContained();
        this._child = this.updateChild(this._child, built, force);
    }

    buildContained() {
        let armed = InFlow.current;
        if (armed === this.underInFlow) return this.owner.buildComponent(this._component, this.owner.context, this.componentDepth);
        InFlow.current = this.underInFlow;
        try {
            return this.owner.buildComponent(this._component, this.owner.context, this.componentDepth);
        } finally {
            InFlow.current = armed;
        }
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
        let stateful: any; 
        if (this.owner.live && (($0) => ($0 instanceof StatefulComponent && (stateful = $0, true)))(this._component) && stateful.leave(this)) this.owner.queueUnmount(stateful);
    }

    toString(): string {
        return 'eQuantic.UI.Primitives.ComponentElement';
    }
}

