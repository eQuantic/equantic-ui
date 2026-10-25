import { $eq, CellRef, Point, PointerKindValue, PointerPhaseValue, SheetAxisValue, SheetCellSnapshot, SheetDocument, SheetEdit, SheetGrid, SheetHistory, SheetKeymap, SheetMotionValue, SheetRange, TsvCodec } from "../runtime-exports";

export class SheetController {
    constructor(rows: number = 1000, cols: number = 26) {
        this._selection = new SheetRange(new CellRef(0, 0));
        this._active = new CellRef(0, 0);
        this.document = null!;
        this.history = new SheetHistory();
        this.changed = null;
        this.revealVersion = 0;
        this.revealCell = CellRef.$zero();
        this.editing = false;
        this.draft = '';
        this.fillSource = null;
        this.fillTarget = null;
        this.grid = SheetGrid.default;
        this._selecting = false;
        this._tapped = null;
        this._dragAt = null;
        this.document = new SheetDocument(rows, cols);
    }

    _selection!: SheetRange;
    _active!: CellRef;
    document!: SheetDocument;
    history!: SheetHistory;
    changed!: ((sheetEdit: SheetEdit) => void) | null;
    revealVersion!: number;
    revealCell!: CellRef;
    editing!: boolean;
    draft!: string;
    fillSource!: SheetRange | null;
    fillTarget!: SheetRange | null;
    grid!: SheetGrid;
    _selecting!: boolean;
    _tapped!: CellRef | null;
    _dragAt!: Point | null;
    static fillHandleGrab: number = 8;

    get selection(): SheetRange {
        return this._selection;
    }

    set selection(value: SheetRange) {
        this.select(value);
        this.reveal();
    }

    get activeCell(): CellRef {
        return this._active;
    }

    get filling(): boolean {
        return !(this.fillSource == null);
    }

    select(range: SheetRange) {
        this._selection = new SheetRange(this.document.clamp(range.anchor), this.document.clamp(range.focus));
        this._active = this._selection.focus;
    }

    move(dRow: number, dCol: number, motion: SheetMotionValue = 'cell', extend: boolean = false) {
        let from = this.activeCell;
        let landed = (() => { const $s = motion; if ($s === 'dataEdge') return this.dataEdge(from, dRow, dCol); if ($s === 'rowBoundary') return new CellRef(from.row, dCol < 0 ? 0 : this.document.cols - 1); if ($s === 'sheetBoundary') return dRow < 0 || dCol < 0 ? new CellRef(0, 0) : new CellRef(this.document.rows - 1, this.document.cols - 1); return new CellRef(from.row + dRow, from.col + dCol); })();
        landed = this.document.clamp(landed);
        this._selection = extend ? new SheetRange(this._selection.anchor, landed) : new SheetRange(landed);
        this._active = landed;
        this.reveal();
    }

    movePage(direction: number, extend: boolean = false) {
        let scroll: any; 
        if (!((scroll = this.grid.scroll) != null)) return false;
        let page = Math.max(this.document.rowSpans.extentOf(this._active.row), Math.fround(scroll.viewport.height - this.grid.headerHeight));
        let from = this.document.rowSpans.offsetOf(this._active.row);
        let row = this.document.rowSpans.indexAt(from + Math.sign(direction) * page);
        let landed = this.document.clamp(new CellRef(row, this._active.col));
        this._selection = extend ? new SheetRange(this._selection.anchor, landed) : new SheetRange(landed);
        this._active = landed;
        this.reveal();
        return true;
    }

    reveal() {
        this.revealVersion++;
        this.revealCell = this._active;
    }

