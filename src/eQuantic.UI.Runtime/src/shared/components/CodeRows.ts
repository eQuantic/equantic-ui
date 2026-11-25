import { $CodeRowKindValue, $eq, CodeCollapse, CodeFiller, CodeRow } from "../runtime-exports";

export class CodeRows {
    constructor(lineCount: number, fillers: CodeFiller[], collapses: CodeCollapse[]) {
        this._kinds = [];
        this._lines = [];
        this._lineCounts = [];
        this._rows = [];
        this._rowCounts = [];
        this._sources = [];
        this._labels = [];
        this.lineCount = 0;
        this.rowCount = 0;
        this.lineCount = Math.max(0, lineCount);
        let sortedFillers = $eq.linq.toArray($eq.query.defer(($src) => [...$src].sort(($a, $b) => { { const $k: ($element: typeof $a) => any = ((filler) => filler.beforeLine); const $x = $k($a), $y = $k($b); if ($x < $y) return -1; if ($x > $y) return 1; } return 0; }), $eq.query.where(fillers, (filler) => filler.rows > 0)));
        let sortedCollapses = $eq.linq.toArray($eq.query.defer(($src) => [...$src].sort(($a, $b) => { { const $k: ($element: typeof $a) => any = ((collapse) => collapse.firstLine); const $x = $k($a), $y = $k($b); if ($x < $y) return -1; if ($x > $y) return 1; } return 0; }), $eq.query.where(collapses, (collapse) => collapse.lastLine >= collapse.firstLine)));
        let line = 0;
        let row = 0;
        let f = 0;
        let c = 0;
        while (f < sortedFillers.length || c < sortedCollapses.length) {
            let takeFiller = f < sortedFillers.length && (c >= sortedCollapses.length || sortedFillers[f].beforeLine <= sortedCollapses[c].firstLine);
            let at = takeFiller ? sortedFillers[f].beforeLine : sortedCollapses[c].firstLine;
            if (at < line || at > this.lineCount) throw $eq.exceptions.create(['System.ArgumentException', 'System.SystemException', 'System.Exception'], `A filler or a collapse at line ${$eq.text.format(at, null, undefined, undefined, 'int32')} overlaps a collapse, or lies outside the ${$eq.text.format(this.lineCount, null, undefined, undefined, 'int32')} lines.`);
            if (at > line) {
                this.add('line', line, at - line | 0, row, at - line | 0, -1, null);
                row = row + (at - line | 0) | 0;
                line = at;
            }
            if (takeFiller) {
                let filler = sortedFillers[(($old) => (f = $old + 1 | 0, $old))(f)];
                this.add('filler', filler.beforeLine, 0, row, filler.rows, filler.sourceLine, filler.label);
                row = row + filler.rows | 0;
            } else {
                let collapse = sortedCollapses[(($old) => (c = $old + 1 | 0, $old))(c)];
                let last = Math.min(collapse.lastLine, this.lineCount - 1 | 0);
                let rows = collapse.placeholder ? 1 : 0;
                this.add('placeholder', collapse.firstLine, (last - collapse.firstLine | 0) + 1 | 0, row, rows, -1, collapse.label);
                row = row + rows | 0;
                line = last + 1 | 0;
            }
        }
        if (line < this.lineCount) {
            this.add('line', line, this.lineCount - line | 0, row, this.lineCount - line | 0, -1, null);
            row = row + (this.lineCount - line | 0) | 0;
        }
        this.rowCount = row;
    }

    _kinds!: $CodeRowKindValue[];
    _lines!: number[];
    _lineCounts!: number[];
    _rows!: number[];
    _rowCounts!: number[];
    _sources!: number[];
    _labels!: (string | null)[];
    lineCount!: number;
    rowCount!: number;

