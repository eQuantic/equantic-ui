import { $eq, CellRef, CodeCommandValue, CodeDocument, CodeEditorController, CodeGrid, CodeKeymap, CodeRange, DateTime, KeyboardConventionValue, Point, PointerKindValue, PointerPhaseValue, Rect, SheetAxisValue, SheetCellSnapshot, SheetDocument, SheetEdit, SheetEditModeValue, SheetEntryLanguage, SheetEntryViewValue, SheetFormulaBar, SheetGrid, SheetHistory, SheetKeymap, SheetMotionValue, SheetNameBox, SheetRange, SheetReference, SheetSummary, Size, TsvCodec } from "../runtime-exports";

export class SheetController {
    constructor(rows: number = 1000, cols: number = 26) {
        this._selection = new SheetRange(new CellRef(0, 0));
        this._active = new CellRef(0, 0);
        this._others = [];
        this.formulaBar = null!;
        this.nameBox = null!;
        this.document = null!;
        this.history = new SheetHistory();
        this.changed = null;
        this.refreshed = null;
        this.addingRanges = false;
        this.revealVersion = 0;
        this.revealCell = CellRef.$zero();
        this._tabRun = null;
        this._summaryRanges = [];
        this._summaryAt = -1;
        this._summary = SheetSummary.$zero();
        this._lastInUseAt = -1;
        this._lastInUse = CellRef.$zero();
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
        this._tappedHeader = null;
        this._headerDrag = null;
        this._entryPressed = false;
        this._slide = 0;
        this._rise = 0;
        this._dragAt = null;
        this._widestOf = null;
        this._widestBy = null;
        this._widest = 0;
        this._focusVersion = 0;
        this.hasKeyboard = false;
        this.tabReleased = false;
        this._entryKeysFor = null;
        this.document = new SheetDocument(rows, cols);
        this.entry = new CodeEditorController('', new SheetEntryLanguage());
        this.formulaBar = new SheetFormulaBar(this);
        this.nameBox = new SheetNameBox(this);
    }

    _selection!: SheetRange;
    _active!: CellRef;
    _others!: SheetRange[];
    formulaBar!: SheetFormulaBar;
    nameBox!: SheetNameBox;
    document!: SheetDocument;
    history!: SheetHistory;
    changed!: ((sheetEdit: SheetEdit) => void) | null;
    refreshed!: (() => void) | null;
    addingRanges!: boolean;
    revealVersion!: number;
    revealCell!: CellRef;
    _tabRun!: [number, CellRef] | null;
    _summaryRanges!: SheetRange[];
    _summaryAt!: number;
    _summary!: SheetSummary;
    _lastInUseAt!: number;
    _lastInUse!: CellRef;
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
    _tappedHeader!: [SheetAxisValue | null, number, boolean] | null;
    _headerDrag!: SheetAxisValue | null;
    _entryPressed!: boolean;
    _slide!: number;
    _rise!: number;
    _dragAt!: Point | null;
    _widestOf!: CodeDocument | null;
    _widestBy!: ((value: string) => number) | null;
    _widest!: number;
    _focusVersion!: number;
    hasKeyboard!: boolean;
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

    get ranges(): SheetRange[] {
        let ranges: SheetRange[] = ($eq.collections.listCapacity(this._others.length + 1), []);
        ranges.push(...this._others);
        ranges.push(this._selection);
        return ranges;
    }

    get activeCell(): CellRef {
        return this._active;
    }

