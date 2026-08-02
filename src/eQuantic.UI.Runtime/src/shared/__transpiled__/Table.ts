import { Box, BoxStyle, BuildContext, Column, Divider, EdgeInsets, Grid, GridTrack, SizeValue, StatelessComponent, StyleDiff, Text } from "@equantic/runtime";

export class Table extends StatelessComponent {
    declare columns: string[];
    declare rows: string[][];
    constructor(columns?: any, rows?: any, props?: any) {
        super(props);
        if (columns !== undefined) this.columns = columns;
        if (rows !== undefined) this.rows = rows;
        this.columns = columns;this.rows = rows;
    }

    build(context: BuildContext) {
        let theme = context.theme;let tracks = GridTrack.repeat(this.columns.length, GridTrack.flex());let header = new Grid(tracks, 12, null, { width: SizeValue.fill });for (const column of this.columns) header.add(new Text(column, 'label', theme.textSecondary, 1));let table = new Column(0, { width: SizeValue.fill });table.add(new Box(new BoxStyle({ width: SizeValue.fill, padding: EdgeInsets.symmetric(12, 8), borderWidth: 0 }), header));table.add(new Divider());for (let r = 0; r < this.rows.length; r++) {let row = new Grid(tracks, 12, null, { width: SizeValue.fill });let cells = this.rows[r];for (let c = 0; c < this.columns.length; c++) row.add(new Text(c < cells.length ? cells[c] : '', 'bodyM', null, 1));table.add(new Box(new BoxStyle({ width: SizeValue.fill, minHeight: 44, padding: EdgeInsets.symmetric(12, 8), hover: new StyleDiff({ background: theme.surfaceSubtle }) }), row));if (r < this.rows.length - 1) table.add(new Divider());}return table;
    }

}

