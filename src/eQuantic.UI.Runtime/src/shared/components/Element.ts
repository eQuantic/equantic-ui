import { $eq, BuildOwner, ComponentElement, InFlow, InheritedComponent, InheritedElement, NodeElement, UiComponent, VisualNode } from "../runtime-exports";

export abstract class Element {
    constructor(configuration: VisualNode) {
        this.configuration = null!;
        this.parent = null;
        this.depth = 0;
        this.isActive = false;
        this.id = 0;
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
    owner!: BuildOwner;
    componentDepth!: number;
    underInFlow!: boolean;
    nearestInherited!: InheritedElement | null;
    matchStamp!: number;
    abstract childAt(slot: number): Element | null;
    abstract visitChildren(visitor: (element: Element) => void): void;

    static create(configuration: VisualNode) {
        return (() => { const $s = configuration; if ($s instanceof UiComponent) { const component = $s; return new ComponentElement(component); } if ($s instanceof InheritedComponent) { const inherited = $s; return new InheritedElement(inherited); } return new NodeElement(configuration); })();
    }

    static canUpdate(current: VisualNode, next: VisualNode) {
        return $eq.types.same($eq.types.of(current), $eq.types.of(next)) && $eq.text.equals(current.key, next.key, 'ordinal');
    }

    mount(owner: BuildOwner, parent: Element | null) {
        this.owner = owner;
        this.parent = parent;
        this.id = owner.nextId();
        this.depth = parent == null ? 0 : parent.depth + 1;
        this.componentDepth = parent == null ? 0 : parent.componentDepth + (parent instanceof ComponentElement ? 1 : 0);
        this.underInFlow = !(parent == null) && (parent.underInFlow || parent.configuration instanceof InFlow);
        this.nearestInherited = (($as: any) => $as instanceof InheritedElement ? $as : null)(parent) ?? (parent?.nearestInherited ?? null);
        this.isActive = true;
        this.firstBuild();
    }

    abstract firstBuild(): void;
    abstract update(configuration: VisualNode, force: boolean): void;

    updateChild(child: Element | null, configuration: VisualNode | null, force: boolean) {
        if (configuration == null) {
            if (!(child == null)) this.owner.deactivate(child);
            return null;
        }
        if (!(child == null)) {
            if (Element.canUpdate(child.configuration, configuration)) {
                child.update(configuration, force);
                return child;
            }
            this.owner.deactivate(child);
        }
        return this.inflate(configuration);
    }

    inflate(configuration: VisualNode) {
        let created = Element.create(configuration);
        created.mount(this.owner, this);
        return created;
    }

    deactivate() {
        this.deactivateChildren();
        this.isActive = false;
        this.onDeactivated();
    }

    abstract deactivateChildren(): void;

    onDeactivated() {}

    toString(): string {
        return 'eQuantic.UI.Primitives.Element';
    }
}

