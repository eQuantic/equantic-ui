import { BuildContext, Column, Component, ComponentContext, HtmlElement, SharedStatefulComponent, Space, Text, VisualNode } from "@equantic/runtime";
import { Button } from "./Button";

export class SharedCounter extends SharedStatefulComponent {
    _count: number = 0;
    build(context: BuildContext) {
        let column = new Column(Space.s3);column.add(new Text(`Count: ${this._count}`, 'title'));column.add(new Button('Increment', 'primary', 'medium', () => this.setState(() => this._count++)));return column;
    }

}

