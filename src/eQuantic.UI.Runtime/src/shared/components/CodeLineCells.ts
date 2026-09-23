import { $eq, CodeCell } from "../runtime-exports";

export class CodeLineCells {
    constructor(text: string, tabSize: number, props?: any) {
        this.text = text;
        this.tabSize = Math.max(1, tabSize);
        let starts = $eq.text.textElementStarts(text);
        this._columns = new Array(starts.length + 1).fill(0);
        this._cells = new Array(starts.length + 1).fill(0);
        let cell = 0;
        for (let i = 0; i < starts.length; i++) {
            let start = starts[i];
            let end = i + 1 < starts.length ? starts[i + 1] : text.length;
            this._columns[i] = start;
            this._cells[i] = cell;
            cell += this.elementWidth(text, start, end, cell);
        }
        this._columns[starts.length] = text.length;
        this._cells[starts.length] = cell; if (props && typeof props === 'object') Object.assign(this, props);
    }

    _columns: number[];
    _cells: number[];
    declare text: string;
    tabSize: number = 0;

    get width(): number {
        return this._cells[this._cells.length - 1];
    }

    get count(): number {
        return this._columns.length - 1;
    }

    elementAt(index: number) {
        return new CodeCell(this._columns[index], this._columns[index + 1], this._cells[index], this._cells[index + 1] - this._cells[index]);
    }

    cellOf(column: number) {
        if (column <= 0) return 0;
        if (column >= this.text.length) return this.width;
        return this._cells[this.indexOf(column)];
    }

    columnAt(cell: number) {
        if (cell <= 0) return 0;
        for (let i = 0; i < this.count; i++) {
            let from = this._cells[i];
            let to = this._cells[i + 1];
            if (cell < to) return Math.fround(cell - from) <= Math.fround((to - from) / 2) ? this._columns[i] : this._columns[i + 1];
        }
        return this.text.length;
    }

    next(column: number) {
        if (column >= this.text.length) return this.text.length;
        if (column < 0) return 0;
        return this._columns[this.indexOf(column) + 1];
    }

    previous(column: number) {
        if (column <= 0) return 0;
        if (column > this.text.length) return this.text.length;
        let index = this.indexOf(column);
        return this._columns[index] === column ? this._columns[Math.max(0, index - 1)] : this._columns[index];
    }

    static widthOf(text: string, tabSize: number) {
        let stop = Math.max(1, tabSize);
        let cell = 0;
        for (const c of text) {
            if (c === '\t') cell += stop - cell % stop; else if (c < String.fromCharCode(0x80)) cell++; else return new CodeLineCells(text, tabSize).width;
        }
        return cell;
    }

    indexOf(column: number) {
        let low = 0;
        let high = this.count;
        while (low < high) {
            let middle = Math.trunc((low + high + 1) / 2);
            if (this._columns[middle] <= column) low = middle; else high = middle - 1;
        }
        return Math.min(low, this.count - 1);
    }

    elementWidth(text: string, start: number, end: number, cell: number) {
        let first = text[start];
        if (first === '\t') return this.tabSize - cell % this.tabSize;
        let codePoint = /^[\uD800-\uDBFF]$/.test(first) && start + 1 < end ? Number((first + text[start + 1]).codePointAt(0)) : first.charCodeAt(0);
        if (CodeLineCells.isWide(codePoint)) return 2;
        for (let i = start + 1; i < end; i++) {
            if (text[i] === '\uFE0F') return 2;
        }
        return 1;
    }

    static isWide(codePoint: number) {
        return (((((((((((((((((((((((((((((((((((((((((((((((((((((((((((codePoint >= 0x1100 && codePoint <= 0x115F) || (codePoint >= 0x231A && codePoint <= 0x231B)) || (codePoint >= 0x23E9 && codePoint <= 0x23EC)) || codePoint === 0x23F0) || codePoint === 0x23F3) || (codePoint >= 0x25FD && codePoint <= 0x25FE)) || (codePoint >= 0x2614 && codePoint <= 0x2615)) || (codePoint >= 0x2648 && codePoint <= 0x2653)) || codePoint === 0x267F) || codePoint === 0x2693) || codePoint === 0x26A1) || (codePoint >= 0x26AA && codePoint <= 0x26AB)) || (codePoint >= 0x26BD && codePoint <= 0x26BE)) || (codePoint >= 0x26C4 && codePoint <= 0x26C5)) || codePoint === 0x26CE) || codePoint === 0x26D4) || codePoint === 0x26EA) || (codePoint >= 0x26F2 && codePoint <= 0x26F3)) || codePoint === 0x26F5) || codePoint === 0x26FA) || codePoint === 0x26FD) || codePoint === 0x2705) || (codePoint >= 0x270A && codePoint <= 0x270B)) || codePoint === 0x2728) || codePoint === 0x274C) || codePoint === 0x274E) || (codePoint >= 0x2753 && codePoint <= 0x2755)) || codePoint === 0x2757) || (codePoint >= 0x2795 && codePoint <= 0x2797)) || codePoint === 0x27B0) || codePoint === 0x27BF) || (codePoint >= 0x2B1B && codePoint <= 0x2B1C)) || codePoint === 0x2B50) || codePoint === 0x2B55) || (codePoint >= 0x2E80 && codePoint <= 0x303E)) || (codePoint >= 0x3041 && codePoint <= 0x33FF)) || (codePoint >= 0x3400 && codePoint <= 0x4DBF)) || (codePoint >= 0x4E00 && codePoint <= 0x9FFF)) || (codePoint >= 0xA000 && codePoint <= 0xA4CF)) || (codePoint >= 0xA960 && codePoint <= 0xA97F)) || (codePoint >= 0xAC00 && codePoint <= 0xD7A3)) || (codePoint >= 0xF900 && codePoint <= 0xFAFF)) || (codePoint >= 0xFE10 && codePoint <= 0xFE19)) || (codePoint >= 0xFE30 && codePoint <= 0xFE6F)) || (codePoint >= 0xFF00 && codePoint <= 0xFF60)) || (codePoint >= 0xFFE0 && codePoint <= 0xFFE6)) || codePoint === 0x1F004) || codePoint === 0x1F0CF) || codePoint === 0x1F18E) || (codePoint >= 0x1F191 && codePoint <= 0x1F19A)) || (codePoint >= 0x1F1E6 && codePoint <= 0x1F1FF)) || (codePoint >= 0x1F200 && codePoint <= 0x1F251)) || (codePoint >= 0x1F300 && codePoint <= 0x1F64F)) || (codePoint >= 0x1F680 && codePoint <= 0x1F6FF)) || (codePoint >= 0x1F7E0 && codePoint <= 0x1F7EB)) || (codePoint >= 0x1F90C && codePoint <= 0x1F9FF)) || (codePoint >= 0x1FA70 && codePoint <= 0x1FAFF)) || (codePoint >= 0x20000 && codePoint <= 0x2FFFD)) || (codePoint >= 0x30000 && codePoint <= 0x3FFFD));
    }
}