    dataEdge(from: CellRef, dRow: number, dCol: number) {
        let cell = from;
        let next = new CellRef(cell.row + dRow, cell.col + dCol);
        if (!this.inBounds(next)) return cell;
        if (this.document.hasValue(cell) && this.document.hasValue(next)) {
            while (this.inBounds(next) && this.document.hasValue(next)) {
                cell = next;
                next = new CellRef(cell.row + dRow, cell.col + dCol);
            }
            return cell;
        }
        cell = next;
        while (this.inBounds(cell) && !this.document.hasValue(cell)) cell = new CellRef(cell.row + dRow, cell.col + dCol);
        return this.inBounds(cell) ? cell : this.edge(from, dRow, dCol);
    }

    edge(from: CellRef, dRow: number, dCol: number) {
        return new CellRef(dRow === 0 ? from.row : dRow < 0 ? 0 : this.document.rows - 1, dCol === 0 ? from.col : dCol < 0 ? 0 : this.document.cols - 1);
    }

    inBounds(cell: CellRef) {
        return cell.row >= 0 && cell.row < this.document.rows && cell.col >= 0 && cell.col < this.document.cols;
    }

    step(dRow: number, dCol: number) {
        if (this._selection.isSingleCell) {
            this.move(dRow, dCol);
            return;
        }
        let range = this._selection;
        let row = this._active.row;
        let col = this._active.col;
        if (dCol !== 0) {
            col += dCol;
            if (col > range.rightCol) {
                col = range.leftCol;
                row = row === range.bottomRow ? range.topRow : row + 1;
            } else if (col < range.leftCol) {
                col = range.rightCol;
                row = row === range.topRow ? range.bottomRow : row - 1;
            }
        } else {
            row += dRow;
            if (row > range.bottomRow) {
                row = range.topRow;
                col = col === range.rightCol ? range.leftCol : col + 1;
            } else if (row < range.topRow) {
                row = range.bottomRow;
                col = col === range.leftCol ? range.rightCol : col - 1;
            }
        }
        this._active = new CellRef(row, col);
        this.reveal();
    }

    selectRows(fromRow: number, toRow: number) {
        this._selection = new SheetRange(new CellRef(fromRow, 0), new CellRef(toRow, this.document.cols - 1));
        this._active = new CellRef(fromRow, 0);
    }

    selectCols(fromCol: number, toCol: number) {
        this._selection = new SheetRange(new CellRef(0, fromCol), new CellRef(this.document.rows - 1, toCol));
        this._active = new CellRef(0, fromCol);
    }

    selectAll() {
        this._selection = new SheetRange(new CellRef(0, 0), new CellRef(this.document.rows - 1, this.document.cols - 1));
        this._active = new CellRef(0, 0);
    }

    selectTo(cell: CellRef) {
        return this._selection = new SheetRange(this._selection.anchor, this.document.clamp(cell));
    }

    beginEdit(seed: string) {
        this.editing = true;
        this.draft = seed;
    }

    typeIntoDraft(text: string) {
        return this.draft += text;
    }

    eraseFromDraft() {
        if (this.draft.length > 0) this.draft = this.draft.slice(0, -1);
    }

    commitEdit() {
        if (!this.editing) return false;
        let changed = this.setCell(this.activeCell, this.draft);
        this.editing = false;
        this.draft = '';
        return changed;
    }

    cancelEdit() {
        this.editing = false;
        this.draft = '';
    }

    setCell(cell: CellRef, value: string) {
        cell = this.document.clamp(cell);
        let old = this.document.getCell(cell);
        if (old === value) return false;
        let $n55: any; 
        let edit = ($n55 = new SheetEdit(), $n55.kind = 'setCells', $n55.before = [new SheetCellSnapshot(cell, old)], $n55.after = [new SheetCellSnapshot(cell, value)], $n55.selectionBefore = this._selection, $n55.selectionAfter = this._selection, $n55);
        this.document.setCell(cell, value);
        this.commit(edit);
        return true;
    }

