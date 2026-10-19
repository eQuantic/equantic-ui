import { $eq } from "../runtime-exports";

export class SparseTableSpans {
    constructor(count: number, extent: number, extents: any) {
        this._indices = null!;
        this._extents = null!;
        this._added = null!;
        this.count = 0;
        this.extent = 0;
        this.count = Math.max(0, count);
        this.extent = Math.max(0, extent);
        let kept: number[] = [];
        for (const index of extents.keys()) if (index >= 0 && index < this.count) kept.push(index);
        $eq.collections.listSort(kept, $eq.collections.order('value', 'comparable'));
        this._indices = kept.slice();
        this._extents = new Array(this._indices.length).fill(0);
        this._added = new Array(this._indices.length + 1).fill(0);
        for (let k = 0; k < this._indices.length; k++) {
            this._extents[k] = Math.max(0, $eq.mapGet(extents, this._indices[k]));
            this._added[k + 1] = this._added[k] + (this._extents[k] - this.extent);
        }
    }

    _indices!: number[];
    _extents!: number[];
    _added!: number[];
    count!: number;
    extent!: number;

    get total(): number {
        return this.offsetOf(this.count);
    }

    extentOf(index: number) {
        let k = this.before(index);
        return k < this._indices.length && this._indices[k] === index ? this._extents[k] : this.extent;
    }

    offsetOf(index: number) {
        let clamped = Math.min(Math.max(index, 0), this.count);
        return clamped * this.extent + this._added[this.before(clamped)];
    }

    indexAt(offset: number) {
        if (this.count === 0) return 0;
        let low = 0;
        let high = this.count - 1;
        while (low < high) {
            let middle = low + Math.trunc((high - low + 1) / 2);
            if (this.offsetOf(middle) <= offset) low = middle; else high = middle - 1;
        }
        return low;
    }

    before(index: number) {
        let low = 0;
        let high = this._indices.length;
        while (low < high) {
            let middle = Math.trunc((low + high) / 2);
            if (this._indices[middle] < index) low = middle + 1; else high = middle;
        }
        return low;
    }

    toString(): string {
        return 'eQuantic.UI.Components.SparseTableSpans';
    }
}

