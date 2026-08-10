import { BuildContext, Column, ScrollView, SharedStatefulComponent, SizeValue, Spacer, UiComponent } from "../runtime-exports";

export class ListView extends SharedStatefulComponent {
    _offset: number = 0;
    _viewport: number = 0;
    _first: number = 0;
    _last: number = -1;
    declare count: number;
    declare itemExtent: number;
    declare itemBuilder: any;
    declare overscan: number;
    declare width: SizeValue;
    declare height: SizeValue;
    constructor(count?: any, itemExtent?: any, itemBuilder?: any, props?: any) {
        super();
        if (count !== undefined) this.count = count;
        if (itemExtent !== undefined) this.itemExtent = itemExtent;
        if (itemBuilder !== undefined) this.itemBuilder = itemBuilder;
        if (this.overscan === undefined) this.overscan = 3;
        this.count = count;this.itemExtent = itemExtent;this.itemBuilder = itemBuilder;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(_context: BuildContext) {
        let viewport = this._viewport > 0 ? this._viewport : this.viewportGuess();[this._first, this._last] = this.windowFor(this._offset, viewport);let rows = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });if (this._first > 0) rows.add(Spacer.fixed(this._first * this.itemExtent));for (let i = this._first; i <= this._last; i++) rows.add(this.itemBuilder(i));if (this._last < this.count - 1) rows.add(Spacer.fixed((this.count - 1 - this._last) * this.itemExtent));return new ScrollView(rows, 'vertical', { width: this.width, height: this.height, onScrolled: this.onScrolled.bind(this), onViewportChanged: this.onViewportChanged.bind(this) });
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; if (!((next instanceof ListView && (fresh = next, true)))) return;this.count = fresh.count;this.itemExtent = fresh.itemExtent;this.itemBuilder = fresh.itemBuilder;
    }

    windowFor(offset: number, viewport: number) {
        if (this.count === 0 || this.itemExtent <= 0) return [0, -1];let first = Math.max(0, (Math.trunc(Math.floor(offset / this.itemExtent)) | 0) - this.overscan);let last = Math.min(this.count - 1, (Math.trunc(Math.ceil((offset + viewport) / this.itemExtent)) | 0) + this.overscan);return [first, last];
    }

    viewportGuess() {
        return this.itemExtent * 12;
    }

    onScrolled(offset: number) {
        this._offset = offset;let [first, last] = this.windowFor(offset, this._viewport > 0 ? this._viewport : this.viewportGuess());if (first === this._first && last === this._last) return;this.setState(() => {});
    }

    onViewportChanged(viewport: number) {
        if (Math.abs(viewport - this._viewport) < 0.5) return;this.setState(() => this._viewport = viewport);
    }

}