    get summary(): SheetSummary {
        let ranges = this.ranges;
        if (this._summaryAt === this.document.version && this.sameRanges(ranges)) return this._summary;
        let count = 0;
        let numbers = 0;
        let sum = $eq.num.dec("0");
        for (const [key, value] of this.document.cells) {
            let cell = CellRef.fromKey(key);
            let covered = false;
            for (const range of ranges) {
                if (!range.contains(cell)) continue;
                covered = true;
                break;
            }
            if (!covered) continue;
            count++;
            let number: any; 
            if (!((number = $eq.num.decTryParse(value, 167)) !== undefined || ((number = $eq.num.dec(0)), false))) continue;
            numbers++;
            sum = sum.add(number);
        }
        this._summary = new SheetSummary(count, numbers, sum, numbers > 0 ? sum.div($eq.num.dec(numbers)) : null);
        this._summaryAt = this.document.version;
        this._summaryRanges.splice(0);
        this._summaryRanges.push(...ranges);
        return this._summary;
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
        let shown = bounds.intersect(this.body());
        let left = Math.max(Math.fround(bounds.x + padding.start), shown.x);
        let right = Math.fround(Math.min(Math.fround(bounds.right - padding.end), shown.right) - 2);
        let top = Math.max(Math.fround(bounds.y + padding.top), shown.y);
        let bottom = Math.min(Math.fround(bounds.bottom - padding.bottom), shown.bottom);
        this.entry.grid = this.cellGrid(bounds, this._slide, this._rise);
        let caret = this.entry.caretRect(this.entry.caret);
        let slide = this._slide;
        if (caret.x > right) slide = Math.fround(slide + Math.fround(caret.x - right)); else if (caret.x < left) slide = Math.fround(slide - Math.fround(left - caret.x));
        let rise = this._rise;
        if (caret.bottom > bottom) rise = Math.fround(rise + Math.fround(caret.bottom - bottom)); else if (caret.y < top) rise = Math.fround(rise - Math.fround(top - caret.y));
        slide = Math.min(slide, Math.fround(Math.fround(caret.x + this._slide) - left));
        rise = Math.min(rise, Math.fround(Math.fround(caret.y + this._rise) - top));
        slide = Math.min(Math.max(slide, 0), Math.max(0, Math.fround(this.widestEntryLine() - Math.fround(right - left))));
        rise = Math.min(Math.max(rise, 0), Math.max(0, Math.fround(Math.fround(Math.fround(this.entry.document.lineCount) * this.lineHeight) - Math.fround(bottom - top))));
        if (slide !== this._slide || rise !== this._rise) {
            this._slide = slide;
            this._rise = rise;
            this.entry.grid = this.cellGrid(bounds, slide, rise);
        }
        return this.entry.grid;
    }

    get carets(): Rect[] {
        if (!this.editing || this.entryView !== 'cell') return [];
        this.cellEditorGrid;
        let shown = this.editorBounds.intersect(this.body());
        let carets: Rect[] = [];
        for (const caret of this.entry.carets) {
            let clipped = caret.intersect(shown);
            if (clipped.width > 0 && clipped.height > 0) carets.push(clipped);
        }
        return carets;
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
        this._others.splice(0);
        this._tabRun = null;
        this.setActiveRange(range);
    }

    setActiveRange(range: SheetRange) {
        this._selection = new SheetRange(this.document.clamp(range.anchor), this.document.clamp(range.focus));
        this._active = this._selection.anchor;
    }

    keepActiveInRange() {
        return this._active = this._selection.contains(this._active) ? this._active : this._selection.anchor;
    }

    addRange(range: SheetRange) {
        this._others.push(this._selection);
        this.setActiveRange(range);
    }

    move(dRow: number, dCol: number, motion: SheetMotionValue = 'cell', extend: boolean = false) {
        let from = extend ? this._selection.focus : this.activeCell;
        let landed = (() => { const $s = motion; if ($s === 'dataEdge') return this.dataEdge(from, dRow, dCol); if ($s === 'rowBoundary') return new CellRef(from.row, dCol < 0 ? 0 : this.document.cols - 1); if ($s === 'sheetBoundary') return dRow < 0 || dCol < 0 ? new CellRef(0, 0) : this.lastCellInUse(); return new CellRef(from.row + dRow, from.col + dCol); })();
        landed = this.document.clamp(landed);
        this.land(landed, extend);
        this._tabRun = null;
        this.reveal();
    }

    land(landed: CellRef, extend: boolean) {
        if (!extend) {
            this._others.splice(0);
            this._selection = new SheetRange(landed);
            this._active = landed;
            return;
        }
        this._selection = new SheetRange(this._selection.anchor, landed);
        this.keepActiveInRange();
    }

    movePage(direction: number, extend: boolean = false) {
        let scroll: any; 
        if (!((scroll = this.grid.scroll) != null)) return false;
        let at = extend ? this._selection.focus : this._active;
        let page = Math.max(this.document.rowSpans.extentOf(at.row), Math.fround(scroll.viewport.height - this.grid.headerHeight));
        let from = this.document.rowSpans.offsetOf(at.row);
        let row = this.document.rowSpans.indexAt(from + Math.sign(direction) * page);
        this.land(this.document.clamp(new CellRef(row, at.col)), extend);
        this.reveal();
        return true;
    }

