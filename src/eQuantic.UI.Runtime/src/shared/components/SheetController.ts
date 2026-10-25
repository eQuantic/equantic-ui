import { $eq, CellRef, CodeCommandValue, CodeDocument, CodeEditorController, CodeGrid, CodeKeymap, CodeRange, DateTime, KeyboardConventionValue, Point, PointerKindValue, PointerPhaseValue, Rect, SheetAxisValue, SheetCellSnapshot, SheetDocument, SheetEdit, SheetEditModeValue, SheetEntryLanguage, SheetEntryViewValue, SheetFormulaBar, SheetGrid, SheetHistory, SheetKeymap, SheetMotionValue, SheetNameBox, SheetRange, SheetReference, Size, TsvCodec } from "../runtime-exports";

export class SheetController {
    constructor(rows: number = 1000, cols: number = 26) {
        this._selection = new SheetRange(new CellRef(0, 0));
        this._active = new CellRef(0, 0);
        this.formulaBar = null!;
        this.nameBox = null!;
        this.document = null!;
        this.history = new SheetHistory();
        this.changed = null;
        this.refreshed = null;
        this.revealVersion = 0;
        this.revealCell = CellRef.$zero();
        this.editing = false;
        this.entry = null!;
        this.editMode = 'enter';
        this.entryView = 'cell';
        this.now = () => $eq.time.dateTime.now();
        this.fillSource = null;
        this.fillTarget = null;
        this.grid = SheetGrid.default;
        this._selecting = false;
        this._tapped = null;
        this._entryPressed = false;
        this._slide = 0;
        this._dragAt = null;
        this._widestOf = null;
        this._widestBy = null;
        this._widest = 0;
        this._focusVersion = 0;
        this.tabReleased = false;
        this._entryKeysFor = null;
        this.document = new SheetDocument(rows, cols);
        this.entry = new CodeEditorController('', new SheetEntryLanguage());
        this.formulaBar = new SheetFormulaBar(this);
        this.nameBox = new SheetNameBox(this);
    }

    _selection!: SheetRange;
    _active!: CellRef;
    formulaBar!: SheetFormulaBar;
    nameBox!: SheetNameBox;
    document!: SheetDocument;
    history!: SheetHistory;
    changed!: ((sheetEdit: SheetEdit) => void) | null;
    refreshed!: (() => void) | null;
    revealVersion!: number;
    revealCell!: CellRef;
    editing!: boolean;
    entry!: CodeEditorController;
    editMode!: SheetEditModeValue;
    entryView!: SheetEntryViewValue;
    now!: () => DateTime;
    fillSource!: SheetRange | null;
    fillTarget!: SheetRange | null;
    grid!: SheetGrid;
    _selecting!: boolean;
    _tapped!: CellRef | null;
    _entryPressed!: boolean;
    _slide!: number;
    _dragAt!: Point | null;
    _widestOf!: CodeDocument | null;
    _widestBy!: ((value: string) => number) | null;
    _widest!: number;
    _focusVersion!: number;
    tabReleased!: boolean;
    _entryKeysFor!: KeyboardConventionValue | null;
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

    get selecting(): boolean {
        return this._selecting && !this._selection.isSingleCell;
    }

    get filling(): boolean {
        return !(this.fillSource == null);
    }

    get lineHeight(): number {
        return this.grid.lineHeight > 0 ? this.grid.lineHeight : 20;
    }

    get editorRange(): SheetRange {
        let active = this._active;
        if (!this.editing) return new SheetRange(active);
        let padding = this.grid.cellPadding;
        let wide = Math.fround(Math.fround(Math.fround(this.widestEntryLine() + padding.start) + padding.end) + 2);
        let tall = Math.fround(Math.fround(Math.fround(Math.fround(this.entry.document.lineCount) * this.lineHeight) + padding.top) + padding.bottom);
        let last = this.lastInView();
        let right = active.col;
        let width = this.document.columnSpans.extentOf(right);
        while (width < wide && right < last.col) {
            right++;
            width += this.document.columnSpans.extentOf(right);
        }
        let bottom = active.row;
        let height = this.document.rowSpans.extentOf(bottom);
        while (height < tall && bottom < last.row) {
            bottom++;
            height += this.document.rowSpans.extentOf(bottom);
        }
        return new SheetRange(active, new CellRef(bottom, right));
    }

