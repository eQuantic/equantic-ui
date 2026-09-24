import { $eq, CodeDocument, CodeInnerChange, CodeLineChange, CodePosition, CodeRange } from "../runtime-exports";

export class CodeDiffer {
    static _innerTokenLimit: number | undefined;

    static get innerTokenLimit(): number {
        return CodeDiffer._innerTokenLimit ??= 20_000;
    }

    static _maxRounds: number | undefined;

    static get maxRounds(): number {
        return CodeDiffer._maxRounds ??= 2_000;
    }

    static compare(original: CodeDocument, modified: CodeDocument) {
        return CodeDiffer.compareLines(original.lines, modified.lines);
    }

    static compareLines(original: string[], modified: string[]) {
        let ids: Record<string, number> = {};
        let a = CodeDiffer.idsOf(original, ids);
        let b = CodeDiffer.idsOf(modified, ids);
        let removed = new Array(a.length).fill(false);
        let added = new Array(b.length).fill(false);
        CodeDiffer.diff(a, 0, a.length, b, 0, b.length, removed, added);
        let changes: CodeLineChange[] = [];
        let i = 0;
        let j = 0;
        while (i < a.length || j < b.length) {
            if (i < a.length && j < b.length && !removed[i] && !added[j]) {
                i++;
                j++;
                continue;
            }
            let fromI = i;
            let fromJ = j;
            while (i < a.length && removed[i]) i++;
            while (j < b.length && added[j]) j++;
            if (i === fromI && j === fromJ) throw new Error('The two sides of the diff lost their alignment.');
            changes.push(new CodeLineChange(fromI, i - fromI, fromJ, j - fromJ, CodeDiffer.innerChanges(original, fromI, i - fromI, modified, fromJ, j - fromJ)));
        }
        return changes;
    }

    static idsOf(items: string[], ids: Record<string, any>) {
        let id: any;
        let result = new Array(items.length).fill(0);
        let next = Object.keys(ids).length;
        for (let i = 0; i < items.length; i++) {
            if (!(($0: any, $1: any) => (Object.prototype.hasOwnProperty.call($0, $1) ? ((id = $0[$1]), true) : ((id = 0), false)))(ids, items[i])) {
                id = next++;
                ids[items[i]] = id;
            }
            result[i] = id;
        }
        return result;
    }

    static diff(a: number[], aLo: number, aHi: number, b: number[], bLo: number, bHi: number, removed: boolean[], added: boolean[]) {
        while (aLo < aHi && bLo < bHi && a[aLo] === b[bLo]) {
            aLo++;
            bLo++;
        }
        while (aLo < aHi && bLo < bHi && a[aHi - 1] === b[bHi - 1]) {
            aHi--;
            bHi--;
        }
        if (aLo === aHi) {
            for (let j = bLo; j < bHi; j++) added[j] = true;
            return;
        }
        if (bLo === bHi) {
            for (let i = aLo; i < aHi; i++) removed[i] = true;
            return;
        }
        CodeDiffer.bisect(a, aLo, aHi, b, bLo, bHi, removed, added);
    }

    static bisect(a: number[], aLo: number, aHi: number, b: number[], bLo: number, bHi: number, removed: boolean[], added: boolean[]) {
        let n = aHi - aLo;
        let m = bHi - bLo;
        let maxD = Math.trunc((n + m + 1) / 2);
        let reach = Math.min(maxD, CodeDiffer.maxRounds);
        let offset = reach;
        let length = 2 * reach + 2;
        let forward = new Array(length).fill(0);
        let reverse = new Array(length).fill(0);
        for (let k = 0; k < length; k++) {
            forward[k] = -1;
            reverse[k] = -1;
        }
        forward[offset + 1] = 0;
        reverse[offset + 1] = 0;
        let delta = n - m;
        let front = delta % 2 !== 0;
        let k1Start = 0;
        let k1End = 0;
        let k2Start = 0;
        let k2End = 0;
        for (let d = 0; d < maxD && d < CodeDiffer.maxRounds; d++) {
            for (let k1 = -d + k1Start; k1 <= d - k1End; k1 += 2) {
                let k1Offset = offset + k1;
                let x1 = k1 === -d || k1 !== d && forward[k1Offset - 1] < forward[k1Offset + 1] ? forward[k1Offset + 1] : forward[k1Offset - 1] + 1;
                let y1 = x1 - k1;
                while (x1 < n && y1 < m && a[aLo + x1] === b[bLo + y1]) {
                    x1++;
                    y1++;
                }
                forward[k1Offset] = x1;
                if (x1 > n) {
                    k1End += 2;
                } else if (y1 > m) {
                    k1Start += 2;
                } else if (front) {
                    let k2Offset = offset + delta - k1;
                    if (k2Offset >= 0 && k2Offset < length && reverse[k2Offset] !== -1 && x1 >= n - reverse[k2Offset]) {
                        CodeDiffer.diff(a, aLo, aLo + x1, b, bLo, bLo + y1, removed, added);
                        CodeDiffer.diff(a, aLo + x1, aHi, b, bLo + y1, bHi, removed, added);
                        return;
                    }
                }
            }
            for (let k2 = -d + k2Start; k2 <= d - k2End; k2 += 2) {
                let k2Offset = offset + k2;
                let x2 = k2 === -d || k2 !== d && reverse[k2Offset - 1] < reverse[k2Offset + 1] ? reverse[k2Offset + 1] : reverse[k2Offset - 1] + 1;
                let y2 = x2 - k2;
                while (x2 < n && y2 < m && a[aHi - x2 - 1] === b[bHi - y2 - 1]) {
                    x2++;
                    y2++;
                }
                reverse[k2Offset] = x2;
                if (x2 > n) {
                    k2End += 2;
                } else if (y2 > m) {
                    k2Start += 2;
                } else if (!front) {
                    let k1Offset = offset + delta - k2;
                    if (k1Offset >= 0 && k1Offset < length && forward[k1Offset] !== -1) {
                        let x1 = forward[k1Offset];
                        let y1 = x1 - (k1Offset - offset);
                        if (x1 >= n - x2) {
                            CodeDiffer.diff(a, aLo, aLo + x1, b, bLo, bLo + y1, removed, added);
                            CodeDiffer.diff(a, aLo + x1, aHi, b, bLo + y1, bHi, removed, added);
                            return;
                        }
                    }
                }
            }
        }
        for (let i = aLo; i < aHi; i++) removed[i] = true;
        for (let j = bLo; j < bHi; j++) added[j] = true;
    }