    reveal() {
        this.revealVersion++;
        this.revealCell = this._active;
    }

    moveTo(cell: CellRef) {
        let landed = this.document.clamp(cell);
        this._others.splice(0);
        this._selection = new SheetRange(landed);
        this._active = landed;
        this._tabRun = null;
        this.reveal();
    }

    showActiveCell() {
        return this.reveal();
    }

    nextCorner() {
        let range = this._selection;
        let corners = [new CellRef(range.topRow, range.leftCol), new CellRef(range.topRow, range.rightCol), new CellRef(range.bottomRow, range.rightCol), new CellRef(range.bottomRow, range.leftCol)];
        let at = $eq.collections.arrayIndexOf(corners, this._active, true);
        let next = corners[0];
        if (at >= 0) {
            for (let step = 1; step <= 4; step++) {
                next = corners[(at + step) % 4];
                if (!$eq.equals(next, this._active)) break;
            }
        }
        this._active = next;
        this.reveal();
    }

    movePageAcross(direction: number, extend: boolean = false) {
        let scroll: any; 
        if (!((scroll = this.grid.scroll) != null)) return false;
        let at = extend ? this._selection.focus : this._active;
        let page = Math.max(this.document.columnSpans.extentOf(at.col), Math.fround(scroll.viewport.width - this.grid.headerWidth));
        let from = this.document.columnSpans.offsetOf(at.col);
        let col = this.document.columnSpans.indexAt(from + Math.sign(direction) * page);
        this.land(this.document.clamp(new CellRef(at.row, col)), extend);
        this._tabRun = null;
        this.reveal();
        return true;
    }

    selectActiveRows() {
        let active = this._active;
        this.setActiveRange(this.rowBand(this._selection.anchor.row, this._selection.focus.row));
        this._active = active;
    }

    selectActiveColumns() {
        let active = this._active;
        this.setActiveRange(this.columnBand(this._selection.anchor.col, this._selection.focus.col));
        this._active = active;
    }

    selectRegionThenSheet() {
        let region = this.currentRegion(this._active);
        if ($eq.equals(region, this._selection) || region.isSingleCell && !this.document.hasValue(this._active)) {
            this.selectAll();
            return;
        }
        let active = this._active;
        this.select(region);
        this._active = active;
    }

    currentRegion(cell: CellRef) {
        let top = cell.row;
        let bottom = cell.row;
        let left = cell.col;
        let right = cell.col;
        let grew = null;
        do {
            grew = false;
            while (top > 0 && this.anyInUse(top - 1, left - 1, top - 1, right + 1)) {
                top--;
                grew = true;
            }
            while (bottom < this.document.rows - 1 && this.anyInUse(bottom + 1, left - 1, bottom + 1, right + 1)) {
                bottom++;
                grew = true;
            }
            while (left > 0 && this.anyInUse(top - 1, left - 1, bottom + 1, left - 1)) {
                left--;
                grew = true;
            }
            while (right < this.document.cols - 1 && this.anyInUse(top - 1, right + 1, bottom + 1, right + 1)) {
                right++;
                grew = true;
            }
        } while (grew);
        return new SheetRange(new CellRef(top, left), new CellRef(bottom, right));
    }

    anyInUse(top: number, left: number, bottom: number, right: number) {
        top = Math.max(0, top);
        left = Math.max(0, left);
        bottom = Math.min(this.document.rows - 1, bottom);
        right = Math.min(this.document.cols - 1, right);
        for (let row = top; row <= bottom; row++) for (let col = left; col <= right; col++) if (this.document.hasValue(new CellRef(row, col))) return true;
        return false;
    }

    sameRanges(ranges: SheetRange[]) {
        if ($eq.collections.count(ranges) !== this._summaryRanges.length) return false;
        for (let i = 0; i < $eq.collections.count(ranges); i++) if (!$eq.equals($eq.collections.item(ranges, i), this._summaryRanges[i])) return false;
        return true;
    }

