import { $eq } from "../runtime-exports";

export class CellRef { declare row: number; declare col: number; constructor(row: any = 0, col: any = 0) { this.row = row; this.col = col; } equals(o: unknown) { return o instanceof CellRef && $eq.equals(this.row, o.row) && $eq.equals(this.col, o.col); } with(patch: any) { return new CellRef(('row' in patch ? patch.row : this.row), ('col' in patch ? patch.col : this.col)); } static fromKey(key: number) { return new CellRef(Math.trunc(key / CellRef.maxCols), key % CellRef.maxCols); } compareTo(other: CellRef) { return this.row !== other.row ? Math.sign(this.row - other.row) : Math.sign(this.col - other.col); } toA1() { let name = '';
    let col = this.col;
    while (true) {
        name = String.fromCharCode((65 + col % 26)) + name;
        col = Math.trunc(col / 26) - 1;
        if (col < 0) break;
    }
    return name + (this.row + 1); } static parseA1(text: string) { if (!text) return null;
    let col = 0;
    let i = 0;
    while (i < text.length && text[i] >= 'A' && text[i] <= 'Z') {
        col = col * 26 + (text[i].charCodeAt(0) - 65 + 1);
        i++;
    }
    if (i === 0 || i === text.length) return null;
    let row = 0;
    while (i < text.length) {
        if (text[i] < '0' || text[i] > '9') return null;
        row = row * 10 + (text[i].charCodeAt(0) - 48);
        i++;
    }
    if (row === 0) return null;
    return new CellRef(row - 1, col - 1); } static maxCols = 16384; get key() { return this.row * CellRef.maxCols + this.col; } toString() { return `CellRef { Row = ${this.row}, Col = ${this.col} }`; } }
