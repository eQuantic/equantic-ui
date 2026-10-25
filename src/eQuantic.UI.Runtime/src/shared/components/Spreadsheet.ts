import { $eq, Box, BoxStyle, BuildContext, CellRef, Column, Draggable, EdgeInsets, EntryLines, LeadingSpans, Point, Positioned, Pressable, SdkStrings, SheetAxisValue, SheetController, SheetDocument, SheetGrid, SheetPalette, SheetRange, SheetSurface, Size, SizeValue, Stack, StatefulComponent, TableOffset, TableRange, TableScroll, TableScrollRequest, TableView, TableViewCell, Text, TypeRoleValue, UiComponent, VisualNode, VisualNodeExtensions } from "../runtime-exports";

export class Spreadsheet extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Spreadsheets.Spreadsheet';
    static firstGuess: number = 768;
    _scroll: TableScroll = new TableScroll();
    _rows: any = null;
    _columns: any = null;
    _document: any = null;
    _digits: number = 1;
    _config: Spreadsheet;
    _rebuild: () => void;
    _listening: any = null;
    static headerWidth: number = 44;
    static headerHeight: number = 26;
    static numberPadding: number = 8;
    static cellType: TypeRoleValue = 'bodyM';
    static minColWidth: number = 24;
    static minRowHeight: number = 14;
    static grip: number = 6;
    static fillHandle: number = 7;
    _resizeBase: number = -1;
    declare controller: SheetController;
    declare width: SizeValue;
    declare height: SizeValue;
    declare overscan: number;

    static get cellPadding() {
        return EdgeInsets.symmetric(6, 4);
    }

    constructor(controller?: any, props?: any) {
        super();
        if (controller !== undefined) this.controller = controller;
        if (this.overscan === undefined) this.overscan = 3;
        this.controller = controller;
        this._config = this;
        this._rebuild = () => this.setState(() => {});
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let palette = SheetPalette.of(theme);
        let document = this.controller.document;
        this._digits = this.digitsShown(document);
        let numbers = Math.max(Spreadsheet.headerWidth, Math.fround(Math.ceil(context.measureText('0'.repeat(this._digits), theme.type('caption'))) + Math.fround(2 * Spreadsheet.numberPadding)));
        if (this._rows == null || this._columns == null || this._document !== document || this._columns.lead !== numbers) {
            this._document = document;
            this._rows = new LeadingSpans(document.rowSpans, Spreadsheet.headerHeight);
            this._columns = new LeadingSpans(document.columnSpans, numbers);
        }
        let cellType = theme.type(Spreadsheet.cellType);
        let $n0: any; 
        this.controller.grid = ($n0 = new SheetGrid(this._scroll, numbers, Spreadsheet.headerHeight), $n0.measure = (text: string) => context.measureText(text, cellType), $n0.lineHeight = cellType.lineHeight, $n0.cellPadding = Spreadsheet.cellPadding, $n0);
        let revealed = this.controller.revealCell;
        let table = new TableView(this._rows, this._columns, (row: number, column: number) => this.cell(row, column, document, palette, numbers), { pinnedRows: 1, pinnedColumns: 1, overscan: this._config.overscan, width: SizeValue.fill, height: SizeValue.fill, scroll: this._scroll, marks: this.marks(palette), request: new TableScrollRequest(this.controller.revealVersion, { reveal: new TableRange(revealed.row + 1, revealed.col + 1) }), onScrolled: this.scrolled.bind(this), onViewportChanged: this.viewportChanged.bind(this), canRequestFocus: false });
        return new Box(new BoxStyle({ width: this._config.width, height: this._config.height, borderColor: palette.gridline, borderWidth: 1, clip: true }), new SheetSurface(new Box(new BoxStyle({ width: SizeValue.fill, height: SizeValue.fill }), table), this.controller, { onChanged: () => this.controller.refresh(), label: SdkStrings.spreadsheet, caretColor: palette.cellText }));
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof Spreadsheet && (fresh = next, true)))) return;
        if (!(this._listening == null)) this.listen(fresh.controller);
        this.controller = fresh.controller;
        this._config = fresh;
    }

    onMount() {
        return this.listen(this.controller);
    }

    onUnmount() {
        return this.listen(null);
    }

    listen(controller: any) {
        if ((this._listening === controller)) return;
        if (!(this._listening == null)) this._listening.refreshed = $eq.delegates.remove(this._listening.refreshed, this._rebuild);
        this._listening = controller;
        if (!(controller == null)) controller.refreshed = $eq.delegates.combine(controller.refreshed, this._rebuild);
    }

    digitsShown(document: SheetDocument) {
        let height = this._scroll.viewport.height > 0 ? this._scroll.viewport.height : Spreadsheet.firstGuess;
        let last = document.rowSpans.indexAt(this._scroll.offset.down + height) + 1;
        return $eq.text.format(last, null, undefined, undefined, 'int32').length;
    }

    scrolled(_offset: TableOffset) {
        if (this.controller.follow()) this.controller.refresh(); else if (this.digitsShown(this.controller.document) !== this._digits) this.setState(() => {});
    }

    viewportChanged(_viewport: Size) {
        if (this.digitsShown(this.controller.document) !== this._digits) this.setState(() => {});
    }

    cell(row: number, column: number, document: SheetDocument, palette: SheetPalette, numbers: number) {
        if (row === 0 && column === 0) return Spreadsheet.headerCell('', palette, false, () => {
            this.controller.selectAll();
            this.controller.refresh();
        });
        if (row === 0) return this.columnHeader(column - 1, palette);
        if (column === 0) return this.rowHeader(row - 1, palette, numbers);
        return Spreadsheet.sheetCell(new CellRef(row - 1, column - 1), document, palette);
    }

    static headerCell(label: string, palette: SheetPalette, selected: boolean = false, onPressed: (() => void) | null = null) {
        let cell = new Box(new BoxStyle({ width: SizeValue.fill, height: SizeValue.fill, background: selected ? palette.headerSelectedBackground : palette.headerBackground, borderColor: palette.gridline, borderWidth: 0.5 }), label.length === 0 ? null : VisualNodeExtensions.centered(new Text(label, 'caption', selected ? palette.headerSelectedText : palette.headerText, 1)));
        return onPressed == null ? cell : new Pressable(cell, onPressed, { canRequestFocus: false });
    }

    columnHeader(c: number, palette: SheetPalette) {
        let col = c;
        let selection = this.controller.selection;
        let inBand = c >= selection.leftCol && c <= selection.rightCol;
        let stack = new Stack();
        stack.add(Spreadsheet.headerCell(CellRef.columnName(c), palette, inBand, () => {
            this.controller.selectCols(col, col);
            this.controller.refresh();
        }));
        stack.add(new Positioned(new Draggable(new Box(new BoxStyle({ width: SizeValue.fixed(Spreadsheet.grip), height: SizeValue.fixed(Spreadsheet.headerHeight), cursor: 'colResize' })), null, { axis: 'horizontal', min: -4000, max: 4000, follows: false, onMoved: (delta: number) => this.previewResize('cols', col, delta), onReleased: (delta: number) => this.commitResize('cols', col, delta) }), 0, 0, null, null, { layer: 1 }));
        return stack;
    }

    rowHeader(r: number, palette: SheetPalette, width: number) {
        let row = r;
        let selection = this.controller.selection;
        let inBand = r >= selection.topRow && r <= selection.bottomRow;
        let stack = new Stack();
        stack.add(Spreadsheet.headerCell(`${$eq.text.format(r + 1, null, undefined, undefined, 'int32')}`, palette, inBand, () => {
            this.controller.selectRows(row, row);
            this.controller.refresh();
        }));
        stack.add(new Positioned(new Draggable(new Box(new BoxStyle({ width: SizeValue.fixed(width), height: SizeValue.fixed(Spreadsheet.grip), cursor: 'rowResize' })), null, { axis: 'vertical', min: -4000, max: 4000, follows: false, onMoved: (delta: number) => this.previewResize('rows', row, delta), onReleased: (delta: number) => this.commitResize('rows', row, delta) }), null, null, 0, 0, { layer: 1 }));
        return stack;
    }

    previewResize(axis: SheetAxisValue, index: number, delta: number) {
        let document = this.controller.document;
        if (this._resizeBase < 0) this._resizeBase = axis === 'cols' ? document.colWidth(index) : document.rowHeight(index);
        let floor = axis === 'cols' ? Spreadsheet.minColWidth : Spreadsheet.minRowHeight;
        let size = Math.max(floor, Math.fround(this._resizeBase + delta));
        this.setState(() => {
            if (axis === 'cols') document.setColWidth(index, size); else document.setRowHeight(index, size);
        });
    }

    commitResize(axis: SheetAxisValue, index: number, delta: number) {
        if (this._resizeBase < 0) return;
        let document = this.controller.document;
        let floor = axis === 'cols' ? Spreadsheet.minColWidth : Spreadsheet.minRowHeight;
        let final = Math.max(floor, Math.fround(this._resizeBase + delta));
        if (axis === 'cols') document.setColWidth(index, this._resizeBase); else document.setRowHeight(index, this._resizeBase);
        this._resizeBase = -1;
        this.controller.resize(axis, index, final);
        this.setState(() => {});
    }

    static sheetCell(cell: CellRef, document: SheetDocument, palette: SheetPalette) {
        let value = document.getCell(cell);
        let content: VisualNode | null = null;
        if (value.includes('\n')) {
            let lines = new Column(0);
            for (const line of $eq.text.split(value, '\n')) lines.add(new Text(line, Spreadsheet.cellType, palette.cellText, 1));
            content = lines;
        } else if (value.length > 0) {
            content = new Text(value, Spreadsheet.cellType, palette.cellText, 1);
        }
        return new Box(new BoxStyle({ width: SizeValue.fill, height: SizeValue.fill, background: palette.cellBackground, borderColor: palette.gridline, borderWidth: 0.5, padding: Spreadsheet.cellPadding, clip: true }), content);
    }

    marks(palette: SheetPalette) {
        let marks: TableViewCell[] = [];
        let selection = this.controller.selection;
        if (!selection.isSingleCell) {
            marks.push(Spreadsheet.mark(selection, new Box(new BoxStyle({ width: SizeValue.fill, height: SizeValue.fill, background: palette.rangeFill }))));
        }
        let preview: any; 
        if ((preview = this.controller.fillTarget) != null) {
            marks.push(Spreadsheet.mark(preview, new Box(new BoxStyle({ width: SizeValue.fill, height: SizeValue.fill, borderColor: palette.fillPreview, borderWidth: 1.5 }))));
        }
        if (this.controller.editing) {
            marks.push(Spreadsheet.mark(this.controller.editorRange, this.editor(palette)));
            return marks;
        }
        marks.push(Spreadsheet.mark(new SheetRange(this.controller.activeCell), new Box(new BoxStyle({ width: SizeValue.fill, height: SizeValue.fill, borderColor: palette.activeOutline, borderWidth: 2 }))));
        let corner = new Stack();
        corner.add(new Box(new BoxStyle({ width: SizeValue.fill, height: SizeValue.fill })));
        corner.add(new Positioned(new Box(new BoxStyle({ width: SizeValue.fixed(Spreadsheet.fillHandle), height: SizeValue.fixed(Spreadsheet.fillHandle), background: palette.fillHandle, borderColor: palette.cellBackground, borderWidth: 1 })), null, -1, -1));
        marks.push(Spreadsheet.mark(new SheetRange(new CellRef(selection.bottomRow, selection.rightCol)), corner));
        return marks;
    }

    editor(palette: SheetPalette) {
        let grid = this.controller.cellEditorGrid;
        let bounds = this.controller.editorBounds;
        return new Box(new BoxStyle({ width: SizeValue.fill, height: SizeValue.fill, background: palette.cellBackground, borderColor: palette.activeOutline, borderWidth: 2, clip: true }), new EntryLines(this.controller.entry, grid, new Point(bounds.x, bounds.y), Spreadsheet.cellType, palette, this.controller.entryView === 'cell'));
    }

    static mark(range: SheetRange, child: VisualNode) {
        return new TableViewCell(range.topRow + 1, range.leftCol + 1, child, true, { rows: range.bottomRow - range.topRow + 1, columns: range.rightCol - range.leftCol + 1 });
    }
}

