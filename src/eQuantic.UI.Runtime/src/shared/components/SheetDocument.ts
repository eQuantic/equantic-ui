import { $eq, CellRef, SheetCellSnapshot } from "../runtime-exports";

export class SheetDocument {
    constructor(rows: number = 1000, cols: number = 26, props?: any) {
        this._cells = $eq.collections.dictionary();
        this._rowHeights = $eq.collections.dictionary();
        this._colWidths = $eq.collections.dictionary();
        this.rows = Math.max(1, rows);
        this.cols = Math.max(1, Math.min(cols, 16384));
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    _cells: any;
    _rowHeights: any;
    _colWidths: any;
    static defaultRowHeight: number = 28;
    static defaultColWidth: number = 96;
    rows: number = 0;
    cols: number = 0;

    get cells(): any {
        return this._cells;
    }

    getCell(cell: CellRef) {
        let value: any;
        return (($0: any, $1: any) => ($0.has($1) ? ((value = $0.get($1)), true) : ((value = null), false)))(this._cells, cell.key) ? value : '';
    }

    setCell(cell: CellRef, value: string) {
        if (value.length === 0) this._cells.delete(cell.key); else $eq.mapSet(this._cells, cell.key, value);
    }

    rowHeight(row: number) {
        let height: any;
        return (($0: any) => ($0.has(row) ? ((height = $0.get(row)), true) : ((height = 0), false)))(this._rowHeights) ? height : SheetDocument.defaultRowHeight;
    }

    colWidth(col: number) {
        let width: any;
        return (($0: any) => ($0.has(col) ? ((width = $0.get(col)), true) : ((width = 0), false)))(this._colWidths) ? width : SheetDocument.defaultColWidth;
    }

    setRowHeight(row: number, height: number) {
        if (Math.abs(Math.fround(height - SheetDocument.defaultRowHeight)) < Math.fround(0.01)) this._rowHeights.delete(row); else $eq.mapSet(this._rowHeights, row, Math.max(12, height));
    }

    setColWidth(col: number, width: number) {
        if (Math.abs(Math.fround(width - SheetDocument.defaultColWidth)) < Math.fround(0.01)) this._colWidths.delete(col); else $eq.mapSet(this._colWidths, col, Math.max(24, width));
    }

    clamp(cell: CellRef) {
        return new CellRef(Math.min(Math.max(cell.row, 0), this.rows - 1), Math.min(Math.max(cell.col, 0), this.cols - 1));
    }

    hasValue(cell: CellRef) {
        return this._cells.has(cell.key);
    }

    shiftRows(atRow: number, delta: number) {
        let removed: SheetCellSnapshot[] = [];
        let moved: SheetCellSnapshot[] = [];
        for (const [key, value] of this._cells) {
            let cell = CellRef.fromKey(key);
            if (cell.row < atRow) continue;
            if (delta < 0 && cell.row < atRow - delta) {
                removed.push(new SheetCellSnapshot(cell, value));
                continue;
            }
            moved.push(new SheetCellSnapshot(cell, value));
        }
        for (const snapshot of removed) this._cells.delete(snapshot.cell.key);
        for (const snapshot of moved) this._cells.delete(snapshot.cell.key);
        for (const snapshot of moved) $eq.mapSet(this._cells, new CellRef(snapshot.cell.row + delta, snapshot.cell.col).key, snapshot.value);
        SheetDocument.shiftSizes(this._rowHeights, atRow, delta);
        this.rows = Math.max(1, this.rows + delta);
        return removed;
    }

    shiftCols(atCol: number, delta: number) {
        let removed: SheetCellSnapshot[] = [];
        let moved: SheetCellSnapshot[] = [];
        for (const [key, value] of this._cells) {
            let cell = CellRef.fromKey(key);
            if (cell.col < atCol) continue;
            if (delta < 0 && cell.col < atCol - delta) {
                removed.push(new SheetCellSnapshot(cell, value));
                continue;
            }
            moved.push(new SheetCellSnapshot(cell, value));
        }
        for (const snapshot of removed) this._cells.delete(snapshot.cell.key);
        for (const snapshot of moved) this._cells.delete(snapshot.cell.key);
        for (const snapshot of moved) $eq.mapSet(this._cells, new CellRef(snapshot.cell.row, snapshot.cell.col + delta).key, snapshot.value);
        SheetDocument.shiftSizes(this._colWidths, atCol, delta);
        this.cols = Math.max(1, Math.min(this.cols + delta, 16384));
        return removed;
    }

    static shiftSizes(sizes: any, at: number, delta: number) {
        if (sizes.size === 0) return;
        let entries = [...sizes];
        for (const entry of entries) {
            if (entry.key < at) continue;
            sizes.delete(entry.key);
        }
        for (const entry of entries) {
            if (entry.key < at) continue;
            if (delta < 0 && entry.key < at - delta) continue;
            $eq.mapSet(sizes, entry.key + delta, entry.value);
        }
    }
}

