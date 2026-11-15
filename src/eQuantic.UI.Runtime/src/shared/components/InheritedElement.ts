import { $eq, ComponentElement, InheritedComponent, VisualNode } from "../runtime-exports";
import { Element } from "./Element";

export class InheritedElement extends Element {
    constructor(inherited: InheritedComponent) {
        const $$_dependents: Set<ComponentElement> = $eq.collections.hashSet('item');
        super(inherited);
        this._child = null;
        this._dependents = $$_dependents;
    }

    _child!: Element | null;
    _dependents!: Set<ComponentElement>;

    get inherited(): InheritedComponent {
        return (this.configuration as InheritedComponent);
    }

    get above(): InheritedElement | null {
        return this.nearestInherited;
    }

    childAt(slot: number) {
        return slot === 0 ? this._child : null;
    }

    visitChildren(visitor: (element: Element) => void) {
        if (!(this._child == null)) visitor(this._child);
    }

    firstBuild() {
        this._child = this.updateChild(null, this.inherited.child, 0, false);
    }

    update(configuration: VisualNode, force: boolean) {
        if ((configuration === this.configuration)) {
            if (!force) return;
        } else {
            let old = this.inherited;
            this.configuration = configuration;
            if (this.inherited.updateShouldNotify(old)) this.notifyDependents();
        }
        this._child = this.updateChild(this._child, this.inherited.child, 0, force);
    }

    addDependent(dependent: ComponentElement) {
        $eq.collections.setAdd(this._dependents, dependent);
    }

    removeDependent(dependent: ComponentElement) {
        this._dependents.delete(dependent);
    }

    notifyDependents() {
        for (const dependent of this._dependents) dependent.dependencyChanged();
    }

    deactivateChildren() {
        if (this._child == null) return;
        this._child.deactivate();
        this._child = null;
    }

    onDeactivated() {
        this._dependents.clear();
    }

    toString(): string {
        return 'eQuantic.UI.Primitives.InheritedElement';
    }
}

