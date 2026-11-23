import { $eq, CodeDiffer, CodeDiffGap, CodeDocument, CodeLineChange, CodePatchFile } from "../runtime-exports";

export class CodeDiffSource {
    constructor(original: CodeDocument, originalLineCount: number, modified: CodeDocument, modifiedLineCount: number, changes: CodeLineChange[], originalNumbers: number[] | null, modifiedNumbers: number[] | null, gaps: CodeDiffGap[]) {
        this._originalNumbers = null;
        this._modifiedNumbers = null;
        this.original = null!;
        this.modified = null!;
        this.originalLineCount = 0;
        this.modifiedLineCount = 0;
        this.changes = null!;
        this.gaps = null!;
        this.original = original;
        this.originalLineCount = originalLineCount;
        this.modified = modified;
        this.modifiedLineCount = modifiedLineCount;
        this.changes = changes;
        this._originalNumbers = originalNumbers;
        this._modifiedNumbers = modifiedNumbers;
        this.gaps = gaps;
    }

    _originalNumbers!: number[] | null;
    _modifiedNumbers!: number[] | null;
    original!: CodeDocument;
    modified!: CodeDocument;
    originalLineCount!: number;
    modifiedLineCount!: number;
    changes!: CodeLineChange[];
    gaps!: CodeDiffGap[];

    originalNumber(line: number) {
        let numbers: any; 
        return (numbers = this._originalNumbers) != null ? $eq.collections.item(numbers, line) : line + 1 | 0;
    }

    modifiedNumber(line: number) {
        let numbers: any; 
        return (numbers = this._modifiedNumbers) != null ? $eq.collections.item(numbers, line) : line + 1 | 0;
    }

    static fromTexts(original: string, modified: string) {
        return CodeDiffSource.fromDocuments(CodeDocument.fromText(original), CodeDocument.fromText(modified));
    }

    static fromDocuments(original: CodeDocument, modified: CodeDocument) {
        return new CodeDiffSource(original, original.lineCount, modified, modified.lineCount, CodeDiffer.compare(original, modified), null, null, []);
    }

    originalLineOf(modifiedLine: number) {
        let low = 0;
        let high = $eq.collections.count(this.changes) - 1 | 0;
        let found = -1;
        while (low <= high) {
            let middle = Math.trunc((low + high | 0) / 2);
            if ($eq.collections.item(this.changes, middle).modifiedStart <= modifiedLine) {
                found = middle;
                low = middle + 1 | 0;
            } else high = middle - 1 | 0;
        }
        if (found < 0) return modifiedLine;
        let change = $eq.collections.item(this.changes, found);
        if (modifiedLine < (change.modifiedStart + change.modifiedCount | 0)) return -1;
        return (change.originalStart + change.originalCount | 0) + ((modifiedLine - change.modifiedStart | 0) - change.modifiedCount | 0) | 0;
    }

    static fromPatch(file: CodePatchFile) {
        let originalLines: string[] = [];
        let modifiedLines: string[] = [];
        let originalNumbers: number[] = [];
        let modifiedNumbers: number[] = [];
        let changes: CodeLineChange[] = [];
        let gaps: CodeDiffGap[] = [];
        let originalEnd = 0;
        let modifiedEnd = 0;
        for (const hunk of $eq.collections.sequenceWalk(file.hunks)) {
            let skippedOriginal = hunk.originalStart - originalEnd | 0;
            let skippedModified = hunk.modifiedStart - modifiedEnd | 0;
            if (skippedOriginal > 0 || skippedModified > 0) $eq.collections.listAdd(gaps, new CodeDiffGap(originalLines.length, modifiedLines.length, skippedOriginal, skippedModified, hunk.header));
            let originalNumber = hunk.originalStart;
            let modifiedNumber = hunk.modifiedStart;
            let i = 0;
            while (i < $eq.collections.count(hunk.lines)) {
                if ($eq.collections.item(hunk.lines, i).kind === 'context') {
                    $eq.collections.listAdd(originalLines, $eq.collections.item(hunk.lines, i).text);
                    $eq.collections.listAdd(originalNumbers, originalNumber = originalNumber + 1 | 0);
                    $eq.collections.listAdd(modifiedLines, $eq.collections.item(hunk.lines, i).text);
                    $eq.collections.listAdd(modifiedNumbers, modifiedNumber = modifiedNumber + 1 | 0);
                    i = i + 1 | 0;
                    continue;
                }
                let originalStart = originalLines.length;
                let modifiedStart = modifiedLines.length;
                while (i < $eq.collections.count(hunk.lines) && $eq.collections.item(hunk.lines, i).kind !== 'context') {
                    if ($eq.collections.item(hunk.lines, i).kind === 'removed') {
                        $eq.collections.listAdd(originalLines, $eq.collections.item(hunk.lines, i).text);
                        $eq.collections.listAdd(originalNumbers, originalNumber = originalNumber + 1 | 0);
                    } else {
                        $eq.collections.listAdd(modifiedLines, $eq.collections.item(hunk.lines, i).text);
                        $eq.collections.listAdd(modifiedNumbers, modifiedNumber = modifiedNumber + 1 | 0);
                    }
                    i = i + 1 | 0;
                }
                let originalCount = originalLines.length - originalStart | 0;
                let modifiedCount = modifiedLines.length - modifiedStart | 0;
                $eq.collections.listAdd(changes, new CodeLineChange(originalStart, originalCount, modifiedStart, modifiedCount, CodeDiffer.innerChanges(originalLines, originalStart, originalCount, modifiedLines, modifiedStart, modifiedCount)));
            }
            originalEnd = hunk.originalStart + hunk.originalCount | 0;
            modifiedEnd = hunk.modifiedStart + hunk.modifiedCount | 0;
        }
        return new CodeDiffSource(CodeDocument.fromLines(originalLines), originalLines.length, CodeDocument.fromLines(modifiedLines), modifiedLines.length, changes, originalNumbers, modifiedNumbers, gaps);
    }

    toString(): string {
        return 'eQuantic.UI.Code.CodeDiffSource';
    }
}

