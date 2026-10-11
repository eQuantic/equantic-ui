import { $eq, Box, BoxStyle, BuildContext, Column, Divider, EdgeInsets, Grid, GridTrack, SizeValue, StatelessComponent, StyleDiff, Text } from "../runtime-exports";

export class Table extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.Table';
    declare columns: string[];
    declare rows: string[][];

    constructor(columns?: any, rows?: any, props?: any) {
        super();
        if (columns !== undefined) this.columns = columns;
        if (rows !== undefined) this.rows = rows;
        this.columns = columns;
        this.rows = rows;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let tracks = GridTrack.repeat($eq.collections.count(this.columns), GridTrack.flex());
        let header = new Grid(tracks, 12, null, { width: SizeValue.fill });
        for (const column of this.columns) header.add(new Text(column, 'label', theme.textSecondary, 1));
        let table = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        table.add(new Box(new BoxStyle({ width: SizeValue.fill, padding: EdgeInsets.symmetric(12, 8), borderWidth: 0 }), header));
        table.add(new Divider());
        for (let r = 0; r < $eq.collections.count(this.rows); r++) {
            let row = new Grid(tracks, 12, null, { width: SizeValue.fill });
            let cells = $eq.collections.item(this.rows, r);
            for (let c = 0; c < $eq.collections.count(this.columns); c++) row.add(new Text(c < $eq.collections.count(cells) ? $eq.collections.item(cells, c) : '', 'bodyM', null, 1));
            table.add(new Box(new BoxStyle({ width: SizeValue.fill, minHeight: 44, padding: EdgeInsets.symmetric(12, 8), hover: new StyleDiff({ background: theme.surfaceSubtle }) }), row));
            if (r < $eq.collections.count(this.rows) - 1) table.add(new Divider());
        }
        return table;
    }
}