    static innerChanges(original: string[], originalStart: number, originalCount: number, modified: string[], modifiedStart: number, modifiedCount: number) {
        if (originalCount === 0 || modifiedCount === 0) return [];
        let aTexts: string[] = [];
        let aLines: number[] = [];
        let aColumns: number[] = [];
        CodeDiffer.tokenize(original, originalStart, originalCount, aTexts, aLines, aColumns);
        let bTexts: string[] = [];
        let bLines: number[] = [];
        let bColumns: number[] = [];
        CodeDiffer.tokenize(modified, modifiedStart, modifiedCount, bTexts, bLines, bColumns);
        if (aTexts.length > CodeDiffer.innerTokenLimit || bTexts.length > CodeDiffer.innerTokenLimit) return [];
        let ids: Record<string, number> = {};
        let a = CodeDiffer.idsOf(aTexts, ids);
        let b = CodeDiffer.idsOf(bTexts, ids);
        let removed = new Array(a.length).fill(false);
        let added = new Array(b.length).fill(false);
        CodeDiffer.diff(a, 0, a.length, b, 0, b.length, removed, added);
        let originalEnd = new CodePosition(originalStart + originalCount - 1, original[originalStart + originalCount - 1].length);
        let modifiedEnd = new CodePosition(modifiedStart + modifiedCount - 1, modified[modifiedStart + modifiedCount - 1].length);
        let inner: CodeInnerChange[] = [];
        let i = 0;
        let j = 0;
        while (i < a.length || j < b.length) {
            if (i < a.length && j < b.length && !removed[i] && !added[j]) {
                i++;
                j++;
                continue;
            }
            let fromI = i;
            let fromJ = j;
            while (i < a.length && removed[i]) i++;
            while (j < b.length && added[j]) j++;
            if (i === fromI && j === fromJ) throw new Error('The two sides of the diff lost their alignment.');
            inner.push(new CodeInnerChange(CodeDiffer.span(aTexts, aLines, aColumns, fromI, i, originalEnd), CodeDiffer.span(bTexts, bLines, bColumns, fromJ, j, modifiedEnd)));
        }
        return inner;
    }

    static tokenize(lines: string[], start: number, count: number, texts: string[], tokenLines: number[], tokenColumns: number[]) {
        for (let line = start; line < start + count; line++) {
            if (line > start) {
                texts.push('\n');
                tokenLines.push(line - 1);
                tokenColumns.push(lines[line - 1].length);
            }
            let text = lines[line];
            let column = 0;
            while (column < text.length) {
                let begin = column;
                if (CodeDocument.isWordChar(text[column])) {
                    while (column < text.length && CodeDocument.isWordChar(text[column])) column++;
                } else if ($eq.text.isWhiteSpace(text[column])) {
                    while (column < text.length && $eq.text.isWhiteSpace(text[column])) column++;
                } else {
                    column += column + 1 < text.length && (/^[\uD800-\uDBFF]$/.test(text[column]) && /^[\uDC00-\uDFFF]$/.test(text[column + 1])) ? 2 : 1;
                }
                texts.push($eq.text.substring(text, begin, column - begin));
                tokenLines.push(line);
                tokenColumns.push(begin);
            }
        }
    }

    static span(texts: string[], lines: number[], columns: number[], from: number, to: number, end: CodePosition) {
        let start = from < texts.length ? new CodePosition(lines[from], columns[from]) : end;
        if (to === from) return new CodeRange(start);
        let last = to - 1;
        let finish = texts[last] === '\n' ? new CodePosition(lines[last] + 1, 0) : new CodePosition(lines[last], columns[last] + texts[last].length);
        return new CodeRange(start, finish);
    }
}

