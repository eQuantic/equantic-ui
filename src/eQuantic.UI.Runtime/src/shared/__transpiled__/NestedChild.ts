import { BuildContext, Button, Column, SharedStatefulComponent, Text, UiComponent } from "@equantic/runtime";

export class NestedChild extends SharedStatefulComponent {
    _count: number = 0;
    _label: string;

    constructor(label: any = 'child', props?: any) {
        super();
        this._label = label;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(_context: BuildContext) {
        let column = new Column(8);
        column.add(new Text(`${this._label}:${this._count}`, 'caption'));
        column.add(new Button('Add', 'primary', 'medium', () => this.setState(() => this._count++)));
        return column;
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if ((next instanceof NestedChild && (fresh = next, true))) this._label = fresh._label;
    }
}

