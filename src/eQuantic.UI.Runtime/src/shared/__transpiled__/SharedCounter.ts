import { BuildContext, Button, Column, SharedStatefulComponent, Space, Text } from "@equantic/runtime";

export class SharedCounter extends SharedStatefulComponent {
    _count: number = 0;
    build(_context: BuildContext) {
        let column = new Column(Space.s3);column.add(new Text(`Count: ${this._count}`, 'title'));column.add(new Button('Increment', 'primary', 'medium', () => this.setState(() => this._count++)));return column;
    }

}

