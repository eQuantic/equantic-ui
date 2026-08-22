import { $eq, CellRef, SheetCellSnapshot } from "@equantic/runtime";

export class SheetDocument {
    constructor(rows: number = 1000, cols: number = 26, props?: any) {
        this._cells = {}; this._rowHeights = {}; this._colWidths = {}; this.rows = Math.max(1, rows);
        this.cols = Math.max(1, Math.min(cols, 16384)); if (props && typeof props === 'object') Object.assign(this, props);
    }

    _cells: Record<string, any>;
    _rowHeights: Record<string, any>;
    _colWidths: Record<string, any>;
    static defaultRowHeight: number = 28;
    static defaultColWidth: number = 96;
    rows: number = 0;
    cols: number = 0;

    get cells(): any {
        return this._cells;
    }

    getCell(cell: CellRef) {
        let value: any; return (Object.prototype.hasOwnProperty.call(this._cells, cell.key) ? ((value = this._cells[cell.key]), true) : false) ? value : '';
    }

    setCell(cell: CellRef, value: string) {
        if (value.length === 0) delete this._cells[cell.key]; else this._cells[cell.key] = value;
    }

    rowHeight(row: number) {
        let height: any; return (Object.prototype.hasOwnProperty.call(this._rowHeights, row) ? ((height = this._rowHeights[row]), true) : false) ? height : SheetDocument.defaultRowHeight;
    }

    colWidth(col: number) {
        let width: any; return (Object.prototype.hasOwnProperty.call(this._colWidths, col) ? ((width = this._colWidths[col]), true) : false) ? width : SheetDocument.defaultColWidth;
    }

    setRowHeight(row: number, height: number) {
        if (Math.abs(height - SheetDocument.defaultRowHeight) < 0.01) delete this._rowHeights[row]; else this._rowHeights[row] = Math.max(12, height);
    }

    setColWidth(col: number, width: number) {
        if (Math.abs(width - SheetDocument.defaultColWidth) < 0.01) delete this._colWidths[col]; else this._colWidths[col] = Math.max(24, width);
    }

    clamp(cell: CellRef) {
        return new CellRef(Math.min(Math.max(cell.row, 0), this.rows - 1), Math.min(Math.max(cell.col, 0), this.cols - 1));
    }

    hasValue(cell: CellRef) {
        return Object.prototype.hasOwnProperty.call(this._cells, cell.key);
    }

    shiftRows(atRow: number, delta: number) {
        let removed: SheetCellSnapshot[] = [];
        let moved: SheetCellSnapshot[] = [];
        for (const [key, value] of $eq.entries(this._cells, true)) {
            let cell = CellRef.fromKey(key);
            if (cell.row < atRow) continue;
            if (delta < 0 && cell.row < atRow - delta) {
                removed.push(new SheetCellSnapshot(cell, value));
                continue;
            }
            moved.push(new SheetCellSnapshot(cell, value));
        }
        for (const snapshot of removed) delete this._cells[snapshot.cell.key];
        for (const snapshot of moved) delete this._cells[snapshot.cell.key];
        for (const snapshot of moved) this._cells[new CellRef(snapshot.cell.row + delta, snapshot.cell.col).key] = snapshot.value;
        SheetDocument.shiftSizes(this._rowHeights, atRow, delta);
        this.rows = Math.max(1, this.rows + delta);
        return removed;
    }

    shiftCols(atCol: number, delta: number) {
        let removed: SheetCellSnapshot[] = [];
        let moved: SheetCellSnapshot[] = [];
        for (const [key, value] of $eq.entries(this._cells, true)) {
            let cell = CellRef.fromKey(key);
            if (cell.col < atCol) continue;
            if (delta < 0 && cell.col < atCol - delta) {
                removed.push(new SheetCellSnapshot(cell, value));
                continue;
            }
            moved.push(new SheetCellSnapshot(cell, value));
        }
        for (const snapshot of removed) delete this._cells[snapshot.cell.key];
        for (const snapshot of moved) delete this._cells[snapshot.cell.key];
        for (const snapshot of moved) this._cells[new CellRef(snapshot.cell.row, snapshot.cell.col + delta).key] = snapshot.value;
        SheetDocument.shiftSizes(this._colWidths, atCol, delta);
        this.cols = Math.max(1, Math.min(this.cols + delta, 16384));
        return removed;
    }

    static shiftSizes(sizes: Record<string, any>, at: number, delta: number) {
        if (Object.keys(sizes).length === 0) return;
        let entries = $eq.entries(sizes, true);
        for (const entry of entries) {
            if (entry.key < at) continue;
            delete sizes[entry.key];
        }
        for (const entry of entries) {
            if (entry.key < at) continue;
            if (delta < 0 && entry.key < at - delta) continue;
            sizes[entry.key + delta] = entry.value;
        }
    }
}

