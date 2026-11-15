import { $eq, Box, BoxStyle, BuildContext, Checkbox, Column, DataColumn, DataRow, EdgeInsets, Grid, GridTrack, Icon, IconGlyph, Motion, Pressable, Row, SizeValue, Skeleton, StatelessComponent, StyleDiff, Text, TextAlignmentValue, TransitionSpec, VisualNode } from "../runtime-exports";

export class DataTable extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.DataTable';
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

    get selectable() {
        return !(this.selection == null);
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
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let tracks = this.tracks(theme.table);
        let table = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        table.add(this.header(theme, tracks));
        let empty: any; 
        if ($eq.collections.count(this.rows) === 0 && this.pendingRows === 0 && (empty = this.empty) != null) {
            table.add(empty);
            return table;
        }
        for (const row of this.rows) table.add(this.body(theme, tracks, row));
        for (let i = 0; i < this.pendingRows; i++) table.add(this.pending(theme, tracks, i));
        return table;
    }

    tracks(slot: any) {
        let tracks = new Array($eq.collections.count(this.columns) + (this.selectable ? 1 : 0)).fill(undefined);
        let offset = 0;
        if (this.selectable) tracks[offset++] = GridTrack.fixed(slot?.selectionColumnWidth ?? 44);
        for (let i = 0; i < $eq.collections.count(this.columns); i++) tracks[offset + i] = $eq.collections.item(this.columns, i).track;
        return tracks;
    }

    header(theme: any, tracks: GridTrack[]) {
        let slot = theme.table;
        let grid = new Grid(tracks, 0, null, { width: SizeValue.fill });
        if (this.selectable) {
            let anySelected = $eq.collections.count(this.selection!) > 0;
            grid.add(DataTable.cell(new Box(new BoxStyle(), new Checkbox(anySelected, this.onToggleAll, null, { indeterminate: anySelected && $eq.collections.count(this.selection) < $eq.collections.count(this.rows) })), 'center', slot));
        }
        for (let i = 0; i < $eq.collections.count(this.columns); i++) {
            let column = $eq.collections.item(this.columns, i);
            let sorted = i === this.sortColumn && this.sortDirection !== 'none';
            let label = new Row(slot?.headerIconGap ?? 4, 'start', 'center', false, null, null, { width: SizeValue.fill, cross: 'center', main: column.align === 'start' ? 'start' : 'end' });
            let sortedColor = slot?.sortedHeaderColor ?? theme.textPrimary;
            label.add(new Text(slot?.headerUppercase ?? true ? $eq.text.toUpper(column.header, true) : column.header, 'caption', sorted ? sortedColor : slot?.dataHeaderColor ?? theme.textMuted, 1));
            if (sorted) {
                label.add(new Icon(IconGlyph.fromIcons(this.sortDirection === 'ascending' ? 'chevronUp' : 'chevronDown'), slot?.sortIconSize ?? 16, sortedColor));
            }
            let index = i;
            grid.add(column.sortable && !(this.onSort == null) ? new Pressable(DataTable.cell(label, column.align, slot), () => this.onSort!(index), { label: `Sort by ${column.header}` }) : DataTable.cell(label, column.align, slot));
        }
        return new Box(new BoxStyle({ width: SizeValue.fill, background: slot?.headerFill ?? theme.surfaceSubtle, borderWidth: slot?.borderWidth ?? 1, borderColor: slot?.borderColor ?? theme.border }), grid);
    }

    body(theme: any, tracks: GridTrack[], row: DataRow) {
        let $n0: any; 
        let selected = (($n0 = this.selection) == null ? null : $eq.collections.contains($n0, row.key)) === true;
        let slot = theme.table;
        let grid = new Grid(tracks, 0, null, { width: SizeValue.fill });
        if (this.selectable) {
            let toggle = this.onToggleRow;
            grid.add(DataTable.cell(new Box(new BoxStyle(), new Checkbox(selected, () => toggle?.(row), null, { disabled: toggle == null })), 'center', slot));
        }
        for (let i = 0; i < $eq.collections.count(this.columns) && i < $eq.collections.count(row.cells); i++) grid.add(DataTable.cell($eq.collections.item(row.cells, i), $eq.collections.item(this.columns, i).align, slot));
        let box = new Box(new BoxStyle({ width: SizeValue.fill, minHeight: slot?.dataRowHeight ?? 52, background: selected ? theme.colors('primary').subtle : null, borderWidth: slot?.borderWidth ?? 1, borderColor: slot?.borderColor ?? theme.border, hover: new StyleDiff({ background: slot?.hoverFill ?? theme.surfaceSubtle }), transition: TransitionSpec.of(1, Motion.press) }), grid, { key: row.key });
        return this.onRowPressed == null ? box : new Pressable(box, () => this.onRowPressed!(row));
    }

    pending(theme: any, tracks: GridTrack[], index: number) {
        let slot = theme.table;
        let grid = new Grid(tracks, 0, null, { width: SizeValue.fill });
        if (this.selectable) grid.add(DataTable.cell(new Box(new BoxStyle(), new Skeleton('block', 16, 16)), 'center', slot));
        for (let i = 0; i < $eq.collections.count(this.columns); i++) {
            grid.add(DataTable.cell(new Box(new BoxStyle(), new Skeleton('line', i % 2 === 0 ? 96 : 64)), $eq.collections.item(this.columns, i).align, slot));
        }
        return new Box(new BoxStyle({ width: SizeValue.fill, minHeight: slot?.dataRowHeight ?? 52, borderWidth: slot?.borderWidth ?? 1, borderColor: slot?.borderColor ?? theme.border }), grid, { key: `pending-${$eq.text.format(index, null, undefined, undefined, 'int32')}` });
    }

    static cell(child: VisualNode, align: TextAlignmentValue, slot: any) {
        let row = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, height: SizeValue.fill, cross: 'center', main: (() => { const $s = align; if ($s === 'center') return 'center'; if ($s === 'end') return 'end'; return 'start'; })() });
        row.add(child);
        return new Box(new BoxStyle({ width: SizeValue.fill, padding: EdgeInsets.symmetric(slot?.cellPaddingX ?? 12, slot?.cellPaddingY ?? 8) }), row);
    }
}

