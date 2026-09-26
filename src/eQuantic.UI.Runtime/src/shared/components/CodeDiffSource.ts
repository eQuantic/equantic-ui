import { CodeDiffer, CodeDiffGap, CodeDocument, CodeLineChange, CodePatchFile } from "../runtime-exports";

export class CodeDiffSource {
    constructor(original: CodeDocument, originalLineCount: number, modified: CodeDocument, modifiedLineCount: number, changes: CodeLineChange[], originalNumbers: number[] | null, modifiedNumbers: number[] | null, gaps: CodeDiffGap[], props?: any) {
        this._originalNumbers = null;
        this._modifiedNumbers = null;
        this.original = original;
        this.originalLineCount = originalLineCount;
        this.modified = modified;
        this.modifiedLineCount = modifiedLineCount;
        this.changes = changes;
        this._originalNumbers = originalNumbers;
        this._modifiedNumbers = modifiedNumbers;
        this.gaps = gaps;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    _originalNumbers: number[] | null;
    _modifiedNumbers: number[] | null;
    declare original: CodeDocument;
    declare modified: CodeDocument;
    originalLineCount: number = 0;
    modifiedLineCount: number = 0;
    declare changes: CodeLineChange[];
    declare gaps: CodeDiffGap[];

    originalNumber(line: number) {
        let numbers: any; return (numbers = this._originalNumbers) != null ? numbers[line] : line + 1;
    }

    modifiedNumber(line: number) {
        let numbers: any; return (numbers = this._modifiedNumbers) != null ? numbers[line] : line + 1;
    }

    static fromTexts(original: string, modified: string) {
        return CodeDiffSource.fromDocuments(CodeDocument.fromText(original), CodeDocument.fromText(modified));
    }

    static fromDocuments(original: CodeDocument, modified: CodeDocument) {
        return new CodeDiffSource(original, original.lineCount, modified, modified.lineCount, CodeDiffer.compare(original, modified), null, null, []);
    }

    originalLineOf(modifiedLine: number) {
        let low = 0;
        let high = this.changes.length - 1;
        let found = -1;
        while (low <= high) {
            let middle = Math.trunc((low + high) / 2);
            if (this.changes[middle].modifiedStart <= modifiedLine) {
                found = middle;
                low = middle + 1;
            } else high = middle - 1;
        }
        if (found < 0) return modifiedLine;
        let change = this.changes[found];
        if (modifiedLine < change.modifiedStart + change.modifiedCount) return -1;
        return change.originalStart + change.originalCount + (modifiedLine - change.modifiedStart - change.modifiedCount);
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
        for (const hunk of file.hunks) {
            let skippedOriginal = hunk.originalStart - originalEnd;
            let skippedModified = hunk.modifiedStart - modifiedEnd;
            if (skippedOriginal > 0 || skippedModified > 0) gaps.push(new CodeDiffGap(originalLines.length, modifiedLines.length, skippedOriginal, skippedModified, hunk.header));
            let originalNumber = hunk.originalStart;
            let modifiedNumber = hunk.modifiedStart;
            let i = 0;
            while (i < hunk.lines.length) {
                if (hunk.lines[i].kind === 'context') {
                    originalLines.push(hunk.lines[i].text);
                    originalNumbers.push(++originalNumber);
                    modifiedLines.push(hunk.lines[i].text);
                    modifiedNumbers.push(++modifiedNumber);
                    i++;
                    continue;
                }
                let originalStart = originalLines.length;
                let modifiedStart = modifiedLines.length;
                while (i < hunk.lines.length && hunk.lines[i].kind !== 'context') {
                    if (hunk.lines[i].kind === 'removed') {
                        originalLines.push(hunk.lines[i].text);
                        originalNumbers.push(++originalNumber);
                    } else {
                        modifiedLines.push(hunk.lines[i].text);
                        modifiedNumbers.push(++modifiedNumber);
                    }
                    i++;
                }
                let originalCount = originalLines.length - originalStart;
                let modifiedCount = modifiedLines.length - modifiedStart;
                changes.push(new CodeLineChange(originalStart, originalCount, modifiedStart, modifiedCount, CodeDiffer.innerChanges(originalLines, originalStart, originalCount, modifiedLines, modifiedStart, modifiedCount)));
            }
            originalEnd = hunk.originalStart + hunk.originalCount;
            modifiedEnd = hunk.modifiedStart + hunk.modifiedCount;
        }
        return new CodeDiffSource(CodeDocument.fromLines(originalLines), originalLines.length, CodeDocument.fromLines(modifiedLines), modifiedLines.length, changes, originalNumbers, modifiedNumbers, gaps);
    }
}

