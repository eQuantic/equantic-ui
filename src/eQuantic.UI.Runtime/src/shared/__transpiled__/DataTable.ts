import { Box, BoxStyle, BuildContext, Checkbox, Column, DataRow, EdgeInsets, Grid, GridTrack, Icon, Motion, Pressable, Row, SizeValue, Skeleton, StatelessComponent, StyleDiff, Text, TransitionSpec, VisualNode } from "@equantic/runtime";

export class DataTable extends StatelessComponent {
    static rowHeight: number = 52;
    static checkboxTrack: number = 44;
    declare columns: any;
    declare rows: any;
    declare sortColumn: number;
    declare sortDirection: string;
    declare onSort: any;
    declare selection: any;
    declare onToggleRow: any;
    declare onToggleAll: () => void;
    declare onRowPressed: any;
    declare pendingRows: number;
    declare empty: VisualNode;
    get selectable() { return !(this.selection == null); }
    constructor(columns?: any, rows?: any, props?: any) {
        super(props);
        if (columns !== undefined) this.columns = columns;
        if (rows !== undefined) this.rows = rows;
        if (this.sortColumn === undefined) this.sortColumn = -1;
        if (this.sortDirection === undefined) this.sortDirection = 'none';
        this.columns = columns;this.rows = rows;
    }

    build(context: BuildContext) {
        let theme = context.theme;let tracks = this.tracks();let table = new Column(0, { width: SizeValue.fill });table.add(this.header(theme, tracks));let empty; if (this.rows.length === 0 && this.pendingRows === 0 && ((this.empty != null) && (empty = this.empty, true))) {table.add(empty);return table;}for (const row of this.rows) table.add(this.body(theme, tracks, row));for (let i = 0; i < this.pendingRows; i++) table.add(this.pending(theme, tracks, i));return table;
    }

    tracks() {
        let tracks = new Array(this.columns.length + (this.selectable ? 1 : 0)).fill(null);let offset = 0;if (this.selectable) tracks[offset++] = GridTrack.fixed(DataTable.checkboxTrack);for (let i = 0; i < this.columns.length; i++) tracks[offset + i] = this.columns[i].track;return tracks;
    }

    header(theme: any, tracks: GridTrack[]) {
        let grid = new Grid(tracks, 0, null, { width: SizeValue.fill });if (this.selectable) {let anySelected = this.selection!.length > 0;grid.add(DataTable.cell(new Box(new BoxStyle(), new Checkbox(anySelected, this.onToggleAll, null, { indeterminate: anySelected && this.selection.length < this.rows.length })), 'center'));}for (let i = 0; i < this.columns.length; i++) {let column = this.columns[i];let sorted = i === this.sortColumn && this.sortDirection !== 'none';let label = new Row(4, { width: SizeValue.fill, cross: 'center', main: column.align === 'start' ? 'start' : 'end' });label.add(new Text(column.header.toUpperCase(), 'caption', sorted ? theme.textPrimary : theme.textMuted, 1));if (sorted) {label.add(new Icon(this.sortDirection === 'ascending' ? 'chevronUp' : 'chevronDown', 16, theme.textPrimary));}let index = i;grid.add(column.sortable && !(this.onSort == null) ? new Pressable(DataTable.cell(label, column.align), () => this.onSort(index), { label: `Sort by ${column.header}` }) : DataTable.cell(label, column.align));}return new Box(new BoxStyle({ width: SizeValue.fill, background: theme.surfaceSubtle, borderWidth: 1, borderColor: theme.border }), grid);
    }

    body(theme: any, tracks: GridTrack[], row: DataRow) {
        let selection; let selected = ((this.selection != null) && (selection = this.selection, true)) && selection.includes(row.key);let grid = new Grid(tracks, 0, null, { width: SizeValue.fill });if (this.selectable) {let toggle = this.onToggleRow;grid.add(DataTable.cell(new Box(new BoxStyle(), new Checkbox(selected, () => toggle?.(row), null, { disabled: toggle == null })), 'center'));}for (let i = 0; i < this.columns.length && i < row.cells.length; i++) grid.add(DataTable.cell(row.cells[i], this.columns[i].align));let box = new Box(new BoxStyle({ width: SizeValue.fill, minHeight: DataTable.rowHeight, background: selected ? theme.colors('primary').subtle : null, borderWidth: 1, borderColor: theme.border, hover: new StyleDiff({ background: theme.surfaceSubtle }), transition: TransitionSpec.of(1, Motion.press) }), grid, { key: row.key });return this.onRowPressed == null ? box : new Pressable(box, () => this.onRowPressed(row));
    }

    pending(theme: any, tracks: GridTrack[], index: number) {
        let grid = new Grid(tracks, 0, null, { width: SizeValue.fill });if (this.selectable) grid.add(DataTable.cell(new Box(new BoxStyle(), new Skeleton('block', 16, 16)), 'center'));for (let i = 0; i < this.columns.length; i++) {grid.add(DataTable.cell(new Box(new BoxStyle(), new Skeleton('line', i % 2 === 0 ? 96 : 64)), this.columns[i].align));}return new Box(new BoxStyle({ width: SizeValue.fill, minHeight: DataTable.rowHeight, borderWidth: 1, borderColor: theme.border }), grid, { key: `pending-${index}` });
    }

    static cell(child: VisualNode, align: string) {
        let row = new Row(0, { width: SizeValue.fill, height: SizeValue.fill, cross: 'center', main: (() => { const _s = align; if (_s === 'center') return 'center'; if (_s === 'end') return 'end'; return 'start'; })() });row.add(child);return new Box(new BoxStyle({ width: SizeValue.fill, padding: EdgeInsets.symmetric(12, 8) }), row);
    }

}

