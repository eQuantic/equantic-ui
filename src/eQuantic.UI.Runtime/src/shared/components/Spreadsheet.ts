import { $eq, Box, BoxStyle, BuildContext, CellRef, Column, Draggable, EdgeInsets, Flexible, Positioned, Pressable, Row, ScrollView, SharedStatefulComponent, SheetController, SheetDocument, SheetSurface, SizeValue, Spacer, Stack, Text, UiComponent } from "../runtime-exports";

export class Spreadsheet extends SharedStatefulComponent {
    _offset: number = 0;
    _viewport: number = 0;
    _first: number = 0;
    _last: number = -1;
    static headerWidth: number = 44;
    static headerHeight: number = 26;
    static minColWidth: number = 24;
    static minRowHeight: number = 14;
    static grip: number = 6;
    static fillHandle: number = 7;
    _resizeBase: number = -1;
    declare controller: SheetController;
    declare width: SizeValue;
    declare height: SizeValue;
    declare overscan: number;
    constructor(controller?: any, props?: any) {
        super();
        if (controller !== undefined) this.controller = controller;
        if (this.overscan === undefined) this.overscan = 3;
        this.controller = controller;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let document = this.controller.document;let topOfWindow = null;[this._first, this._last, topOfWindow] = this.windowFor(this._offset, this._viewport > 0 ? this._viewport : Spreadsheet.headerHeight * 14);let headerRow = new Row(0, { width: SizeValue.fill });headerRow.add(Spreadsheet.headerCell('', Spreadsheet.headerWidth, Spreadsheet.headerHeight, theme, false, () => this.setState(this.controller.selectAll.bind(this.controller))));for (let c = 0; c < document.cols; c++) headerRow.add(this.columnHeader(c, document, theme));let windowRows = new Row(0);let rowHeaders = new Column(0);let grid = new Column(0);for (let r = this._first; r <= this._last; r++) {rowHeaders.add(this.rowHeader(r, document, theme));grid.add(this.gridRow(r, document, theme));}windowRows.add(rowHeaders);windowRows.add(new SheetSurface(grid, this.controller, { firstRow: this._first, onChanged: () => this.setState(() => {}), label: 'Spreadsheet' }));let content = new Column(0, { width: SizeValue.fill });if (topOfWindow > 0) content.add(Spacer.fixed(topOfWindow));content.add(windowRows);let below = 0;for (let r = this._last + 1; r < document.rows; r++) below += document.rowHeight(r);if (below > 0) content.add(Spacer.fixed(below));let frame = new Column(0, { width: this.width, height: this.height });frame.add(headerRow);frame.add(new Flexible(new ScrollView(content, 'vertical', { onScrolled: this.onScrolled.bind(this), onViewportChanged: this.onViewportChanged.bind(this) }), 1));return new Box(new BoxStyle({ width: this.width, height: this.height, borderColor: theme.border, borderWidth: 1, clip: true }), frame);
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; if ((next instanceof Spreadsheet && (fresh = next, true))) this.controller = fresh.controller;
    }

    static columnName(col: number) {
        let name = '';while (true) {name = String.fromCharCode((65 + col % 26)) + name;col = Math.trunc(col / 26) - 1;if (col < 0) break;}return name;
    }

    windowFor(offset: number, viewport: number) {
        let document = this.controller.document;let row = 0;let top = 0;while (row < document.rows - 1 && top + document.rowHeight(row) <= offset) {top += document.rowHeight(row);row++;}let first = Math.max(0, row - this.overscan);let firstTop = top;for (let r = row - 1; r >= first; r--) firstTop -= document.rowHeight(r);let last = row;let covered = top - offset;while (last < document.rows - 1 && covered < viewport) {covered += document.rowHeight(last);last++;}last = Math.min(document.rows - 1, last + this.overscan);return [first, last, firstTop];
    }

    onScrolled(offset: number) {
        this._offset = offset;let [first, last, ] = this.windowFor(offset, this._viewport > 0 ? this._viewport : Spreadsheet.headerHeight * 14);if (first === this._first && last === this._last) return;this.setState(() => {});
    }

    onViewportChanged(viewport: number) {
        if (Math.abs(viewport - this._viewport) < 0.5) return;this.setState(() => this._viewport = viewport);
    }

    static headerCell(label: string, width: number, height: number, theme: any, selected: boolean = false, onPressed: (() => void) | null = null) {
        let cell = new Box(new BoxStyle({ width: SizeValue.fixed(width), height: SizeValue.fixed(height), background: selected ? theme.surfaceHighlight : theme.surfaceSubtle, borderColor: theme.border, borderWidth: 0.5 }), new Text(label, 'caption', selected ? theme.textPrimary : theme.textMuted, 1).centered());return onPressed == null ? cell : new Pressable(cell, onPressed);
    }