    get editorBounds(): Rect {
        return this.boundsOf(this.editorRange);
    }

    get cellEditorGrid(): CodeGrid {
        let bounds = this.editorBounds;
        let padding = this.grid.cellPadding;
        let left = Math.fround(bounds.x + padding.start);
        let right = Math.fround(Math.fround(bounds.right - padding.end) - 2);
        this.entry.grid = this.cellGrid(bounds, this._slide);
        let caret = this.entry.caretRect(this.entry.caret).x;
        let slide = this._slide;
        if (caret > right) slide = Math.fround(slide + Math.fround(caret - right)); else if (caret < left) slide = Math.fround(slide - Math.fround(left - caret));
        slide = Math.min(Math.max(slide, 0), Math.max(0, Math.fround(this.widestEntryLine() - Math.fround(right - left))));
        if (slide !== this._slide) {
            this._slide = slide;
            this.entry.grid = this.cellGrid(bounds, slide);
        }
        return this.entry.grid;
    }

    get carets(): Rect[] {
        if (!this.editing || this.entryView !== 'cell') return [];
        this.cellEditorGrid;
        return this.entry.carets;
    }

    get inputBounds(): Rect {
        let carets: any; 
        return ((this.carets != null && $eq.collections.count(this.carets) > 0) && (carets = this.carets, true)) ? $eq.collections.item(carets, 0) : this.boundsOf(new SheetRange(this._active));
    }

    get focusVersion(): number {
        return this._focusVersion;
    }

