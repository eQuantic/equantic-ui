import { $eq, SheetAxisValue, SheetDocument, TableSpans } from "../runtime-exports";

export class SheetSpans extends TableSpans {
    constructor(document: SheetDocument, axis: SheetAxisValue) {
        super();
        this._document = null!;
        this._axis = 'rows';
        this._readAt = 0;
        this._indices = null!;
        this._added = null!;
        this._document = document;
        this._axis = axis;
        this._readAt = -1;
        this._indices = [];
        this._added = [0];
    }

    _document!: SheetDocument;
    _axis!: SheetAxisValue;
    _readAt!: number;
    _indices!: number[];
    _added!: number[];

    get count(): number {
        return this._axis === 'rows' ? this._document.rows : this._document.cols;
    }

    get extent(): number {
        return this._axis === 'rows' ? 28 : 96;
    }

    get total(): number {
        return this.offsetOf(this.count);
    }

    extentOf(index: number) {
        return this._axis === 'rows' ? this._document.rowHeight(index) : this._document.colWidth(index);
    }

    offsetOf(index: number) {
        this.read();
        let clamped = Math.min(Math.max(index, 0), this.count);
        return clamped * this.extent + this._added[this.before(clamped)];
    }

    indexAt(offset: number) {
        let count = this.count;
        if (count === 0) return 0;
        let low = 0;
        let high = count - 1;
        while (low < high) {
            let middle = low + Math.trunc((high - low + 1) / 2);
            if (this.offsetOf(middle) <= offset) low = middle; else high = middle - 1;
        }
        return low;
    }

    read() {
        if (this._readAt === this._document.sizeVersion) return;
        this._readAt = this._document.sizeVersion;
        let sizes = this._axis === 'rows' ? this._document.rowHeights : this._document.colWidths;
        let indices: number[] = $eq.collections.listCopy(sizes.keys());
        $eq.collections.listSort(indices, $eq.collections.order('value', 'comparable'));
        this._indices = indices.slice();
        this._added = new Array(this._indices.length + 1).fill(0);
        for (let k = 0; k < this._indices.length; k++) this._added[k + 1] = this._added[k] + ($eq.mapGet(sizes, this._indices[k]) - this.extent);
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
}