    columnHeader(c: number, document: SheetDocument, theme: any) {
        let col = c;let selection = this.controller.selection;let inBand = c >= selection.leftCol && c <= selection.rightCol;let stack = new Stack();stack.add(Spreadsheet.headerCell(Spreadsheet.columnName(c), document.colWidth(c), Spreadsheet.headerHeight, theme, inBand, () => this.setState(() => this.controller.selectCols(col, col))));stack.add(new Positioned(new Draggable(new Box(new BoxStyle({ width: SizeValue.fixed(Spreadsheet.grip), height: SizeValue.fixed(Spreadsheet.headerHeight), cursor: 'colResize' })), null, { axis: 'horizontal', min: -4000, max: 4000, follows: false, onMoved: (delta: number) => this.previewResize('cols', col, delta), onReleased: (delta: number) => this.commitResize('cols', col, delta) }), 0, 0, null, null, { zIndex: 1 }));return stack;
    }

    rowHeader(r: number, document: SheetDocument, theme: any) {
        let row = r;let selection = this.controller.selection;let inBand = r >= selection.topRow && r <= selection.bottomRow;let stack = new Stack();stack.add(Spreadsheet.headerCell(`${r + 1}`, Spreadsheet.headerWidth, document.rowHeight(r), theme, inBand, () => this.setState(() => this.controller.selectRows(row, row))));stack.add(new Positioned(new Draggable(new Box(new BoxStyle({ width: SizeValue.fixed(Spreadsheet.headerWidth), height: SizeValue.fixed(Spreadsheet.grip), cursor: 'rowResize' })), null, { axis: 'vertical', min: -4000, max: 4000, follows: false, onMoved: (delta: number) => this.previewResize('rows', row, delta), onReleased: (delta: number) => this.commitResize('rows', row, delta) }), null, null, 0, 0, { zIndex: 1 }));return stack;
    }

    previewResize(axis: string, index: number, delta: number) {
        let document = this.controller.document;if (this._resizeBase < 0) this._resizeBase = axis === 'cols' ? document.colWidth(index) : document.rowHeight(index);let floor = axis === 'cols' ? Spreadsheet.minColWidth : Spreadsheet.minRowHeight;let size = Math.max(floor, this._resizeBase + delta);this.setState(() => {if (axis === 'cols') document.setColWidth(index, size); else document.setRowHeight(index, size);});
    }

    commitResize(axis: string, index: number, delta: number) {
        if (this._resizeBase < 0) return;let document = this.controller.document;let floor = axis === 'cols' ? Spreadsheet.minColWidth : Spreadsheet.minRowHeight;let final = Math.max(floor, this._resizeBase + delta);if (axis === 'cols') document.setColWidth(index, this._resizeBase); else document.setRowHeight(index, this._resizeBase);this._resizeBase = -1;this.controller.resize(axis, index, final);this.setState(() => {});
    }

    gridRow(row: number, document: SheetDocument, theme: any) {
        let selection = this.controller.selection;let active = this.controller.activeCell;let fillTarget = this.controller.fillTarget;let line = new Row(0);for (let c = 0; c < document.cols; c++) {let cell = new CellRef(row, c);let isActive = $eq.equals(cell, active);let selected = selection.contains(cell) && !selection.isSingleCell;let preview: any; let inFillPreview = ((fillTarget != null) && (preview = fillTarget, true)) && preview.contains(cell);let editingHere = isActive && this.controller.editing;let value = editingHere ? this.controller.draft : document.getCell(cell);let content = null;if (editingHere) {let draftLine = new Row(0, { cross: 'center' });if (value.length > 0) draftLine.add(new Text(value, 'bodyM', theme.textPrimary, 1));draftLine.add(new Box(new BoxStyle({ width: SizeValue.fixed(2), height: SizeValue.fixed(document.rowHeight(row) - 8), background: theme.textPrimary })));content = draftLine;} else if (value.length > 0) {content = new Text(value, 'bodyM', theme.textPrimary, 1);}let box = new Box(new BoxStyle({ width: SizeValue.fixed(document.colWidth(c)), height: SizeValue.fixed(document.rowHeight(row)), background: selected ? theme.surfaceHighlight : theme.surface, borderColor: isActive || inFillPreview ? theme.focusRing : theme.border, borderWidth: isActive ? 2 : inFillPreview ? 1.5 : 0.5, padding: new EdgeInsets(0, 6, 0, 6) }), content);if (!editingHere && row === selection.bottomRow && c === selection.rightCol) {let handled = new Stack();handled.add(box);handled.add(new Positioned(new Box(new BoxStyle({ width: SizeValue.fixed(Spreadsheet.fillHandle), height: SizeValue.fixed(Spreadsheet.fillHandle), background: theme.focusRing, borderColor: theme.surface, borderWidth: 1, cursor: 'crosshair' })), null, -1, -1, null, { zIndex: 2 }));line.add(handled);continue;}line.add(box);}return line;
    }

}

