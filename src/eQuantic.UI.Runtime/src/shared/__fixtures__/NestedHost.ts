import { $eq, BuildContext, Button, Column, StatefulComponent } from "@equantic/runtime";
import { NestedChild } from "./NestedChild";

export class NestedHost extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Web.Tests.Fixtures.NestedHost';
    _generation: number = 0;

    build(_context: BuildContext) {
        let column = new Column(8);
        column.add(new Button('Bump', 'primary', 'medium', () => this.setState(() => this._generation++)));
        column.add(new NestedChild(`g${$eq.text.format(this._generation, null, undefined, undefined, 'int32')}`));
        return column;
    }
}