    refresh() {
        return this.refreshed?.();
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

    beginEdit(seed: string, mode: SheetEditModeValue) {
        this.editing = true;
        this.editMode = mode;
        this.entryView = 'cell';
        this._slide = 0;
        this.entry.document = CodeDocument.fromText(seed);
        this.entry.selection = new CodeRange(this.entry.document.end);
    }

    enterEditMode() {
        if (this.editing) this.editMode = 'edit';
    }

    holdEntryIn(view: SheetEntryViewValue) {
        this.entryView = view;
        this.useView();
    }

    useView() {
        if (this.entryView === 'formulaBar') this.entry.grid = this.formulaBar.grid; else this.cellEditorGrid;
    }

    goTo(reference: string) {
        let range: any; 
        if (!($o => (range = $o.range, $o.$))(SheetReference.tryParse(reference, this.document.rows, this.document.cols))) return false;
        if (this.editing) this.commitEdit();
        this._selection = range;
        this._active = range.topLeft;
        this.reveal();
        this.requestFocus();
        return true;
    }

    toggleEditMode() {
        if (this.editing) this.editMode = this.editMode === 'enter' ? 'edit' : 'enter';
    }

    commitEdit() {
        if (!this.editing) return false;
        let changed = this.setCell(this.activeCell, this.entry.document.text);
        this.endEdit();
        return changed;
    }

    cancelEdit() {
        if (this.editing) this.endEdit();
    }

    fillSelectionWithEntry() {
        if (!this.editing) return false;
        let value = this.entry.document.text;
        this.endEdit();
        return this.fillSelection(value);
    }

    endEdit() {
        this.editing = false;
        this.entryView = 'cell';
        this.entry.document = CodeDocument.fromText('');
    }

    typeNow(time: boolean) {
        let text = $eq.text.format(this.now(), time ? 't' : 'd');
        if (!this.editing) this.beginEdit(text, 'enter'); else this.entry.insert(text);
    }

    breakLine() {
        if (this.editing) this.entry.insert('\n');
    }

    setCell(cell: CellRef, value: string) {
        cell = this.document.clamp(cell);
        let old = this.document.getCell(cell);
        if (old === value) return false;
        let $n51: any; 
        let edit = ($n51 = new SheetEdit(), $n51.kind = 'setCells', $n51.before = [new SheetCellSnapshot(cell, old)], $n51.after = [new SheetCellSnapshot(cell, value)], $n51.selectionBefore = this._selection, $n51.selectionAfter = this._selection, $n51);
        this.document.setCell(cell, value);
        this.commit(edit);
        return true;
    }

    clearSelection() {
        let $n52: any; 
        let edit = ($n52 = new SheetEdit(), $n52.kind = 'setCells', $n52.selectionBefore = this._selection, $n52.selectionAfter = this._selection, $n52);
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

    fillSelection(value: string) {
        let $n53: any; 
        let edit = ($n53 = new SheetEdit(), $n53.kind = 'setCells', $n53.selectionBefore = this._selection, $n53.selectionAfter = this._selection, $n53);
        for (let row = this._selection.topRow; row <= this._selection.bottomRow; row++) {
            for (let col = this._selection.leftCol; col <= this._selection.rightCol; col++) {
                let cell = new CellRef(row, col);
                let old = this.document.getCell(cell);
                if (old === value) continue;
                edit.before.push(new SheetCellSnapshot(cell, old));
                edit.after.push(new SheetCellSnapshot(cell, value));
                this.document.setCell(cell, value);
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
        let $n54: any; 
        this.commit(($n54 = new SheetEdit(), $n54.kind = axis === 'rows' ? 'insertRows' : 'insertCols', $n54.at = at, $n54.count = count, $n54.selectionBefore = selectionBefore, $n54.selectionAfter = this._selection, $n54));
    }

    delete(axis: SheetAxisValue, at: number, count: number = 1) {
        if (count < 1) return;
        let selectionBefore = this._selection;
        let removed = axis === 'rows' ? this.document.shiftRows(at, -count) : this.document.shiftCols(at, -count);
        this.select(this._selection);
        let $n55: any; 
        this.commit(($n55 = new SheetEdit(), $n55.kind = axis === 'rows' ? 'deleteRows' : 'deleteCols', $n55.at = at, $n55.count = count, $n55.removed = removed, $n55.selectionBefore = selectionBefore, $n55.selectionAfter = this._selection, $n55));
    }

    resize(axis: SheetAxisValue, index: number, size: number) {
        let old = axis === 'rows' ? this.document.rowHeight(index) : this.document.colWidth(index);
        if (Math.abs(Math.fround(old - size)) < Math.fround(0.01)) return;
        if (axis === 'rows') this.document.setRowHeight(index, size); else this.document.setColWidth(index, size);
        let $n56: any; 
        this.commit(($n56 = new SheetEdit(), $n56.kind = axis === 'rows' ? 'resizeRow' : 'resizeCol', $n56.at = index, $n56.oldSize = old, $n56.newSize = axis === 'rows' ? this.document.rowHeight(index) : this.document.colWidth(index), $n56.selectionBefore = this._selection, $n56.selectionAfter = this._selection, $n56));
    }

    copyText() {
        return this.editing ? this.entry.document.textIn(this.entry.selection) : TsvCodec.serialize(this.document, this._selection);
    }

    cut() {
        if (this.editing) return this.entry.selection.isEmpty ? '' : this.entry.cut();
        let text = this.copyText();
        this.clearSelection();
        return text;
    }

    paste(text: string) {
        if (this.editing) return this.entry.paste(text);
        let grid = TsvCodec.parse(text);
        if (grid.length === 0) return false;
        let origin = this._selection.topLeft;
        let maxCols = 0;
        for (const line of grid) maxCols = Math.max(maxCols, line.length);
        if (origin.row + grid.length > this.document.rows) this.document.rows = origin.row + grid.length;
        if (origin.col + maxCols > this.document.cols) this.document.cols = Math.min(origin.col + maxCols, 16384);
        let $n57: any; 
        let edit = ($n57 = new SheetEdit(), $n57.kind = 'setCells', $n57.selectionBefore = this._selection, $n57);
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
        let $n58: any; 
        let edit = ($n58 = new SheetEdit(), $n58.kind = 'setCells', $n58.selectionBefore = selectionBefore ?? this._selection, $n58);
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

    boundsOf(range: SheetRange) {
        let offset = this.grid.offset;
        let left = this.grid.headerWidth + this.document.columnSpans.offsetOf(range.leftCol) - offset.across;
        let top = this.grid.headerHeight + this.document.rowSpans.offsetOf(range.topRow) - offset.down;
        let right = this.grid.headerWidth + this.document.columnSpans.offsetOf(range.rightCol + 1) - offset.across;
        let bottom = this.grid.headerHeight + this.document.rowSpans.offsetOf(range.bottomRow + 1) - offset.down;
        return new Rect(Math.fround(left), Math.fround(top), Math.fround((right - left)), Math.fround((bottom - top)));
    }

    lastInView() {
        let scroll: any; 
        if (!((scroll = this.grid.scroll) != null) || scroll.viewport.width <= 0 || scroll.viewport.height <= 0) return new CellRef(this.document.rows - 1, this.document.cols - 1);
        let offset = this.grid.offset;
        let across = offset.across + Math.max(0, Math.fround(scroll.viewport.width - this.grid.headerWidth)) - 0.5;
        let down = offset.down + Math.max(0, Math.fround(scroll.viewport.height - this.grid.headerHeight)) - 0.5;
        return this.document.clamp(new CellRef(this.document.rowSpans.indexAt(down), this.document.columnSpans.indexAt(across)));
    }

    widestEntryLine() {
        let measure: any; 
        if (!((measure = this.grid.measure) != null)) return 0;
        let document = this.entry.document;
        if ((document === this._widestOf) && (measure === this._widestBy)) return this._widest;
        let widest = 0;
        for (let line = 0; line < document.lineCount; line++) widest = Math.max(widest, measure(document.line(line)));
        this._widestOf = document;
        this._widestBy = measure;
        this._widest = widest;
        return widest;
    }

    cellGrid(bounds: Rect, slide: number) {
        let padding = this.grid.cellPadding;
        let origin = new Point(Math.fround(Math.fround(bounds.x + padding.start) - slide), Math.fround(bounds.y + padding.top));
        let measure: any; 
        return (measure = this.grid.measure) != null ? new CodeGrid(origin, new Size(Math.max(measure(' '), 4), this.lineHeight), undefined, measure) : new CodeGrid(origin, new Size(8, this.lineHeight));
    }

    useCellView() {
        this.entryView = 'cell';
        this.cellEditorGrid;
    }

    inEditor(position: Point) {
        return this.editing && this.editorBounds.contains(position);
    }

    onFillHandle(position: Point) {
        if (this.editing) return false;
        let corner = this.fillHandleCorner();
        return corner.x > this.grid.headerWidth && corner.y > this.grid.headerHeight && Math.abs(Math.fround(position.x - corner.x)) <= SheetController.fillHandleGrab && Math.abs(Math.fround(position.y - corner.y)) <= SheetController.fillHandleGrab;
    }

    cursorAt(position: Point) {
        return this.inEditor(position) ? 'text' : this.onFillHandle(position) ? 'crosshair' : 'default';
    }

    handleKey(key: string, modifiers: number, convention: KeyboardConventionValue, clipboard: any) {
        return SheetKeymap.handle(this, key, modifiers, convention, clipboard);
    }

    handleText(text: string) {
        if (text.length === 0) return false;
        this.tabReleased = false;
        if (!this.editing) this.beginEdit('', 'enter');
        this.useCellView();
        return this.entry.handleText(text);
    }

    setComposition(text: string) {
        if (!this.editing) {
            if (text.length === 0) return false;
            this.tabReleased = false;
            this.beginEdit('', 'enter');
        }
        this.useCellView();
        return this.entry.setComposition(text);
    }

    requestFocus() {
        return this._focusVersion++;
    }

    focusChanged(focused: boolean) {
        if (focused) this.tabReleased = false; else if (this.editing && this.entryView === 'cell') this.entry.focusChanged(false);
    }

    static notACellsCommand(command: CodeCommandValue) {
        return (((((((((((((((command === 'indent' || command === 'outdent') || command === 'toggleLineComment') || command === 'moveLinesUp') || command === 'moveLinesDown') || command === 'copyLinesUp') || command === 'copyLinesDown') || command === 'deleteLines') || command === 'joinLines') || command === 'insertLineAbove') || command === 'insertLineBelow') || command === 'completionShow') || command === 'signatureHelpShow') || command === 'hoverShow') || command === 'problemNext') || command === 'problemPrevious');
    }

    entryKey(key: string, modifiers: number, convention: KeyboardConventionValue) {
        if (this._entryKeysFor !== convention) {
            $eq.collections.clear(this.entry.keyBindings);
            for (const binding of CodeKeymap.defaults(convention)) {
                if (SheetController.notACellsCommand(binding.command)) $eq.collections.add(this.entry.keyBindings, $eq.withPatch(binding, { command: 'none' }));
            }
            this._entryKeysFor = convention;
        }
        this.useView();
        return this.entry.handleKey(key, modifiers, convention, null);
    }

    handlePointer(phase: PointerPhaseValue, position: Point, modifiers: number, clicks: number, kind: PointerKindValue) {
        switch (phase) {
            case 'down':
                {
                    this._selecting = false;
                    this._tapped = null;
                    this._entryPressed = false;
                    if (this.filling) this.cancelFill();
                    if (kind === 'touch') {
                        this._tapped = this.cellAt(position);
                        return false;
                    }
                    if (this.inEditor(position)) {
                        this.editMode = 'edit';
                        this.useCellView();
                        this._entryPressed = true;
                        this.entry.handlePointer('down', position, modifiers, clicks);
                        return true;
                    }
                    if (this.editing) this.commitEdit();
                    if (this.onFillHandle(position)) {
                        this.beginFill();
                        this._dragAt = position;
                        return true;
                    }
                    this.press(this.cellAt(position), position, modifiers, clicks);
                    this._selecting = clicks < 2 && (modifiers & 1) === 0;
                    if (this._selecting) this._dragAt = position;
                    return true;
                }
            case 'move':
                {
                    if (this._entryPressed) {
                        this.useCellView();
                        return this.entry.handlePointer('move', position, modifiers, clicks);
                    }
                    if (!this.filling && !this._selecting) return false;
                    this._dragAt = position;
                    let past = this.pastTheCells(position);
                    this.grid.scroll?.autoScroll(past ? position : null);
                    return this.drag(position) || past;
                }
            case 'up':
                {
                    let wasSelecting = this.selecting;
                    this.stopDrag();
                    if (this._entryPressed) {
                        this._entryPressed = false;
                        this.useCellView();
                        this.entry.handlePointer('up', position, modifiers, clicks);
                        return true;
                    }
                    let tapped: any; 
                    if ((tapped = this._tapped) != null) {
                        this._tapped = null;
                        if (this.inEditor(position)) {
                            this.editMode = 'edit';
                            this.useCellView();
                            this.entry.selection = new CodeRange(this.entry.positionAt(position));
                            return true;
                        }
                        if (this.editing) this.commitEdit();
                        this.press(tapped, position, modifiers, clicks);
                        return true;
                    }
                    if (!this.filling) return wasSelecting;
                    this.commitFill();
                    return true;
                }
            case 'cancel':
                {
                    this.stopDrag();
                    this._tapped = null;
                    if (this._entryPressed) {
                        this._entryPressed = false;
                        this.entry.handlePointer('cancel', position, modifiers, clicks);
                        return true;
                    }
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

    press(cell: CellRef, position: Point, modifiers: number, clicks: number) {
        if ((modifiers & 1) !== 0) this.selectTo(cell); else this.select(new SheetRange(cell));
        if (clicks < 2) return;
        this.beginEdit(this.document.getCell(cell), 'edit');
        this.useCellView();
        this.entry.selection = new CodeRange(this.entry.positionAt(position));
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

