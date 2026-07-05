import { $eq, BuildContext, Button, Column, Component, ComponentContext, HtmlElement, SharedStatefulComponent, Space, Text, UiComponent, VisualNode } from "@equantic/runtime";

export class NestedChild extends SharedStatefulComponent {
    _count: number = 0;
    _label: string;
    constructor(label: any = 'child', props?: any) {
        super(props);
        if (label !== undefined) this.label = label;
        this._label = label;
    }

    build(context: BuildContext) {
        let column = new Column(Space.s2);column.add(new Text(`${this._label}:${this._count}`, 'caption'));column.add(new Button('Add', 'primary', 'medium', () => this.setState(() => this._count++)));return column;
    }

    adoptConfig(next: UiComponent) {
        { let fresh; if ((next instanceof NestedChild && (fresh = next, true))) this._label = fresh._label; }
    }

}

