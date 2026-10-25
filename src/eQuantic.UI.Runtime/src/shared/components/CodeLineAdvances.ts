import { $eq, CodeLineCells } from "../runtime-exports";

export class CodeLineAdvances {
    constructor(cells: CodeLineCells, measure: (value: string) => number) {
        this._cells = null!;
        this._measure = null!;
        this._advances = null!;
        this._cells = cells;
        this._measure = measure;
        this._advances = new Array(cells.count + 1).fill(0);
        for (let i = 1; i < this._advances.length; i++) this._advances[i] = NaN;
    }

    _cells!: CodeLineCells;
    _measure!: (value: string) => number;
    _advances!: number[];

    get text(): string {
        return this._cells.text;
    }

    get measure(): (value: string) => number {
        return this._measure;
    }

    at(index: number) {
        let advance = this._advances[index];
        if (!Number.isNaN(advance)) return advance;
        let column = index < this._cells.count ? this._cells.elementAt(index).start : this._cells.text.length;
        advance = this._measure($eq.text.substring(this._cells.text, 0, column));
        this._advances[index] = advance;
        return advance;
    }

    of(column: number) {
        if (column <= 0) return 0;
        if (column >= this._cells.text.length) return this.at(this._cells.count);
        return this.at(this._cells.indexOf(column));
    }

    columnAt(advance: number) {
        if (advance <= 0 || this._cells.count === 0) return 0;
        let after = this.firstEndingPast(advance);
        if (after < 0) return this._cells.text.length;
        let element = this._cells.elementAt(after);
        let from = this.at(after);
        let to = this.at(after + 1);
        return Math.fround(advance - from) <= Math.fround(Math.fround(to - from) / 2) ? element.start : element.end;
    }

    columnUnder(advance: number) {
        if (advance < 0 || this._cells.count === 0) return -1;
        let under = this.firstEndingPast(advance);
        return under < 0 ? -1 : this._cells.elementAt(under).start;
    }

    firstEndingPast(advance: number) {
        let low = 0;
        let high = this._cells.count - 1;
        let found = -1;
        while (low <= high) {
            let middle = Math.trunc((low + high) / 2);
            if (this.at(middle + 1) > advance) {
                found = middle;
                high = middle - 1;
            } else low = middle + 1;
        }
        return found;
    }

    toString(): string {
        return 'eQuantic.UI.Code.CodeLineAdvances';
    }
}