    add(kind: $CodeRowKindValue, line: number, lineCount: number, row: number, rowCount: number, source: number, label: string | null) {
        $eq.collections.listAdd(this._kinds, kind);
        $eq.collections.listAdd(this._lines, line);
        $eq.collections.listAdd(this._lineCounts, lineCount);
        $eq.collections.listAdd(this._rows, row);
        $eq.collections.listAdd(this._rowCounts, rowCount);
        $eq.collections.listAdd(this._sources, source);
        $eq.collections.listAdd(this._labels, label);
    }

    rowOf(line: number) {
        if (this.lineCount === 0) return 0;
        let target = $eq.num.clamp(line, 0, this.lineCount - 1 | 0, 'int32');
        let segment = this.segmentOfLine(target);
        if (this._kinds[segment] === 'line') return this._rows[segment] + (target - this._lines[segment] | 0) | 0;
        if (this._rowCounts[segment] > 0) return this._rows[segment];
        return Math.max(0, this._rows[segment] - 1 | 0);
    }

    isVisible(line: number) {
        if (line < 0 || line >= this.lineCount) return false;
        return this._kinds[this.segmentOfLine(line)] === 'line';
    }

    rowAt(row: number) {
        if (this.rowCount === 0) return new CodeRow('filler', 0);
        let target = $eq.num.clamp(row, 0, this.rowCount - 1 | 0, 'int32');
        let segment = this.segmentOfRow(target);
        let offset = target - this._rows[segment] | 0;
        return (() => { const $s = this._kinds[segment]; if ($s === 'line') return new CodeRow('line', this._lines[segment] + offset | 0); if ($s === 'filler') return new CodeRow('filler', this._lines[segment], 1, this._sources[segment] >= 0 ? this._sources[segment] + offset | 0 : -1, this._labels[segment]); return new CodeRow('placeholder', this._lines[segment], this._lineCounts[segment], undefined, this._labels[segment]); })();
    }

    lineAtRow(row: number) {
        let shown = this.rowAt(row);
        return Math.max(0, Math.min(shown.line, this.lineCount - 1 | 0));
    }

    linesIn(first: number, last: number): [number, number] {
        return last < first ? [0, -1] : [this.lineAtRow(first), this.lineAtRow(last)];
    }

    sourceLinesIn(first: number, last: number): [number, number] {
        let lowest = 2147483647;
        let highest = -1;
        for (let row = first; row <= last; row = row + 1 | 0) {
            let shown = this.rowAt(row);
            if (shown.kind !== 'filler' || shown.sourceLine < 0) continue;
            lowest = Math.min(lowest, shown.sourceLine);
            highest = Math.max(highest, shown.sourceLine);
        }
        return highest < 0 ? [0, -1] : [lowest, highest];
    }

    segmentOfLine(line: number) {
        let low = 0;
        let high = this._kinds.length - 1 | 0;
        let found = 0;
        while (low <= high) {
            let middle = Math.trunc((low + high | 0) / 2);
            if (this._lines[middle] <= line) {
                if (this._lineCounts[middle] > 0) found = middle;
                low = middle + 1 | 0;
            } else high = middle - 1 | 0;
        }
        while (found > 0 && (this._lineCounts[found] === 0 || this._lines[found] > line)) found = found - 1 | 0;
        while ((found + 1 | 0) < this._kinds.length && this._lineCounts[found + 1 | 0] > 0 && this._lines[found + 1 | 0] <= line) found = found + 1 | 0;
        return found;
    }

    segmentOfRow(row: number) {
        let low = 0;
        let high = this._kinds.length - 1 | 0;
        let found = 0;
        while (low <= high) {
            let middle = Math.trunc((low + high | 0) / 2);
            if (this._rows[middle] <= row) {
                if (this._rowCounts[middle] > 0) found = middle;
                low = middle + 1 | 0;
            } else high = middle - 1 | 0;
        }
        while (found > 0 && this._rowCounts[found] === 0) found = found - 1 | 0;
        return found;
    }

    toString(): string {
        return 'eQuantic.UI.Code.CodeRows';
    }
}