    clearSelection() {
        let $n56: any; 
        let edit = ($n56 = new SheetEdit(), $n56.kind = 'setCells', $n56.selectionBefore = this._selection, $n56.selectionAfter = this._selection, $n56);
        for (let row = this._selection.topRow; row <= this._selection.bottomRow; row++) {
            for (let col = this._selection.leftCol; col <= this._selection.rightCol; col++) {
                let cell = new CellRef(row, col);
                let old = this.document.getCell(cell);
                if (old.length === 0) continue;
                edit.before.push(new SheetCellSnapshot(cell, old));
                edit.after.push(new SheetCellSnapshot(cell, ''));
                this.document.setCell(cell, '');
            }
        }
        if (edit.before.length === 0) return false;
        this.commit(edit);
        return true;
    }

    insert(axis: SheetAxisValue, at: number, count: number = 1) {
        if (count < 1) return;
        let selectionBefore = this._selection;
        if (axis === 'rows') this.document.shiftRows(at, count); else this.document.shiftCols(at, count);
        let $n57: any; 
        this.commit(($n57 = new SheetEdit(), $n57.kind = axis === 'rows' ? 'insertRows' : 'insertCols', $n57.at = at, $n57.count = count, $n57.selectionBefore = selectionBefore, $n57.selectionAfter = this._selection, $n57));
    }

    delete(axis: SheetAxisValue, at: number, count: number = 1) {
        if (count < 1) return;
        let selectionBefore = this._selection;
        let removed = axis === 'rows' ? this.document.shiftRows(at, -count) : this.document.shiftCols(at, -count);
        this.select(this._selection);
        let $n58: any; 
        this.commit(($n58 = new SheetEdit(), $n58.kind = axis === 'rows' ? 'deleteRows' : 'deleteCols', $n58.at = at, $n58.count = count, $n58.removed = removed, $n58.selectionBefore = selectionBefore, $n58.selectionAfter = this._selection, $n58));
    }

    resize(axis: SheetAxisValue, index: number, size: number) {
        let old = axis === 'rows' ? this.document.rowHeight(index) : this.document.colWidth(index);
        if (Math.abs(Math.fround(old - size)) < Math.fround(0.01)) return;
        if (axis === 'rows') this.document.setRowHeight(index, size); else this.document.setColWidth(index, size);
        let $n59: any; 
        this.commit(($n59 = new SheetEdit(), $n59.kind = axis === 'rows' ? 'resizeRow' : 'resizeCol', $n59.at = index, $n59.oldSize = old, $n59.newSize = axis === 'rows' ? this.document.rowHeight(index) : this.document.colWidth(index), $n59.selectionBefore = this._selection, $n59.selectionAfter = this._selection, $n59));
    }

    copyText() {
        return TsvCodec.serialize(this.document, this._selection);
    }

    cut() {
        let text = this.copyText();
        this.clearSelection();
        return text;
    }

    paste(text: string) {
        let grid = TsvCodec.parse(text);
        if (grid.length === 0) return false;
        let origin = this._selection.topLeft;
        let maxCols = 0;
        for (const line of grid) maxCols = Math.max(maxCols, line.length);
        if (origin.row + grid.length > this.document.rows) this.document.rows = origin.row + grid.length;
        if (origin.col + maxCols > this.document.cols) this.document.cols = Math.min(origin.col + maxCols, 16384);
        let $n60: any; 
        let edit = ($n60 = new SheetEdit(), $n60.kind = 'setCells', $n60.selectionBefore = this._selection, $n60);
        for (let r = 0; r < grid.length; r++) {
            let line = grid[r];
            for (let c = 0; c < line.length; c++) {
                let cell = new CellRef(origin.row + r, origin.col + c);
                if (!this.inBounds(cell)) continue;
                let old = this.document.getCell(cell);
                if (old === line[c]) continue;
                edit.before.push(new SheetCellSnapshot(cell, old));
                edit.after.push(new SheetCellSnapshot(cell, line[c]));
                this.document.setCell(cell, line[c]);
            }
        }
        this._selection = new SheetRange(origin, this.document.clamp(new CellRef(origin.row + grid.length - 1, origin.col + maxCols - 1)));
        this.reveal();
        if (edit.before.length === 0) return false;
        edit.selectionAfter = this._selection;
        this.commit(edit);
        return true;
    }

