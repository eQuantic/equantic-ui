import { BuildContext, Button, Column, SharedStatefulComponent } from "../runtime-exports";
import { NestedChild } from "./NestedChild";

export class NestedHost extends SharedStatefulComponent {
    _generation: number = 0;
    build(_context: BuildContext) {
        let column = new Column(8);column.add(new Button('Bump', 'primary', 'medium', () => this.setState(() => this._generation++)));column.add(new NestedChild(`g${this._generation}`));return column;
    }

}

