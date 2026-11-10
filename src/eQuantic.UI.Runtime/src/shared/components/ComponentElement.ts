import { InFlow, StatefulComponent, UiComponent, VisualNode } from "../runtime-exports";
import { Element } from "./Element";

export class ComponentElement extends Element {
    constructor(component: UiComponent) {
        super(component);
        this._child = null;
        this.component = null!;
        this.dirty = false;
        this.builtInFrame = 0;
        this.component = component;
    }

    _child!: Element | null;
    component!: UiComponent;
    dirty!: boolean;
    builtInFrame!: number;

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
        let built = this.buildContained();
        this._child = this.updateChild(this._child, built, force);
    }

    buildContained() {
        let armed = InFlow.current;
        if (armed === this.underInFlow) return this.owner.buildComponent(this.component, this.owner.context, this.componentDepth);
        InFlow.current = this.underInFlow;
        try {
            return this.owner.buildComponent(this.component, this.owner.context, this.componentDepth);
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
        if (this.owner.live && (($0) => ($0 instanceof StatefulComponent && (stateful = $0, true)))(this.component) && stateful.leavePosition(this)) this.owner.queueUnmount(stateful);
    }

    toString(): string {
        return 'eQuantic.UI.Primitives.ComponentElement';
    }
}