    beginFill() {
        this.fillSource = this._selection;
        this.fillTarget = null;
    }

    updateFill(at: CellRef) {
        let source: any; 
        if (!((source = this.fillSource) != null)) return;
        at = this.document.clamp(at);
        let dRow = at.row < source.topRow ? at.row - source.topRow : at.row > source.bottomRow ? at.row - source.bottomRow : 0;
        let dCol = at.col < source.leftCol ? at.col - source.leftCol : at.col > source.rightCol ? at.col - source.rightCol : 0;
        if (dRow === 0 && dCol === 0) {
            this.fillTarget = null;
            return;
        }
        this.fillTarget = Math.abs(dRow) >= Math.abs(dCol) ? dRow > 0 ? new SheetRange(new CellRef(source.bottomRow + 1, source.leftCol), new CellRef(at.row, source.rightCol)) : new SheetRange(new CellRef(at.row, source.leftCol), new CellRef(source.topRow - 1, source.rightCol)) : dCol > 0 ? new SheetRange(new CellRef(source.topRow, source.rightCol + 1), new CellRef(source.bottomRow, at.col)) : new SheetRange(new CellRef(source.topRow, at.col), new CellRef(source.bottomRow, source.leftCol - 1));
    }

    commitFill() {
        let source = this.fillSource;
        let target = this.fillTarget;
        this.fillSource = null;
        this.fillTarget = null;
        if (source == null || target == null) return false;
        let selectionBefore = this._selection;
        this._selection = new SheetRange(new CellRef(Math.min($eq.nullable.value(source).topRow, $eq.nullable.value(target).topRow), Math.min($eq.nullable.value(source).leftCol, $eq.nullable.value(target).leftCol)), new CellRef(Math.max($eq.nullable.value(source).bottomRow, $eq.nullable.value(target).bottomRow), Math.max($eq.nullable.value(source).rightCol, $eq.nullable.value(target).rightCol)));
        this._active = $eq.nullable.value(source).topLeft;
        return this.fill($eq.nullable.value(source), $eq.nullable.value(target), selectionBefore);
    }

    cancelFill() {
        this.fillSource = null;
        this.fillTarget = null;
    }

    fill(source: SheetRange, target: SheetRange, selectionBefore: SheetRange | null = null) {
        let $n61: any; 
        let edit = ($n61 = new SheetEdit(), $n61.kind = 'setCells', $n61.selectionBefore = selectionBefore ?? this._selection, $n61);
        let sourceRows = source.bottomRow - source.topRow + 1;
        let sourceCols = source.rightCol - source.leftCol + 1;
        for (let row = target.topRow; row <= target.bottomRow; row++) {
            for (let col = target.leftCol; col <= target.rightCol; col++) {
                let cell = new CellRef(row, col);
                if (!this.inBounds(cell) || source.contains(cell)) continue;
                let from = new CellRef(source.topRow + SheetController.mod(row - source.topRow, sourceRows), source.leftCol + SheetController.mod(col - source.leftCol, sourceCols));
                let value = this.document.getCell(from);
                let old = this.document.getCell(cell);
                if (old === value) continue;
                edit.before.push(new SheetCellSnapshot(cell, old));
                edit.after.push(new SheetCellSnapshot(cell, value));
                this.document.setCell(cell, value);
            }
        }
        if (edit.before.length === 0) return false;
        edit.selectionAfter = this._selection;
        this.commit(edit);
        this.reveal();
        return true;
    }

    static mod(value: number, size: number) {
        return $eq.num.intRem($eq.num.intRem(value, size) + size, size);
    }

