import { $eq, CellRef, SheetDocument, SheetRange } from "../runtime-exports";

export class TsvCodec {
    static serialize(document: SheetDocument, range: SheetRange) {
        let rows: string[] = [];
        for (let row = range.topRow; row <= range.bottomRow; row++) {
            let cells: string[] = [];
            for (let col = range.leftCol; col <= range.rightCol; col++) $eq.collections.listAdd(cells, TsvCodec.escape(document.getCell(new CellRef(row, col))));
            $eq.collections.listAdd(rows, $eq.text.join('\t', cells));
        }
        return $eq.text.join('\n', rows);
    }

    static escape(value: string) {
        if (value.indexOf('\t') < 0 && value.indexOf('\n') < 0 && value.indexOf('"') < 0) return value;
        return '"' + $eq.text.replace(value, '"', '""', 'ordinal') + '"';
    }

    static parse(text: string) {
        let rows: string[][] = [];
        let row: string[] = [];
        let cell = '';
        let quoted = false;
        let i = 0;
        while (i < text.length) {
            let ch = text[i];
            if (quoted) {
                if (ch === '"') {
                    if (i + 1 < text.length && text[i + 1] === '"') {
                        cell += '"';
                        i += 2;
                        continue;
                    }
                    quoted = false;
                    i++;
                    continue;
                }
                cell += ch;
                i++;
                continue;
            }
            if (ch === '"' && cell.length === 0) {
                quoted = true;
                i++;
                continue;
            }
            if (ch === '\t') {
                $eq.collections.listAdd(row, cell);
                cell = '';
                i++;
                continue;
            }
            if (ch === '\r') {
                i++;
                continue;
            }
            if (ch === '\n') {
                $eq.collections.listAdd(row, cell);
                $eq.collections.listAdd(rows, row);
                row = [];
                cell = '';
                i++;
                continue;
            }
            cell += ch;
            i++;
        }
        if (cell.length > 0 || row.length > 0) {
            $eq.collections.listAdd(row, cell);
            $eq.collections.listAdd(rows, row);
        }
        return rows;
    }
}

