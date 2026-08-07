import { Box, BoxStyle, BuildContext, CellRef, Column, EdgeInsets, Flexible, Row, ScrollView, SharedStatefulComponent, SheetController, SheetDocument, SheetSurface, SizeValue, Spacer, Text, UiComponent } from "../runtime-exports";

export class Spreadsheet extends SharedStatefulComponent {
    _offset: number = 0;
    _viewport: number = 0;
    _first: number = 0;
    _last: number = -1;
    static headerWidth: number = 44;
    static headerHeight: number = 26;
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
        let theme = context.theme;let document = this.controller.document;let topOfWindow = null;[this._first, this._last, topOfWindow] = this.windowFor(this._offset, this._viewport > 0 ? this._viewport : Spreadsheet.headerHeight * 14);let headerRow = new Row(0, { width: SizeValue.fill });headerRow.add(Spreadsheet.headerCell('', Spreadsheet.headerWidth, Spreadsheet.headerHeight, theme));for (let c = 0; c < document.cols; c++) headerRow.add(Spreadsheet.headerCell(Spreadsheet.columnName(c), document.colWidth(c), Spreadsheet.headerHeight, theme));let windowRows = new Row(0);let rowHeaders = new Column(0);let grid = new Column(0);for (let r = this._first; r <= this._last; r++) {rowHeaders.add(Spreadsheet.headerCell(`${r + 1}`, Spreadsheet.headerWidth, document.rowHeight(r), theme));grid.add(this.gridRow(r, document, theme));}windowRows.add(rowHeaders);windowRows.add(new SheetSurface(grid, this.controller, { firstRow: this._first, onChanged: () => this.setState(() => {}), label: 'Spreadsheet' }));let content = new Column(0, { width: SizeValue.fill });if (topOfWindow > 0) content.add(Spacer.fixed(topOfWindow));content.add(windowRows);let below = 0;for (let r = this._last + 1; r < document.rows; r++) below += document.rowHeight(r);if (below > 0) content.add(Spacer.fixed(below));let frame = new Column(0, { width: this.width, height: this.height });frame.add(headerRow);frame.add(new Flexible(new ScrollView(content, 'vertical', { onScrolled: this.onScrolled.bind(this), onViewportChanged: this.onViewportChanged.bind(this) }), 1));return new Box(new BoxStyle({ width: this.width, height: this.height, borderColor: theme.border, borderWidth: 1, clip: true }), frame);
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

    static headerCell(label: string, width: number, height: number, theme: any) {
        return new Box(new BoxStyle({ width: SizeValue.fixed(width), height: SizeValue.fixed(height), background: theme.surfaceSubtle, borderColor: theme.border, borderWidth: 0.5 }), new Text(label, 'caption', theme.textMuted, 1).centered());
    }

    gridRow(row: number, document: SheetDocument, theme: any) {
        let line = new Row(0);for (let c = 0; c < document.cols; c++) {let value = document.getCell(new CellRef(row, c));line.add(new Box(new BoxStyle({ width: SizeValue.fixed(document.colWidth(c)), height: SizeValue.fixed(document.rowHeight(row)), background: theme.surface, borderColor: theme.border, borderWidth: 0.5, padding: new EdgeInsets(0, 6, 0, 6) }), value.length > 0 ? new Text(value, 'bodyM', theme.textPrimary, 1) : null));}return line;
    }

}