    fillDown() {
        let selection = this._selection;
        if (selection.topRow === selection.bottomRow) {
            if (selection.topRow === 0) return false;
            let above = new SheetRange(new CellRef(selection.topRow - 1, selection.leftCol), new CellRef(selection.topRow - 1, selection.rightCol));
            return this.fill(above, selection);
        }
        let first = new SheetRange(new CellRef(selection.topRow, selection.leftCol), new CellRef(selection.topRow, selection.rightCol));
        return this.fill(first, selection);
    }

    fillRight() {
        let selection = this._selection;
        if (selection.leftCol === selection.rightCol) {
            if (selection.leftCol === 0) return false;
            let left = new SheetRange(new CellRef(selection.topRow, selection.leftCol - 1), new CellRef(selection.bottomRow, selection.leftCol - 1));
            return this.fill(left, selection);
        }
        let first = new SheetRange(new CellRef(selection.topRow, selection.leftCol), new CellRef(selection.bottomRow, selection.leftCol));
        return this.fill(first, selection);
    }

    cellAt(point: Point) {
        let offset = this.grid.offset;
        let across = Math.max(Math.fround(point.x - this.grid.headerWidth), 0) + offset.across;
        let down = Math.max(Math.fround(point.y - this.grid.headerHeight), 0) + offset.down;
        return this.document.clamp(new CellRef(this.document.rowSpans.indexAt(down), this.document.columnSpans.indexAt(across)));
    }

    fillHandleCorner() {
        let offset = this.grid.offset;
        return new Point(Math.fround((this.grid.headerWidth + this.document.columnSpans.offsetOf(this._selection.rightCol + 1) - offset.across)), Math.fround((this.grid.headerHeight + this.document.rowSpans.offsetOf(this._selection.bottomRow + 1) - offset.down)));
    }

    onFillHandle(position: Point) {
        if (this.editing) return false;
        let corner = this.fillHandleCorner();
        return corner.x > this.grid.headerWidth && corner.y > this.grid.headerHeight && Math.abs(Math.fround(position.x - corner.x)) <= SheetController.fillHandleGrab && Math.abs(Math.fround(position.y - corner.y)) <= SheetController.fillHandleGrab;
    }

    cursorAt(position: Point) {
        return this.onFillHandle(position) ? 'crosshair' : 'default';
    }

    handleKey(key: string, modifiers: number, clipboard: any) {
        return SheetKeymap.handle(this, key, modifiers, clipboard);
    }

    handleText(text: string) {
        if (text.length === 0) return false;
        SheetKeymap.type(this, text);
        return true;
    }

    handlePointer(phase: PointerPhaseValue, position: Point, modifiers: number, clicks: number, kind: PointerKindValue) {
        switch (phase) {
            case 'down':
                {
                    this._selecting = false;
                    this._tapped = null;
                    if (this.filling) this.cancelFill();
                    if (kind === 'touch') {
                        this._tapped = this.cellAt(position);
                        return false;
                    }
                    if (this.editing) this.commitEdit();
                    if (this.onFillHandle(position)) {
                        this.beginFill();
                        this._dragAt = position;
                        return true;
                    }
                    this.press(this.cellAt(position), modifiers, clicks);
                    this._selecting = clicks < 2 && (modifiers & 1) === 0;
                    if (this._selecting) this._dragAt = position;
                    return true;
                }
            case 'move':
                {
                    if (!this.filling && !this._selecting) return false;
                    this._dragAt = position;
                    let past = this.pastTheCells(position);
                    this.grid.scroll?.autoScroll(past ? position : null);
                    return this.drag(position) || past;
                }
            case 'up':
                {
                    this.stopDrag();
                    let tapped: any; 
                    if ((tapped = this._tapped) != null) {
                        this._tapped = null;
                        if (this.editing) this.commitEdit();
                        this.press(tapped, modifiers, clicks);
                        return true;
                    }
                    if (!this.filling) return false;
                    this.commitFill();
                    return true;
                }
            case 'cancel':
                {
                    this.stopDrag();
                    this._tapped = null;
                    if (!this.filling) return false;
                    this.cancelFill();
                    return true;
                }
            default:
                return false;
        }
    }

