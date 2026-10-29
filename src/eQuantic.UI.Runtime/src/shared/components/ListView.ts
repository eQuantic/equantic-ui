import { BuildContext, Column, ScrollView, SizeValue, Spacer, StatefulComponent, UiComponent, VisualNode } from "../runtime-exports";

export class ListView extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Components.ListView';
    _offset: number = 0;
    _viewport: number = 0;
    _first: number = 0;
    _last: number = -1;
    _overscan: number = 3;
    _width!: SizeValue;
    _height!: SizeValue;
    declare count: number;
    declare itemExtent: number;
    declare itemBuilder: (value: number) => VisualNode;

    get overscan() {
        return this._overscan;
    }

    set overscan(value) {
        this._overscan = value;
    }

    get width() {
        return this._width;
    }

    set width(value) {
        this._width = value;
    }

    get height() {
        return this._height;
    }

    set height(value) {
        this._height = value;
    }

    constructor(count?: any, itemExtent?: any, itemBuilder?: any, props?: any) {
        super();
        if (count !== undefined) this.count = count;
        if (itemExtent !== undefined) this.itemExtent = itemExtent;
        if (itemBuilder !== undefined) this.itemBuilder = itemBuilder;
        if (this.count === undefined) this.count = 0;
        if (this.itemExtent === undefined) this.itemExtent = 0;
        this.count = count;
        this.itemExtent = itemExtent;
        this.itemBuilder = itemBuilder;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(_context: BuildContext) {
        let viewport = this._viewport > 0 ? this._viewport : this.viewportGuess();
        [this._first, this._last] = this.windowFor(this._offset, viewport);
        let rows = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        if (this._first > 0) rows.add(Spacer.fixed(Math.fround(Math.fround(this._first) * this.itemExtent)));
        for (let i = this._first; i <= this._last; i++) rows.add(this.itemBuilder(i));
        if (this._last < this.count - 1) rows.add(Spacer.fixed(Math.fround(Math.fround(this.count - 1 - this._last) * this.itemExtent)));
        return new ScrollView(rows, 'vertical', { width: this.width, height: this.height, onScrolled: this.onScrolled.bind(this), onViewportChanged: this.onViewportChanged.bind(this) });
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof ListView && (fresh = next, true)))) return;
        this.count = fresh.count;
        this.itemExtent = fresh.itemExtent;
        this.itemBuilder = fresh.itemBuilder;
        this._overscan = fresh.overscan;
        this._width = fresh.width;
        this._height = fresh.height;
    }

    windowFor(offset: number, viewport: number): [number, number] {
        if (this.count === 0 || this.itemExtent <= 0) return [0, -1];
        let first = Math.max(0, (Math.trunc(Math.floor(Math.fround(offset / this.itemExtent))) | 0) - this.overscan);
        let last = Math.min(this.count - 1, (Math.trunc(Math.ceil(Math.fround(Math.fround(offset + viewport) / this.itemExtent))) | 0) + this.overscan);
        return [first, last];
    }

    viewportGuess() {
        return Math.fround(this.itemExtent * 12);
    }

    onScrolled(offset: number) {
        this._offset = offset;
        let [first, last] = this.windowFor(offset, this._viewport > 0 ? this._viewport : this.viewportGuess());
        if (first === this._first && last === this._last) return;
        this.setState(() => {});
    }

    onViewportChanged(viewport: number) {
        if (Math.abs(Math.fround(viewport - this._viewport)) < 0.5) return;
        this.setState(() => this._viewport = viewport);
    }
}

