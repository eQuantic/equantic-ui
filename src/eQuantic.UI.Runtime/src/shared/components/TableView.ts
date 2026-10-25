import { $eq, BuildContext, Size, SizeValue, StatefulComponent, TableOffset, TableScroll, TableSpans, TableViewCell, TableViewport, UiComponent, VisualNode } from "../runtime-exports";

export class TableView extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Components.TableView';
    _ownScroll: TableScroll = new TableScroll();
    _config: TableView;
    _viewport: Size = new Size();
    _firstRow: number = 0;
    _lastRow: number = -1;
    _firstColumn: number = 0;
    _lastColumn: number = -1;
    declare rows: TableSpans;
    declare columns: TableSpans;
    declare cellBuilder: (arg1: number, arg2: number) => VisualNode | null;
    declare pinnedRows: number;
    declare pinnedColumns: number;
    declare overscan: number;
    declare width: SizeValue;
    declare height: SizeValue;
    declare marks: any;
    declare request: any;
    declare onScrolled: ((tableOffset: TableOffset) => void) | null;
    declare onViewportChanged: ((size: Size) => void) | null;
    declare label: any;
    declare scroll: any;
    declare canRequestFocus: boolean;
    static $slots: any = null;
    static $failure: any = null;

    static $init(): any {
        if (TableView.$slots === null) {
            if (TableView.$failure !== null) throw TableView.$failure;
            let $slots: any = TableView.$slots = {};
            try {
                $slots.firstGuess = new Size();
                $slots.firstGuess = new Size(1024, 768);
            } catch ($error) {
                TableView.$slots = null;
                throw TableView.$failure = $eq.exceptions.typeInitialization('eQuantic.UI.Components.TableView', $error);
            }
        }
        return TableView.$slots;
    }

    static get firstGuess(): Size {
        return TableView.$init().firstGuess;
    }

    static set firstGuess(value: Size) {
        TableView.$init().firstGuess = value;
    }

    constructor(rows?: any, columns?: any, cellBuilder?: any, props?: any) {
        super();
        if (rows !== undefined) this.rows = rows;
        if (columns !== undefined) this.columns = columns;
        if (cellBuilder !== undefined) this.cellBuilder = cellBuilder;
        if (this.pinnedRows === undefined) this.pinnedRows = 0;
        if (this.pinnedColumns === undefined) this.pinnedColumns = 0;
        if (this.overscan === undefined) this.overscan = 3;
        if (this.canRequestFocus === undefined) this.canRequestFocus = true;
        this.rows = rows;
        this.columns = columns;
        this.cellBuilder = cellBuilder;
        this._config = this;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(_context: BuildContext) {
        let config = this._config;
        let rows = config.rows;
        let columns = config.columns;
        let pinnedRows = Math.min(Math.max(config.pinnedRows, 0), rows.count);
        let pinnedColumns = Math.min(Math.max(config.pinnedColumns, 0), columns.count);
        let scroll = config.scroll ?? this._ownScroll;
        if (!this.covers(scroll.offset) || this._lastRow >= rows.count || this._lastColumn >= columns.count) this.window(scroll.offset);
        let viewport = new TableViewport(rows, columns, scroll, { pinnedRows: pinnedRows, pinnedColumns: pinnedColumns, width: config.width, height: config.height, request: config.request, onScrolled: this.scrolled.bind(this), onViewportChanged: this.viewportChanged.bind(this), label: config.label, canRequestFocus: config.canRequestFocus });
        for (let row = 0; row < pinnedRows; row++) {
            for (let column = 0; column < pinnedColumns; column++) this.place(viewport, row, column);
            for (let column = this._firstColumn; column <= this._lastColumn; column++) this.place(viewport, row, column);
        }
        for (let row = this._firstRow; row <= this._lastRow; row++) {
            for (let column = 0; column < pinnedColumns; column++) this.place(viewport, row, column);
            for (let column = this._firstColumn; column <= this._lastColumn; column++) this.place(viewport, row, column);
        }
        let marks: any; 
        if ((marks = config.marks) != null) for (let index = 0; index < $eq.collections.count(marks); index++) TableView.addMark(viewport, $eq.collections.item(marks, index), index, pinnedRows, pinnedColumns);
        return viewport;
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof TableView && (fresh = next, true)))) return;
        if (fresh.rows !== this._config.rows || fresh.columns !== this._config.columns || fresh.pinnedRows !== this._config.pinnedRows || fresh.pinnedColumns !== this._config.pinnedColumns || fresh.overscan !== this._config.overscan) {
            this._lastRow = -1;
            this._lastColumn = -1;
        }
        this._config = fresh;
    }

    place(viewport: TableViewport, row: number, column: number) {
        let child: any; 
        if ((child = this._config.cellBuilder(row, column)) != null) viewport.add(new TableViewCell(row, column, child));
    }

    static addMark(viewport: TableViewport, mark: TableViewCell, index: number, pinnedRows: number, pinnedColumns: number) {
        let lastRow = mark.row + Math.max(1, mark.rows);
        let lastColumn = mark.column + Math.max(1, mark.columns);
        let piece = 0;
        for (const [rowFrom, rowTo] of TableView.cut(mark.row, lastRow, pinnedRows)) {
            for (const [columnFrom, columnTo] of TableView.cut(mark.column, lastColumn, pinnedColumns)) {
                viewport.add(new TableViewCell(rowFrom, columnFrom, mark.child, true, { rows: rowTo - rowFrom, columns: columnTo - columnFrom, key: (mark.key ?? 'mark:' + $eq.text.format(index, null, undefined, undefined, 'int32')) + '/' + $eq.text.format(piece, null, undefined, undefined, 'int32'), glide: mark.glide }));
                piece++;
            }
        }
    }

    static cut(from: number, to: number, pinned: number) {
        let pieces: [number, number][] = [];
        if (from < pinned) pieces.push([from, Math.min(to, pinned)]);
        if (to > pinned) pieces.push([Math.max(from, pinned), to]);
        return pieces;
    }

    covers(offset: TableOffset) {
        if (this._lastRow < this._firstRow && this._lastColumn < this._firstColumn) return false;
        let [firstRow, lastRow, firstColumn, lastColumn] = this.visible(offset);
        return firstRow >= this._firstRow && lastRow <= this._lastRow && firstColumn >= this._firstColumn && lastColumn <= this._lastColumn;
    }

    window(offset: TableOffset) {
        let [firstRow, lastRow, firstColumn, lastColumn] = this.visible(offset);
        let overscan = Math.max(0, this._config.overscan);
        let pinnedRows = Math.min(Math.max(this._config.pinnedRows, 0), this._config.rows.count);
        let pinnedColumns = Math.min(Math.max(this._config.pinnedColumns, 0), this._config.columns.count);
        this._firstRow = Math.max(pinnedRows, firstRow - overscan);
        this._lastRow = Math.min(this._config.rows.count - 1, lastRow + overscan);
        this._firstColumn = Math.max(pinnedColumns, firstColumn - overscan);
        this._lastColumn = Math.min(this._config.columns.count - 1, lastColumn + overscan);
    }

    visible(offset: TableOffset): [number, number, number, number] {
        let config = this._config;
        let size = this._viewport.width > 0 && this._viewport.height > 0 ? this._viewport : TableView.firstGuess;
        let pinnedRows = Math.min(Math.max(config.pinnedRows, 0), config.rows.count);
        let pinnedColumns = Math.min(Math.max(config.pinnedColumns, 0), config.columns.count);
        let pinnedDown = config.rows.offsetOf(pinnedRows);
        let pinnedAcross = config.columns.offsetOf(pinnedColumns);
        let firstRow = Math.max(pinnedRows, config.rows.indexAt(offset.down + pinnedDown));
        let lastRow = config.rows.indexAt(offset.down + Math.max(pinnedDown, size.height) - 0.5);
        let firstColumn = Math.max(pinnedColumns, config.columns.indexAt(offset.across + pinnedAcross));
        let lastColumn = config.columns.indexAt(offset.across + Math.max(pinnedAcross, size.width) - 0.5);
        return [firstRow, lastRow, firstColumn, lastColumn];
    }

    scrolled(offset: TableOffset) {
        this._config.onScrolled?.(offset);
        if (!this.covers(offset)) this.setState(() => {});
    }

    viewportChanged(viewport: Size) {
        this._config.onViewportChanged?.(viewport);
        if ($eq.equals(viewport, this._viewport)) return;
        this.setState(() => {
            this._viewport = viewport;
            this._lastRow = -1;
            this._lastColumn = -1;
        });
    }
}