    follow() {
        let at: any; 
        return (at = this._dragAt) != null && (this.filling || this._selecting) && this.drag(at);
    }

    press(cell: CellRef, modifiers: number, clicks: number) {
        if ((modifiers & 1) !== 0) this.selectTo(cell); else this.select(new SheetRange(cell));
        if (clicks >= 2) this.beginEdit(this.document.getCell(cell));
    }

    drag(position: Point) {
        let cell = this.cellAt(this.insideTheCells(position));
        if (this.filling) {
            let before = this.fillTarget;
            this.updateFill(cell);
            return !$eq.equals(this.fillTarget, before);
        }
        if ($eq.equals(cell, this._selection.focus)) return false;
        this.select(new SheetRange(this._selection.anchor, cell));
        return true;
    }

    pastTheCells(position: Point) {
        let scroll: any; 
        return (scroll = this.grid.scroll) != null && (position.x < this.grid.headerWidth || position.y < this.grid.headerHeight || position.x >= scroll.viewport.width || position.y >= scroll.viewport.height);
    }

    insideTheCells(position: Point) {
        let scroll: any; 
        if (!((scroll = this.grid.scroll) != null) || scroll.viewport.width <= 0 || scroll.viewport.height <= 0) return position;
        return new Point(Math.min(Math.max(position.x, this.grid.headerWidth), Math.max(this.grid.headerWidth, Math.fround(scroll.viewport.width - 1))), Math.min(Math.max(position.y, this.grid.headerHeight), Math.max(this.grid.headerHeight, Math.fround(scroll.viewport.height - 1))));
    }

    stopDrag() {
        this._selecting = false;
        if (this._dragAt == null) return;
        this._dragAt = null;
        this.grid.scroll?.autoScroll(null);
    }

    undo() {
        let edit = this.history.popUndo();
        if (edit == null) return false;
        this.apply(edit, false);
        this._selection = edit.selectionBefore;
        this._active = this._selection.focus;
        this.reveal();
        this.changed?.(edit);
        return true;
    }

    redo() {
        let edit = this.history.popRedo();
        if (edit == null) return false;
        this.apply(edit, true);
        this._selection = edit.selectionAfter;
        this._active = this._selection.focus;
        this.reveal();
        this.changed?.(edit);
        return true;
    }

    apply(edit: SheetEdit, forward: boolean) {
        switch (edit.kind) {
            case 'setCells':
                let cells = forward ? edit.after : edit.before;
                for (const snapshot of cells) this.document.setCell(snapshot.cell, snapshot.value);
                break;
            case 'insertRows':
                if (forward) this.document.shiftRows(edit.at, edit.count); else this.document.shiftRows(edit.at, -edit.count);
                break;
            case 'deleteRows':
                if (forward) this.document.shiftRows(edit.at, -edit.count); else {
                    this.document.shiftRows(edit.at, edit.count);
                    for (const snapshot of edit.removed) this.document.setCell(snapshot.cell, snapshot.value);
                }
                break;
            case 'insertCols':
                if (forward) this.document.shiftCols(edit.at, edit.count); else this.document.shiftCols(edit.at, -edit.count);
                break;
            case 'deleteCols':
                if (forward) this.document.shiftCols(edit.at, -edit.count); else {
                    this.document.shiftCols(edit.at, edit.count);
                    for (const snapshot of edit.removed) this.document.setCell(snapshot.cell, snapshot.value);
                }
                break;
            case 'resizeRow':
                this.document.setRowHeight(edit.at, forward ? edit.newSize : edit.oldSize);
                break;
            case 'resizeCol':
                this.document.setColWidth(edit.at, forward ? edit.newSize : edit.oldSize);
                break;
        }
    }

    commit(edit: SheetEdit) {
        this.history.push(edit);
        this.changed?.(edit);
    }

    toString(): string {
        return 'eQuantic.UI.Sheet.SheetController';
    }
}

