import { $eq, BuildOwner, ComponentElement, InFlow, InheritedComponent, InheritedElement, NodeElement, UiComponent, VisualNode } from "../runtime-exports";

export abstract class Element {
    constructor(configuration: VisualNode) {
        this.configuration = null!;
        this.parent = null;
        this.depth = 0;
        this.isActive = false;
        this.id = 0;
        this.slot = 0;
        this.owner = null!;
        this.componentDepth = 0;
        this.underInFlow = false;
        this.nearestInherited = null;
        this.matchStamp = 0;
        this.configuration = configuration;
    }

    configuration!: VisualNode;
    parent!: Element | null;
    depth!: number;
    isActive!: boolean;
    id!: number;
    slot!: number;
    owner!: BuildOwner;
    componentDepth!: number;
    underInFlow!: boolean;
    nearestInherited!: InheritedElement | null;
    matchStamp!: number;

    slotsBelow(depth: number) {
        let slots: number[] = [];
        for (let at: Element | null = this; !(at == null) && at.depth > depth; at = at.parent) $eq.collections.listAdd(slots, at.slot);
        $eq.collections.listReverse(slots);
        return $eq.text.join('.', slots, ($value) => $eq.text.format($value, null, undefined, undefined, 'int32'));
    }

    abstract childAt(slot: number): Element | null;
    abstract visitChildren(visitor: (element: Element) => void): void;

    static create(configuration: VisualNode) {
        return (() => { const $s = configuration; if ($s instanceof UiComponent) { const component = $s; return new ComponentElement(component); } if ($s instanceof InheritedComponent) { const inherited = $s; return new InheritedElement(inherited); } return new NodeElement(configuration); })();
    }

    static canUpdate(current: VisualNode, next: VisualNode) {
        return $eq.types.same($eq.types.of(current), $eq.types.of(next)) && $eq.text.equals(current.key, next.key, 'ordinal') && (current.globalKey === next.globalKey);
    }

    mount(owner: BuildOwner, parent: Element | null, slot: number) {
        this.owner = owner;
        this.parent = parent;
        this.slot = slot;
        this.id = owner.nextId();
        this.depth = parent == null ? 0 : parent.depth + 1;
        this.componentDepth = parent == null ? 0 : parent.componentDepth + (parent instanceof ComponentElement ? 1 : 0);
        this.underInFlow = !(parent == null) && (parent.underInFlow || parent.configuration instanceof InFlow);
        this.nearestInherited = (($as: any) => $as instanceof InheritedElement ? $as : null)(parent) ?? (parent?.nearestInherited ?? null);
        this.isActive = true;
        let key: any; 
        if ((key = this.configuration.globalKey) != null) owner.hold(key, this);
        this.firstBuild();
    }

    abstract firstBuild(): void;
    abstract update(configuration: VisualNode, force: boolean): void;

    updateChild(child: Element | null, configuration: VisualNode | null, slot: number, force: boolean) {
        if (configuration == null) {
            if (!(child == null)) this.owner.deactivate(child);
            return null;
        }
        if (!(child == null)) {
            if (Element.canUpdate(child.configuration, configuration)) {
                child.slot = slot;
                child.update(configuration, force);
                return child;
            }
            this.owner.deactivate(child);
        }
        return this.inflate(configuration, slot);
    }

    inflate(configuration: VisualNode, slot: number) {
        let key: any; let moved: any; 
        if ((key = configuration.globalKey) != null && (moved = this.owner.retake(key, configuration, this)) != null) {
            moved.reattach(this, slot);
            moved.update(configuration, true);
            return moved;
        }
        let created = Element.create(configuration);
        created.mount(this.owner, this, slot);
        return created;
    }

    reattach(parent: Element, slot: number) {
        this.parent = parent;
        this.slot = slot;
        this.isActive = true;
        this.rebase();
    }

    rebase() {
        let parent = this.parent;
        this.depth = parent == null ? 0 : parent.depth + 1;
        this.componentDepth = parent == null ? 0 : parent.componentDepth + (parent instanceof ComponentElement ? 1 : 0);
        this.underInFlow = !(parent == null) && (parent.underInFlow || parent.configuration instanceof InFlow);
        this.nearestInherited = (($as: any) => $as instanceof InheritedElement ? $as : null)(parent) ?? (parent?.nearestInherited ?? null);
        this.onRebased();
        this.visitChildren((child) => child.rebase());
    }

    onRebased() {}

    isAtOrBelow(element: Element) {
        for (let at: Element | null = this; !(at == null); at = at.parent) if ((at === element)) return true;
        return false;
    }

    abstract forgetChild(child: Element): void;
    abstract fillForgotten(): void;

    deactivate() {
        let key: any; 
        if ((key = this.configuration.globalKey) != null && this.owner.park(key, this)) {
            this.isActive = false;
            return;
        }
        this.leave();
    }

    leave() {
        this.deactivateChildren();
        this.isActive = false;
        let key: any; 
        if ((key = this.configuration.globalKey) != null) this.owner.letGo(key, this);
        this.onDeactivated();
    }

    abstract deactivateChildren(): void;

    onDeactivated() {}

    toString(): string {
        return 'eQuantic.UI.Primitives.Element';
    }
}

