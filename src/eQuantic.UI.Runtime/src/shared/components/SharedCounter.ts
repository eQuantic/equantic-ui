import { BuildContext, Button, Column, SharedStatefulComponent, Text } from "../runtime-exports";

export class SharedCounter extends SharedStatefulComponent {
    _count: number = 0;
    build(_context: BuildContext) {
        let column = new Column(12);column.add(new Text(`Count: ${this._count}`, 'title'));for (const cell of this.cells()) column.add(cell);column.add(new Button('Increment', 'primary', 'medium', () => this.setState(() => this._count++)));column.add(new Button('Later', 'primary', 'medium', this.bumpSoon.bind(this)));return column;
    }

    async bumpSoon() {
        await new Promise(resolve => setTimeout(resolve, 140));this.setState(() => this._count++);
    }

    cells() {
        const _seq = []; _seq.push(new Text('a', 'caption'));if (this._count > 3) return _seq;_seq.push(new Text('b', 'caption')); return _seq;
    }

}

