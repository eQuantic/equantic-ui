import { $eq, BuildContext, Button, Column, Component, ComponentContext, HtmlElement, SharedStatefulComponent, Space, VisualNode } from "@equantic/runtime";
import { NestedChild } from "./NestedChild";

export class NestedHost extends SharedStatefulComponent {
    _generation: number = 0;
    build(context: BuildContext) {
        let column = new Column(Space.s2);column.add(new Button('Bump', 'primary', 'medium', () => this.setState(() => this._generation++)));column.add(new NestedChild(`g${this._generation}`));return column;
    }

}

