import { $eq, Adjustable, Box, BoxStyle, BuildContext, Checkbox, Column, DataColumn, DataRow, Draggable, EdgeInsets, Grid, GridTrack, Icon, IconGlyph, Motion, Positioned, Pressable, RangeValue, Row, SdkStrings, SizeValue, Skeleton, Sortable, SortableItem, SortDrop, Stack, StatefulComponent, StyleDiff, Text, TextAlignmentValue, TransitionSpec, UiComponent, VisualNode } from "../runtime-exports";

export class DataTable extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Components.DataTable';
    static resizeStep: number = 16;
    static handleWidth: number = 8;
    static edgeLineWidth: number = 2;
    static headerListId: string = 'columns';
    _config: DataTable;
    _carried: number = -1;
    _landing: number = -1;
    declare columns: DataColumn[];
    declare rows: DataRow[];
    declare sortColumn: number;
    declare sortDirection: string;
    declare onSort: ((int: number) => void) | null;
    declare selection: any;
    declare onToggleRow: ((dataRow: DataRow) => void) | null;
    declare onToggleAll: (() => void) | null;
    declare onRowPressed: ((dataRow: DataRow) => void) | null;
    declare pendingRows: number;
    declare empty: any;
    declare columnOrder: any;
    declare onColumnsReordered: ((value: number[]) => void) | null;
    declare onColumnResized: ((arg1: number, arg2: number) => void) | null;

    get selectable() {
        return !(this._config.selection == null);
    }

    get reorders() {
        return !(this._config.onColumnsReordered == null);
    }

    constructor(columns?: any, rows?: any, props?: any) {
        super();
        if (columns !== undefined) this.columns = columns;
        if (rows !== undefined) this.rows = rows;
        if (this.sortColumn === undefined) this.sortColumn = -1;
        if (this.sortDirection === undefined) this.sortDirection = 'none';
        if (this.pendingRows === undefined) this.pendingRows = 0;
        this.columns = columns;
        this.rows = rows;
        this._config = this;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let order = this.drawnOrder();
        let tracks = this.tracks(theme.table, order);
        let table = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        table.add(this.header(theme, tracks, order));
        let empty: any; 
        if ($eq.collections.count(this._config.rows) === 0 && this._config.pendingRows === 0 && (empty = this._config.empty) != null) {
            table.add(empty);
            return table;
        }
        for (const row of this._config.rows) table.add(this.body(theme, tracks, order, row));
        for (let i = 0; i < this._config.pendingRows; i++) table.add(this.pending(theme, tracks, order, i));
        return table;
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if ((next instanceof DataTable && (fresh = next, true))) this._config = fresh;
    }

    drawnOrder() {
        let order: number[] = [];
        let given: any; 
        if ((given = this._config.columnOrder) != null) {
            for (const position of given) {
                if (position >= 0 && position < $eq.collections.count(this._config.columns) && !order.includes(position)) order.push(position);
            }
        }
        for (let i = 0; i < $eq.collections.count(this._config.columns); i++) {
            if (!order.includes(i)) order.push(i);
        }
        return order;
    }

    tracks(slot: any, order: number[]) {
        let tracks = new Array(order.length + (this.selectable ? 1 : 0)).fill(undefined);
        let offset = 0;
        if (this.selectable) tracks[offset++] = GridTrack.fixed(slot?.selectionColumnWidth ?? 44);
        for (let i = 0; i < order.length; i++) tracks[offset + i] = $eq.collections.item(this._config.columns, order[i]).track;
        return tracks;
    }

    header(theme: any, tracks: GridTrack[], order: number[]) {
        let slot = theme.table;
        let grid = new Grid(tracks, 0, null, { width: SizeValue.fill });
        if (this.selectable) {
            let anySelected = $eq.collections.count(this._config.selection!) > 0;
            grid.add(DataTable.cell(new Box(new BoxStyle(), new Checkbox(anySelected, this._config.onToggleAll, null, { indeterminate: anySelected && $eq.collections.count(this._config.selection) < $eq.collections.count(this._config.rows) })), 'center', slot));
        }
        let others = this._carried >= 0 ? DataTable.without(order, this._carried) : order;
        for (let i = 0; i < order.length; i++) {
            let index = order[i];
            let heading = this.heading(theme, slot, index);
            let lineAtStart = this._landing >= 0 && this._landing < others.length && others[this._landing] === index;
            let lineAtEnd = this._landing >= 0 && this._landing === others.length && others[others.length - 1] === index;
            let resizes = this.resizes($eq.collections.item(this._config.columns, index));
            if (!lineAtStart && !lineAtEnd && !resizes) {
                grid.add(heading);
                continue;
            }
            let cell = new Stack('topStart', { width: SizeValue.fill, height: SizeValue.fill });
            cell.add(heading);
            if (lineAtStart) cell.add(new Positioned(DataTable.dropLine(theme, slot), 0, null, 0, 0));
            if (lineAtEnd) cell.add(new Positioned(DataTable.dropLine(theme, slot), 0, 0, 0));
            if (resizes) cell.add(new Positioned(this.resizeHandle(theme, slot, index), 0, 0, 0));
            grid.add(cell);
        }
        let row = !this.reorders ? grid : new Sortable(grid, DataTable.headerListId, $eq.delegates.bound(this, this.dropped), SdkStrings.sorting, { axis: 'horizontal', label: SdkStrings.tableColumns, onHovered: $eq.delegates.bound(this, this.hovered).bind(this) });
        return new Box(new BoxStyle({ width: SizeValue.fill, background: slot?.headerFill ?? theme.surfaceSubtle, borderWidth: slot?.borderWidth ?? 1, borderColor: slot?.borderColor ?? theme.border }), row);
    }

    heading(theme: any, slot: any, index: number) {
        let column = $eq.collections.item(this._config.columns, index);
        let sorted = index === this._config.sortColumn && this._config.sortDirection !== 'none';
        let label = new Row(slot?.headerIconGap ?? 4, 'start', 'center', false, null, null, { width: SizeValue.fill, cross: 'center', main: column.align === 'start' ? 'start' : 'end' });
        let sortedColor = slot?.sortedHeaderColor ?? theme.textPrimary;
        label.add(new Text(slot?.headerUppercase ?? true ? $eq.text.toUpper(column.header, true) : column.header, 'caption', sorted ? sortedColor : slot?.dataHeaderColor ?? theme.textMuted, 1));
        if (sorted) {
            label.add(new Icon(IconGlyph.fromIcons(this._config.sortDirection === 'ascending' ? 'chevronUp' : 'chevronDown'), slot?.sortIconSize ?? 16, sortedColor));
        }
        let onSort = this._config.onSort;
        let heading = column.sortable && !(onSort == null) ? new Pressable(DataTable.cell(label, column.align, slot), () => onSort(index), { label: SdkStrings.sortByColumn(column.header) }) : DataTable.cell(label, column.align, slot);
        return this.reorders ? new SortableItem(heading, DataTable.columnId(index), { label: column.header }) : heading;
    }

    resizes(column: DataColumn) {
        return !(this._config.onColumnResized == null) && column.track.kind === 'fixed' && !column.track.repeats;
    }

    resizeHandle(theme: any, slot: any, index: number) {
        let column = $eq.collections.item(this._config.columns, index);
        let min = column.minWidth;
        let max = Math.max(min, column.maxWidth);
        let width = column.track.value;
        let color = slot?.resizeHandleColor ?? theme.focusRing;
        let resize = this._config.onColumnResized!;
        let edge = new Box(new BoxStyle({ width: SizeValue.fixed(DataTable.handleWidth), height: SizeValue.fill, cursor: 'colResize', borderSides: 2, borderWidth: 0, borderColor: color, hover: new StyleDiff({ borderWidth: DataTable.edgeLineWidth }), focus: new StyleDiff({ borderWidth: DataTable.edgeLineWidth }) }));
        let surface = new Draggable(edge, null, { axis: 'horizontal', follows: false, restOffset: width, min: min, max: max, onMoved: (next: number) => resize(index, next) });
        return new Adjustable(surface, (direction: number) => {
            let next = Math.min(max, Math.max(min, Math.fround(width + Math.fround(Math.fround(direction) * DataTable.resizeStep))));
            if (next !== width) resize(index, next);
        }, { role: 'separator', axis: 'horizontal', label: SdkStrings.resizeColumn(column.header), value: new RangeValue(width, min, max) });
    }

    static dropLine(theme: any, slot: any) {
        return new Box(new BoxStyle({ width: SizeValue.fixed(DataTable.edgeLineWidth), height: SizeValue.fill, background: slot?.columnDropColor ?? theme.focusRing }));
    }

    static columnId(index: number) {
        return `${$eq.text.format(index, null, undefined, undefined, 'int32')}`;
    }

    columnOf(id: string) {
        for (let i = 0; i < $eq.collections.count(this._config.columns); i++) {
            if (DataTable.columnId(i) === id) return i;
        }
        return -1;
    }

    static without(order: number[], column: number) {
        let others: number[] = [];
        for (const index of order) {
            if (index !== column) others.push(index);
        }
        return others;
    }

    static reordered(order: number[], column: number, at: number) {
        let next = DataTable.without(order, column);
        $eq.collections.listInsert(next, Math.min(next.length, Math.max(0, at)), column);
        return next;
    }

    static sameOrder(a: number[], b: number[]) {
        if (a.length !== b.length) return false;
        for (let i = 0; i < a.length; i++) {
            if (a[i] !== b[i]) return false;
        }
        return true;
    }

    hovered(drop: any) {
        let carried = drop == null ? -1 : this.columnOf(drop.item);
        let landing = -1;
        if (!(drop == null) && carried >= 0) {
            let order = this.drawnOrder();
            if (!DataTable.sameOrder(DataTable.reordered(order, carried, drop.index), order)) landing = Math.min(order.length - 1, Math.max(0, drop.index));
        }
        if (carried === this._carried && landing === this._landing) return;
        this.setState(() => {
            this._carried = carried;
            this._landing = landing;
        });
    }

    dropped(drop: SortDrop) {
        let column = this.columnOf(drop.item);
        if (column < 0) return;
        let order = this.drawnOrder();
        let next = DataTable.reordered(order, column, drop.index);
        if (!DataTable.sameOrder(next, order)) this._config.onColumnsReordered?.(next);
    }

    body(theme: any, tracks: GridTrack[], order: number[], row: DataRow) {
        let $n1: any; 
        let selected = (($n1 = this._config.selection) == null ? null : $eq.collections.contains($n1, row.key)) === true;
        let slot = theme.table;
        let grid = new Grid(tracks, 0, null, { width: SizeValue.fill });
        if (this.selectable) {
            let toggle = this._config.onToggleRow;
            grid.add(DataTable.cell(new Box(new BoxStyle(), new Checkbox(selected, () => toggle?.(row), null, { disabled: toggle == null })), 'center', slot));
        }
        for (const index of order) {
            grid.add(DataTable.cell(index < $eq.collections.count(row.cells) ? $eq.collections.item(row.cells, index) : new Box(new BoxStyle()), $eq.collections.item(this._config.columns, index).align, slot));
        }
        let box = new Box(new BoxStyle({ width: SizeValue.fill, minHeight: slot?.dataRowHeight ?? 52, background: selected ? theme.colors('primary').subtle : null, borderWidth: slot?.borderWidth ?? 1, borderColor: slot?.borderColor ?? theme.border, hover: new StyleDiff({ background: slot?.hoverFill ?? theme.surfaceSubtle }), transition: TransitionSpec.of(1, Motion.press) }), grid, { key: row.key });
        let onRowPressed = this._config.onRowPressed;
        return onRowPressed == null ? box : new Pressable(box, () => onRowPressed(row));
    }

    pending(theme: any, tracks: GridTrack[], order: number[], index: number) {
        let slot = theme.table;
        let grid = new Grid(tracks, 0, null, { width: SizeValue.fill });
        if (this.selectable) grid.add(DataTable.cell(new Box(new BoxStyle(), new Skeleton('block', 16, 16)), 'center', slot));
        for (let i = 0; i < order.length; i++) {
            grid.add(DataTable.cell(new Box(new BoxStyle(), new Skeleton('line', i % 2 === 0 ? 96 : 64)), $eq.collections.item(this._config.columns, order[i]).align, slot));
        }
        return new Box(new BoxStyle({ width: SizeValue.fill, minHeight: slot?.dataRowHeight ?? 52, borderWidth: slot?.borderWidth ?? 1, borderColor: slot?.borderColor ?? theme.border }), grid, { key: `pending-${$eq.text.format(index, null, undefined, undefined, 'int32')}` });
    }

    static cell(child: VisualNode, align: TextAlignmentValue, slot: any) {
        let row = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, height: SizeValue.fill, cross: 'center', main: (() => { const $s = align; if ($s === 'center') return 'center'; if ($s === 'end') return 'end'; return 'start'; })() });
        row.add(child);
        return new Box(new BoxStyle({ width: SizeValue.fill, padding: EdgeInsets.symmetric(slot?.cellPaddingX ?? 12, slot?.cellPaddingY ?? 8) }), row);
    }
}

