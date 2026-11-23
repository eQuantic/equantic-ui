import { $eq, CodeDocument, CodeInnerChange, CodeLineChange, CodePosition, CodeRange } from "../runtime-exports";

export class CodeDiffer {
    static innerTokenLimit: number = 20000;
    static maxRounds: number = 2000;

    static compare(original: CodeDocument, modified: CodeDocument) {
        return CodeDiffer.compareLines(original.lines, modified.lines);
    }

    static compareLines(original: string[], modified: string[]) {
        let ids: any = $eq.collections.dictionary();
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
                i = i + 1 | 0;
                j = j + 1 | 0;
                continue;
            }
            let fromI = i;
            let fromJ = j;
            while (i < a.length && removed[i]) i = i + 1 | 0;
            while (j < b.length && added[j]) j = j + 1 | 0;
            if (i === fromI && j === fromJ) throw $eq.exceptions.create(['System.InvalidOperationException', 'System.SystemException', 'System.Exception'], 'The two sides of the diff lost their alignment.');
            $eq.collections.listAdd(changes, new CodeLineChange(fromI, i - fromI | 0, fromJ, j - fromJ | 0, CodeDiffer.innerChanges(original, fromI, i - fromI | 0, modified, fromJ, j - fromJ | 0)));
        }
        return changes;
    }

    static idsOf(items: string[], ids: any) {
        let result = new Array($eq.collections.count(items)).fill(0);
        let next = ids.size;
        for (let i = 0; i < $eq.collections.count(items); i = i + 1 | 0) {
            let id: any; 
            if (!(($0, $1) => ($0.has($1) ? ((id = $0.get($1)), true) : ((id = 0), false)))(ids, $eq.collections.item(items, i))) {
                id = (($old) => (next = $old + 1 | 0, $old))(next);
                $eq.mapSet(ids, $eq.collections.item(items, i), id);
            }
            result[i] = id;
        }
        return result;
    }

    static diff(a: number[], aLo: number, aHi: number, b: number[], bLo: number, bHi: number, removed: boolean[], added: boolean[]) {
        while (aLo < aHi && bLo < bHi && a[aLo] === b[bLo]) {
            aLo = aLo + 1 | 0;
            bLo = bLo + 1 | 0;
        }
        while (aLo < aHi && bLo < bHi && a[aHi - 1 | 0] === b[bHi - 1 | 0]) {
            aHi = aHi - 1 | 0;
            bHi = bHi - 1 | 0;
        }
        if (aLo === aHi) {
            for (let j = bLo; j < bHi; j = j + 1 | 0) added[j] = true;
            return;
        }
        if (bLo === bHi) {
            for (let i = aLo; i < aHi; i = i + 1 | 0) removed[i] = true;
            return;
        }
        CodeDiffer.bisect(a, aLo, aHi, b, bLo, bHi, removed, added);
    }

    static bisect(a: number[], aLo: number, aHi: number, b: number[], bLo: number, bHi: number, removed: boolean[], added: boolean[]) {
        let n = aHi - aLo | 0;
        let m = bHi - bLo | 0;
        let maxD = Math.trunc(((n + m | 0) + 1 | 0) / 2);
        let reach = Math.min(maxD, CodeDiffer.maxRounds);
        let offset = reach;
        let length = Math.imul(2, reach) + 2 | 0;
        let forward = new Array(length).fill(0);
        let reverse = new Array(length).fill(0);
        for (let k = 0; k < length; k = k + 1 | 0) {
            forward[k] = -1;
            reverse[k] = -1;
        }
        forward[offset + 1 | 0] = 0;
        reverse[offset + 1 | 0] = 0;
        let delta = n - m | 0;
        let front = delta % 2 !== 0;
        let k1Start = 0;
        let k1End = 0;
        let k2Start = 0;
        let k2End = 0;
        for (let d = 0; d < maxD && d < CodeDiffer.maxRounds; d = d + 1 | 0) {
            for (let k1 = (-d | 0) + k1Start | 0; k1 <= (d - k1End | 0); k1 = k1 + 2 | 0) {
                let k1Offset = offset + k1 | 0;
                let x1 = k1 === (-d | 0) || k1 !== d && forward[k1Offset - 1 | 0] < forward[k1Offset + 1 | 0] ? forward[k1Offset + 1 | 0] : forward[k1Offset - 1 | 0] + 1 | 0;
                let y1 = x1 - k1 | 0;
                while (x1 < n && y1 < m && a[aLo + x1 | 0] === b[bLo + y1 | 0]) {
                    x1 = x1 + 1 | 0;
                    y1 = y1 + 1 | 0;
                }
                forward[k1Offset] = x1;
                if (x1 > n) {
                    k1End = k1End + 2 | 0;
                } else if (y1 > m) {
                    k1Start = k1Start + 2 | 0;
                } else if (front) {
                    let k2Offset = (offset + delta | 0) - k1 | 0;
                    if (k2Offset >= 0 && k2Offset < length && reverse[k2Offset] !== -1 && x1 >= (n - reverse[k2Offset] | 0)) {
                        CodeDiffer.diff(a, aLo, aLo + x1 | 0, b, bLo, bLo + y1 | 0, removed, added);
                        CodeDiffer.diff(a, aLo + x1 | 0, aHi, b, bLo + y1 | 0, bHi, removed, added);
                        return;
                    }
                }
            }
            for (let k2 = (-d | 0) + k2Start | 0; k2 <= (d - k2End | 0); k2 = k2 + 2 | 0) {
                let k2Offset = offset + k2 | 0;
                let x2 = k2 === (-d | 0) || k2 !== d && reverse[k2Offset - 1 | 0] < reverse[k2Offset + 1 | 0] ? reverse[k2Offset + 1 | 0] : reverse[k2Offset - 1 | 0] + 1 | 0;
                let y2 = x2 - k2 | 0;
                while (x2 < n && y2 < m && a[(aHi - x2 | 0) - 1 | 0] === b[(bHi - y2 | 0) - 1 | 0]) {
                    x2 = x2 + 1 | 0;
                    y2 = y2 + 1 | 0;
                }
                reverse[k2Offset] = x2;
                if (x2 > n) {
                    k2End = k2End + 2 | 0;
                } else if (y2 > m) {
                    k2Start = k2Start + 2 | 0;
                } else if (!front) {
                    let k1Offset = (offset + delta | 0) - k2 | 0;
                    if (k1Offset >= 0 && k1Offset < length && forward[k1Offset] !== -1) {
                        let x1 = forward[k1Offset];
                        let y1 = x1 - (k1Offset - offset | 0) | 0;
                        if (x1 >= (n - x2 | 0)) {
                            CodeDiffer.diff(a, aLo, aLo + x1 | 0, b, bLo, bLo + y1 | 0, removed, added);
                            CodeDiffer.diff(a, aLo + x1 | 0, aHi, b, bLo + y1 | 0, bHi, removed, added);
                            return;
                        }
                    }
                }
            }
        }
        for (let i = aLo; i < aHi; i = i + 1 | 0) removed[i] = true;
        for (let j = bLo; j < bHi; j = j + 1 | 0) added[j] = true;
    }

    static innerChanges(original: string[], originalStart: number, originalCount: number, modified: string[], modifiedStart: number, modifiedCount: number) {
        if (originalCount === 0 || modifiedCount === 0) return [];
        let aTexts: string[] = [];
        let aLines: number[] = [];
        let aColumns: number[] = [];
        let bTexts: string[] = [];
        let bLines: number[] = [];
        let bColumns: number[] = [];
        if (!CodeDiffer.tokenize(original, originalStart, originalCount, aTexts, aLines, aColumns) || !CodeDiffer.tokenize(modified, modifiedStart, modifiedCount, bTexts, bLines, bColumns)) return [];
        let ids: any = $eq.collections.dictionary();
        let a = CodeDiffer.idsOf(aTexts, ids);
        let b = CodeDiffer.idsOf(bTexts, ids);
        let removed = new Array(a.length).fill(false);
        let added = new Array(b.length).fill(false);
        CodeDiffer.diff(a, 0, a.length, b, 0, b.length, removed, added);
        let originalEnd = new CodePosition((originalStart + originalCount | 0) - 1 | 0, $eq.collections.item(original, (originalStart + originalCount | 0) - 1 | 0).length);
        let modifiedEnd = new CodePosition((modifiedStart + modifiedCount | 0) - 1 | 0, $eq.collections.item(modified, (modifiedStart + modifiedCount | 0) - 1 | 0).length);
        let inner: CodeInnerChange[] = [];
        let i = 0;
        let j = 0;
        while (i < a.length || j < b.length) {
            if (i < a.length && j < b.length && !removed[i] && !added[j]) {
                i = i + 1 | 0;
                j = j + 1 | 0;
                continue;
            }
            let fromI = i;
            let fromJ = j;
            while (i < a.length && removed[i]) i = i + 1 | 0;
            while (j < b.length && added[j]) j = j + 1 | 0;
            if (i === fromI && j === fromJ) throw $eq.exceptions.create(['System.InvalidOperationException', 'System.SystemException', 'System.Exception'], 'The two sides of the diff lost their alignment.');
            $eq.collections.listAdd(inner, new CodeInnerChange(CodeDiffer.span(aTexts, aLines, aColumns, fromI, i, originalEnd), CodeDiffer.span(bTexts, bLines, bColumns, fromJ, j, modifiedEnd)));
        }
        return inner;
    }

    static tokenize(lines: string[], start: number, count: number, texts: string[], tokenLines: number[], tokenColumns: number[]) {
        for (let line = start; line < (start + count | 0); line = line + 1 | 0) {
            if (line > start) {
                $eq.collections.listAdd(texts, '\n');
                $eq.collections.listAdd(tokenLines, line - 1 | 0);
                $eq.collections.listAdd(tokenColumns, $eq.collections.item(lines, line - 1 | 0).length);
                if (texts.length > CodeDiffer.innerTokenLimit) return false;
            }
            let text = $eq.collections.item(lines, line);
            let column = 0;
            while (column < text.length) {
                let begin = column;
                if (CodeDocument.isWordChar(text[column])) {
                    while (column < text.length && CodeDocument.isWordChar(text[column])) column = column + 1 | 0;
                } else if ($eq.text.isWhiteSpace(text[column])) {
                    while (column < text.length && $eq.text.isWhiteSpace(text[column])) column = column + 1 | 0;
                } else {
                    column = column + ((column + 1 | 0) < text.length && (/^[\uD800-\uDBFF]$/.test(text[column]) && /^[\uDC00-\uDFFF]$/.test(text[column + 1 | 0])) ? 2 : 1) | 0;
                }
                $eq.collections.listAdd(texts, $eq.text.substring(text, begin, column - begin | 0));
                $eq.collections.listAdd(tokenLines, line);
                $eq.collections.listAdd(tokenColumns, begin);
                if (texts.length > CodeDiffer.innerTokenLimit) return false;
            }
        }
        return true;
    }

    static span(texts: string[], lines: number[], columns: number[], from: number, to: number, end: CodePosition) {
        let start = from < texts.length ? new CodePosition(lines[from], columns[from]) : end;
        if (to === from) return new CodeRange(start);
        let last = to - 1 | 0;
        let finish = texts[last] === '\n' ? new CodePosition(lines[last] + 1 | 0, 0) : new CodePosition(lines[last], columns[last] + texts[last].length | 0);
        return new CodeRange(start, finish);
    }
}