    lastCellInUse() {
        if (this._lastInUseAt === this.document.version) return this._lastInUse;
        let row = 0;
        let col = 0;
        for (const key of this.document.cells.keys()) {
            let cell = CellRef.fromKey(key);
            row = Math.max(row, cell.row);
            col = Math.max(col, cell.col);
        }
        this._lastInUse = new CellRef(row, col);
        this._lastInUseAt = this.document.version;
        return this._lastInUse;
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
            if (dRow === 0 && dCol > 0) {
                let run: any; 
                let start = (run = this._tabRun) != null && $eq.equals(run[1], this._active) ? run[0] : this._active.col;
                this.move(0, dCol);
                this._tabRun = [start, this._active];
                return;
            }
            let ended: any; 
            if (dRow > 0 && dCol === 0 && (ended = this._tabRun) != null && $eq.equals(ended[1], this._active)) {
                this.moveTo(new CellRef(this._active.row + dRow, ended[0]));
                return;
            }
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

    selectRows(anchor: number, focus: number, add: boolean) {
        let band = this.rowBand(anchor, focus);
        if (add) this.addRange(band); else this.select(band);
        this._active = this.document.clamp(new CellRef(anchor, this.firstColumnShown()));
    }

    selectCols(anchor: number, focus: number, add: boolean) {
        let band = this.columnBand(anchor, focus);
        if (add) this.addRange(band); else this.select(band);
        this._active = this.document.clamp(new CellRef(this.firstRowShown(), anchor));
    }

    rowBand(anchor: number, focus: number) {
        return new SheetRange(new CellRef(anchor, 0), new CellRef(focus, this.document.cols - 1));
    }

    columnBand(anchor: number, focus: number) {
        return new SheetRange(new CellRef(0, anchor), new CellRef(this.document.rows - 1, focus));
    }

    firstRowShown() {
        return this.document.rowSpans.indexAt(this.grid.offset.down);
    }

    firstColumnShown() {
        return this.document.columnSpans.indexAt(this.grid.offset.across);
    }

    selectAll() {
        this._others.splice(0);
        this._selection = new SheetRange(new CellRef(0, 0), new CellRef(this.document.rows - 1, this.document.cols - 1));
        this._active = new CellRef(0, 0);
    }

    selectTo(cell: CellRef) {
        return this._selection = new SheetRange(this._selection.anchor, this.document.clamp(cell));
    }

    adds(modifiers: number) {
        return this.addingRanges || (modifiers & 4) !== 0;
    }

    beginEdit(seed: string, mode: SheetEditModeValue) {
        this.editing = true;
        this.editMode = mode;
        this.entryView = 'cell';
        this._slide = 0;
        this._rise = 0;
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
        this._others.splice(0);
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
        return this.fillRanges('');
    }

    fillSelection(value: string) {
        return this.fillRanges(value);
    }

    fillRanges(value: string) {
        let $n52: any; 
        let edit = ($n52 = new SheetEdit(), $n52.kind = 'setCells', $n52.selectionBefore = this._selection, $n52.selectionAfter = this._selection, $n52);
        for (const range of this.ranges) {
            for (let row = range.topRow; row <= range.bottomRow; row++) {
                for (let col = range.leftCol; col <= range.rightCol; col++) {
                    let cell = new CellRef(row, col);
                    let old = this.document.getCell(cell);
                    if (old === value) continue;
                    edit.before.push(new SheetCellSnapshot(cell, old));
                    edit.after.push(new SheetCellSnapshot(cell, value));
                    this.document.setCell(cell, value);
                }
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
        let $n53: any; 
        this.commit(($n53 = new SheetEdit(), $n53.kind = axis === 'rows' ? 'insertRows' : 'insertCols', $n53.at = at, $n53.count = count, $n53.selectionBefore = selectionBefore, $n53.selectionAfter = this._selection, $n53));
    }

    delete(axis: SheetAxisValue, at: number, count: number = 1) {
        if (count < 1) return;
        let selectionBefore = this._selection;
        let removed = axis === 'rows' ? this.document.shiftRows(at, -count) : this.document.shiftCols(at, -count);
        for (let i = 0; i < this._others.length; i++) this._others[i] = new SheetRange(this.document.clamp(this._others[i].anchor), this.document.clamp(this._others[i].focus));
        this.setActiveRange(this._selection);
        let $n54: any; 
        this.commit(($n54 = new SheetEdit(), $n54.kind = axis === 'rows' ? 'deleteRows' : 'deleteCols', $n54.at = at, $n54.count = count, $n54.removed = removed, $n54.selectionBefore = selectionBefore, $n54.selectionAfter = this._selection, $n54));
    }

    resize(axis: SheetAxisValue, index: number, size: number) {
        let old = axis === 'rows' ? this.document.rowHeight(index) : this.document.colWidth(index);
        if (Math.abs(Math.fround(old - size)) < Math.fround(0.01)) return;
        if (axis === 'rows') this.document.setRowHeight(index, size); else this.document.setColWidth(index, size);
        let $n55: any; 
        this.commit(($n55 = new SheetEdit(), $n55.kind = axis === 'rows' ? 'resizeRow' : 'resizeCol', $n55.at = index, $n55.oldSize = old, $n55.newSize = axis === 'rows' ? this.document.rowHeight(index) : this.document.colWidth(index), $n55.selectionBefore = this._selection, $n55.selectionAfter = this._selection, $n55));
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
        let $n56: any; 
        let edit = ($n56 = new SheetEdit(), $n56.kind = 'setCells', $n56.selectionBefore = this._selection, $n56);
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
        this._others.splice(0);
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
        this._others.splice(0);
        this._selection = new SheetRange(new CellRef(Math.min($eq.nullable.value(source).topRow, $eq.nullable.value(target).topRow), Math.min($eq.nullable.value(source).leftCol, $eq.nullable.value(target).leftCol)), new CellRef(Math.max($eq.nullable.value(source).bottomRow, $eq.nullable.value(target).bottomRow), Math.max($eq.nullable.value(source).rightCol, $eq.nullable.value(target).rightCol)));
        this._active = $eq.nullable.value(source).topLeft;
        return this.fill($eq.nullable.value(source), $eq.nullable.value(target), selectionBefore);
    }

    cancelFill() {
        this.fillSource = null;
        this.fillTarget = null;
    }

    fill(source: SheetRange, target: SheetRange, selectionBefore: SheetRange | null = null) {
        let $n57: any; 
        let edit = ($n57 = new SheetEdit(), $n57.kind = 'setCells', $n57.selectionBefore = selectionBefore ?? this._selection, $n57);
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

    cellGrid(bounds: Rect, slide: number, rise: number) {
        let padding = this.grid.cellPadding;
        let origin = new Point(Math.fround(Math.fround(bounds.x + padding.start) - slide), Math.fround(Math.fround(bounds.y + padding.top) - rise));
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

    body() {
        let scroll: any; 
        return (scroll = this.grid.scroll) != null && scroll.viewport.width > 0 && scroll.viewport.height > 0 ? Rect.fromLTRB(this.grid.headerWidth, this.grid.headerHeight, scroll.viewport.width, scroll.viewport.height) : this.editorBounds;
    }

    requestFocus() {
        return this._focusVersion++;
    }

    focusChanged(focused: boolean) {
        this.hasKeyboard = focused;
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
                    this._headerDrag = null;
                    this._tapped = null;
                    this._tappedHeader = null;
                    this._entryPressed = false;
                    if (this.filling) this.cancelFill();
                    let header = this.headerAt(position);
                    if (header[3]) return false;
                    let onHeader = header[2] || !(header[0] == null);
                    if (kind === 'touch') {
                        if (onHeader) this._tappedHeader = [header[0], header[1], header[2]]; else this._tapped = this.cellAt(position);
                        return false;
                    }
                    if (onHeader) {
                        if (this.editing) this.commitEdit();
                        this.pressHeader(header[0], header[1], header[2], modifiers);
                        this._selecting = !header[2];
                        this._headerDrag = header[0];
                        if (this._selecting) this._dragAt = position;
                        return true;
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
                    position = this.alongTheHeaders(position);
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
                    let tappedHeader: any; 
                    if ((tappedHeader = this._tappedHeader) != null) {
                        this._tappedHeader = null;
                        if (this.editing) this.commitEdit();
                        this.pressHeader(tappedHeader[0], tappedHeader[1], tappedHeader[2], modifiers);
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
                    this._tappedHeader = null;
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
        if ((modifiers & 1) !== 0) this.selectTo(cell); else if (this.adds(modifiers)) this.addRange(new SheetRange(cell)); else this.select(new SheetRange(cell));
        if (clicks < 2) return;
        this.beginEdit(this.document.getCell(cell), 'edit');
        this.useCellView();
        this.entry.selection = new CodeRange(this.entry.positionAt(position));
    }

    drag(position: Point) {
        let cell = this.cellAt(this.insideTheCells(position));
        let axis: any; 
        if ((axis = this._headerDrag) != null) {
            let active = this._active;
            let band = axis === 'cols' ? this.columnBand(this._selection.anchor.col, cell.col) : this.rowBand(this._selection.anchor.row, cell.row);
            if ($eq.equals(band, this._selection)) return false;
            this.setActiveRange(band);
            this._active = active;
            return true;
        }
        if (this.filling) {
            let before = this.fillTarget;
            this.updateFill(cell);
            return !$eq.equals(this.fillTarget, before);
        }
        if ($eq.equals(cell, this._selection.focus)) return false;
        this.setActiveRange(new SheetRange(this._selection.anchor, cell));
        return true;
    }

    headerAt(point: Point): [SheetAxisValue | null, number, boolean, boolean] {
        let overColumns = point.y >= 0 && point.y < this.grid.headerHeight;
        let overRows = point.x >= 0 && point.x < this.grid.headerWidth;
        if (!overColumns && !overRows) return [null, 0, false, false];
        if (overColumns && overRows) return [null, 0, true, false];
        let offset = this.grid.offset;
        if (overColumns) {
            let col = this.document.columnSpans.indexAt(Math.max(0, Math.fround(point.x - this.grid.headerWidth)) + offset.across);
            col = Math.min(Math.max(col, 0), this.document.cols - 1);
            let right = this.grid.headerWidth + this.document.columnSpans.offsetOf(col + 1) - offset.across;
            return ['cols', col, false, this.grid.resizeGrip > 0 && point.x >= right - this.grid.resizeGrip];
        }
        let row = this.document.rowSpans.indexAt(Math.max(0, Math.fround(point.y - this.grid.headerHeight)) + offset.down);
        row = Math.min(Math.max(row, 0), this.document.rows - 1);
        let bottom = this.grid.headerHeight + this.document.rowSpans.offsetOf(row + 1) - offset.down;
        return ['rows', row, false, this.grid.resizeGrip > 0 && point.y >= bottom - this.grid.resizeGrip];
    }

    pressHeader(axis: SheetAxisValue | null, index: number, corner: boolean, modifiers: number) {
        let along: any; 
        if (corner || !((along = axis) != null)) {
            this.selectAll();
            return;
        }
        if ((modifiers & 1) !== 0) {
            let active = this._active;
            this.setActiveRange(along === 'cols' ? this.columnBand(this._selection.anchor.col, index) : this.rowBand(this._selection.anchor.row, index));
            this._active = this._selection.contains(active) ? active : this._selection.anchor;
            return;
        }
        if (along === 'cols') this.selectCols(index, index, this.adds(modifiers)); else this.selectRows(index, index, this.adds(modifiers));
    }

    alongTheHeaders(position: Point) {
        return (() => { const $s = this._headerDrag; if ($s === 'cols') return new Point(position.x, Math.max(position.y, this.grid.headerHeight)); if ($s === 'rows') return new Point(Math.max(position.x, this.grid.headerWidth), position.y); return position; })();
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
        this._headerDrag = null;
        if (this._dragAt == null) return;
        this._dragAt = null;
        this.grid.scroll?.autoScroll(null);
    }

    undo() {
        let edit = this.history.popUndo();
        if (edit == null) return false;
        this.apply(edit, false);
        this._others.splice(0);
        this._selection = edit.selectionBefore;
        this._active = this._selection.anchor;
        this.reveal();
        this.changed?.(edit);
        return true;
    }

    redo() {
        let edit = this.history.popRedo();
        if (edit == null) return false;
        this.apply(edit, true);
        this._others.splice(0);
        this._selection = edit.selectionAfter;
        this._active = this._selection.anchor;
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

