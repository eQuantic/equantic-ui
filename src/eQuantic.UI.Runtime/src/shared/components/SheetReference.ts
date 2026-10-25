import { $eq, CellRef, SheetRange } from "../runtime-exports";

export class SheetReference {
    static maxRows: number = 1048576;

    static tryParse(text: string, rows: number, cols: number) {
        let range: any;
        const $r = (() => {
            range = new SheetRange(new CellRef(0, 0));
            let trimmed = $eq.text.toUpper($eq.text.trim(text), true);
            if (trimmed.length === 0) return false;
            let colon = trimmed.indexOf(':');
            let first = colon < 0 ? trimmed : $eq.text.substring(trimmed, 0, colon);
            let second = colon < 0 ? null : $eq.text.substring(trimmed, colon + 1);
            if (second == null) {
                let cell: any; 
                if (!($o => (cell = $o.cell, $o.$))(SheetReference.readCell(first)) || !SheetReference.inside(cell, rows, cols)) return false;
                range = new SheetRange(cell);
                return true;
            }
            let from: any; let to: any; 
            if (($o => (from = $o.cell, $o.$))(SheetReference.readCell(first)) && ($o => (to = $o.cell, $o.$))(SheetReference.readCell(second))) {
                if (!SheetReference.inside(from, rows, cols) || !SheetReference.inside(to, rows, cols)) return false;
                range = SheetReference.ordered(from, to);
                return true;
            }
            let top: any; let bottom: any; 
            if (($o => (top = $o.row, $o.$))(SheetReference.readRow(first)) && ($o => (bottom = $o.row, $o.$))(SheetReference.readRow(second))) {
                if (top >= rows || bottom >= rows) return false;
                range = SheetReference.ordered(new CellRef(top, 0), new CellRef(bottom, cols - 1));
                return true;
            }
            let left: any; let right: any; 
            if (($o => (left = $o.col, $o.$))(SheetReference.readColumn(first)) && ($o => (right = $o.col, $o.$))(SheetReference.readColumn(second))) {
                if (left >= cols || right >= cols) return false;
                range = SheetReference.ordered(new CellRef(0, left), new CellRef(rows - 1, right));
                return true;
            }
            return false;
        })();
        return { $: $r, range };
    }

    static ordered(a: CellRef, b: CellRef) {
        return new SheetRange(new CellRef(Math.min(a.row, b.row), Math.min(a.col, b.col)), new CellRef(Math.max(a.row, b.row), Math.max(a.col, b.col)));
    }

    static inside(cell: CellRef, rows: number, cols: number) {
        return cell.row < rows && cell.col < cols;
    }

    static readCell(text: string) {
        let cell: any;
        const $r = (() => {
            cell = new CellRef(0, 0);
            let at = 0;
            let col: any; 
            if (!($o => (at = $o.at, col = $o.col, $o.$))(SheetReference.letters(text, at))) return false;
            let row: any; 
            if (!($o => (at = $o.at, row = $o.row, $o.$))(SheetReference.digits(text, at))) return false;
            if (at !== text.length) return false;
            cell = new CellRef(row, col);
            return true;
        })();
        return { $: $r, cell };
    }

    static readRow(text: string) {
        let row: any;
        const $r = (() => {
            let at = 0;
            return ($o => (at = $o.at, row = $o.row, $o.$))(SheetReference.digits(text, at)) && at === text.length;
        })();
        return { $: $r, row };
    }

    static readColumn(text: string) {
        let col: any;
        const $r = (() => {
            let at = 0;
            return ($o => (at = $o.at, col = $o.col, $o.$))(SheetReference.letters(text, at)) && at === text.length;
        })();
        return { $: $r, col };
    }

    static letters(text: string, at: number) {
        let col: any;
        const $r = (() => {
            col = -1;
            if (at < text.length && text[at] === '$') at++;
            let start = at;
            let number = 0;
            while (at < text.length && text[at] >= 'A' && text[at] <= 'Z') {
                if (at - start === 3) return false;
                number = number * 26 + (text[at].charCodeAt(0) - 65 + 1);
                at++;
            }
            if (at === start) return false;
            col = number - 1;
            return true;
        })();
        return { $: $r, at, col };
    }

    static digits(text: string, at: number) {
        let row: any;
        const $r = (() => {
            row = -1;
            if (at < text.length && text[at] === '$') at++;
            let start = at;
            let number = 0;
            while (at < text.length && text[at] >= '0' && text[at] <= '9') {
                number = number * 10 + (text[at].charCodeAt(0) - 48);
                if (number > SheetReference.maxRows) return false;
                at++;
            }
            if (at === start || number === 0) return false;
            row = number - 1;
            return true;
        })();
        return { $: $r, at, row };
    }
}

