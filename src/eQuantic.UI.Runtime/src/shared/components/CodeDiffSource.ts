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
        let before = CodeDocument.fromText(original);
        let after = CodeDocument.fromText(modified);
        return new CodeDiffSource(before, before.lineCount, after, after.lineCount, CodeDiffer.compare(before, after), null, null, []);
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

