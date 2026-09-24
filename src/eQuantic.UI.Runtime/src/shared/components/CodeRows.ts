import { CodeCollapse, CodeFiller, CodeRow, CodeRowKindValue } from "../runtime-exports";

export class CodeRows {
    constructor(lineCount: number, fillers: CodeFiller[], collapses: CodeCollapse[], props?: any) {
        this._kinds = [];
        this._lines = [];
        this._lineCounts = [];
        this._rows = [];
        this._rowCounts = [];
        this._sources = [];
        this._labels = [];
        this.lineCount = Math.max(0, lineCount);
        let sortedFillers = [...fillers.filter((filler) => filler.rows > 0)].sort((a, b) => { { const _k: (x: typeof a) => any = (filler) => filler.beforeLine; const _a = _k(a), _b = _k(b); if (_a < _b) return -1; if (_a > _b) return 1; } return 0; });
        let sortedCollapses = [...collapses.filter((collapse) => collapse.lastLine >= collapse.firstLine)].sort((a, b) => { { const _k: (x: typeof a) => any = (collapse) => collapse.firstLine; const _a = _k(a), _b = _k(b); if (_a < _b) return -1; if (_a > _b) return 1; } return 0; });
        let line = 0;
        let row = 0;
        let f = 0;
        let c = 0;
        while (f < sortedFillers.length || c < sortedCollapses.length) {
            let takeFiller = f < sortedFillers.length && (c >= sortedCollapses.length || sortedFillers[f].beforeLine <= sortedCollapses[c].firstLine);
            let at = takeFiller ? sortedFillers[f].beforeLine : sortedCollapses[c].firstLine;
            if (at < line || at > this.lineCount) throw new Error(`A filler or a collapse at line ${at} overlaps a collapse, or lies outside the ${this.lineCount} lines.`);
            if (at > line) {
                this.add('line', line, at - line, row, at - line, -1, null);
                row += at - line;
                line = at;
            }
            if (takeFiller) {
                let filler = sortedFillers[f++];
                this.add('filler', filler.beforeLine, 0, row, filler.rows, filler.sourceLine, filler.label);
                row += filler.rows;
            } else {
                let collapse = sortedCollapses[c++];
                let last = Math.min(collapse.lastLine, this.lineCount - 1);
                let rows = collapse.placeholder ? 1 : 0;
                this.add('placeholder', collapse.firstLine, last - collapse.firstLine + 1, row, rows, -1, collapse.label);
                row += rows;
                line = last + 1;
            }
        }
        if (line < this.lineCount) {
            this.add('line', line, this.lineCount - line, row, this.lineCount - line, -1, null);
            row += this.lineCount - line;
        }
        this.rowCount = row;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    _kinds: CodeRowKindValue[];
    _lines: number[];
    _lineCounts: number[];
    _rows: number[];
    _rowCounts: number[];
    _sources: number[];
    _labels: (string | null)[];
    lineCount: number = 0;
    rowCount: number = 0;

    add(kind: CodeRowKindValue, line: number, lineCount: number, row: number, rowCount: number, source: number, label: string | null) {
        this._kinds.push(kind);
        this._lines.push(line);
        this._lineCounts.push(lineCount);
        this._rows.push(row);
        this._rowCounts.push(rowCount);
        this._sources.push(source);
        this._labels.push(label);
    }

    rowOf(line: number) {
        if (this.lineCount === 0) return 0;
        let target = Math.min(Math.max(line, 0), this.lineCount - 1);
        let segment = this.segmentOfLine(target);
        if (this._kinds[segment] === 'line') return this._rows[segment] + (target - this._lines[segment]);
        if (this._rowCounts[segment] > 0) return this._rows[segment];
        return Math.max(0, this._rows[segment] - 1);
    }

    isVisible(line: number) {
        if (line < 0 || line >= this.lineCount) return false;
        return this._kinds[this.segmentOfLine(line)] === 'line';
    }

    rowAt(row: number) {
        if (this.rowCount === 0) return new CodeRow('filler', 0);
        let target = Math.min(Math.max(row, 0), this.rowCount - 1);
        let segment = this.segmentOfRow(target);
        let offset = target - this._rows[segment];
        return (() => { const _s = this._kinds[segment]; if (_s === 'line') return new CodeRow('line', this._lines[segment] + offset); if (_s === 'filler') return new CodeRow('filler', this._lines[segment], 1, this._sources[segment] >= 0 ? this._sources[segment] + offset : -1, this._labels[segment]); return new CodeRow('placeholder', this._lines[segment], this._lineCounts[segment], -1, this._labels[segment]); })();
    }

    lineAtRow(row: number) {
        let shown = this.rowAt(row);
        return Math.max(0, Math.min(shown.line, this.lineCount - 1));
    }

    segmentOfLine(line: number) {
        let low = 0;
        let high = this._kinds.length - 1;
        let found = 0;
        while (low <= high) {
            let middle = Math.trunc((low + high) / 2);
            if (this._lines[middle] <= line) {
                if (this._lineCounts[middle] > 0) found = middle;
                low = middle + 1;
            } else high = middle - 1;
        }
        while (found > 0 && (this._lineCounts[found] === 0 || this._lines[found] > line)) found--;
        while (found + 1 < this._kinds.length && this._lineCounts[found + 1] > 0 && this._lines[found + 1] <= line) found++;
        return found;
    }

    segmentOfRow(row: number) {
        let low = 0;
        let high = this._kinds.length - 1;
        let found = 0;
        while (low <= high) {
            let middle = Math.trunc((low + high) / 2);
            if (this._rows[middle] <= row) {
                if (this._rowCounts[middle] > 0) found = middle;
                low = middle + 1;
            } else high = middle - 1;
        }
        while (found > 0 && this._rowCounts[found] === 0) found--;
        return found;
    }
}

