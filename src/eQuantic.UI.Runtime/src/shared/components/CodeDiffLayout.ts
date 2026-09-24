import { $eq, CodeCollapse, CodeFiller, CodeRows } from "../runtime-exports";

export class CodeDiffLayout { declare original: CodeRows; declare modified: CodeRows; constructor(original: any = null, modified: any = null) { this.original = original; this.modified = modified; } equals(o: unknown) { return o instanceof CodeDiffLayout && $eq.equals(this.original, o.original) && $eq.equals(this.modified, o.modified); } with(patch: any) { return new CodeDiffLayout(('original' in patch ? patch.original : this.original), ('modified' in patch ? patch.modified : this.modified)); } static sideBySide(changes: any, originalLines: number, modifiedLines: number, context: number, expanded: any, gaps: any, foldLabel: (value: number) => string | null) { let originalFillers: CodeFiller[] = [];
    let modifiedFillers: CodeFiller[] = [];
    CodeDiffLayout.addGaps(gaps, originalFillers, modifiedFillers);
    for (const change of changes) {
        let difference = change.modifiedCount - change.originalCount;
        if (difference > 0) originalFillers.push(new CodeFiller(change.originalStart + change.originalCount, difference)); else if (difference < 0) modifiedFillers.push(new CodeFiller(change.modifiedStart + change.modifiedCount, -difference));
    }
    let [originalRuns, modifiedRuns] = CodeDiffLayout.unchangedRuns(changes, originalLines, modifiedLines, context, expanded, gaps, foldLabel);
    return new CodeDiffLayout(new CodeRows(originalLines, originalFillers, originalRuns), new CodeRows(modifiedLines, modifiedFillers, modifiedRuns)); } static inline(changes: any, originalLines: number, modifiedLines: number, context: number, expanded: any, gaps: any, foldLabel: (value: number) => string | null) { let originalGaps: CodeFiller[] = [];
    let removed: CodeFiller[] = [];
    CodeDiffLayout.addGaps(gaps, originalGaps, removed);
    for (const change of changes) {
        if (change.originalCount > 0) removed.push(new CodeFiller(change.modifiedStart, change.originalCount, change.originalStart));
    }
    let [originalRuns, modifiedRuns] = CodeDiffLayout.unchangedRuns(changes, originalLines, modifiedLines, context, expanded, gaps, foldLabel);
    return new CodeDiffLayout(new CodeRows(originalLines, originalGaps, originalRuns), new CodeRows(modifiedLines, removed, modifiedRuns)); } static addGaps(gaps: any, original: CodeFiller[], modified: CodeFiller[]) { if (gaps == null) return;
    for (const gap of gaps) {
        original.push(new CodeFiller(gap.originalLine, 1, null, gap.header));
        modified.push(new CodeFiller(gap.modifiedLine, 1, null, gap.header));
    } } static unchangedRuns(changes: any, originalLines: number, modifiedLines: number, context: number, expanded: any, gaps: any, foldLabel: (value: number) => string | null) { let original: CodeCollapse[] = [];
    let modified: CodeCollapse[] = [];
    let originalAt = 0;
    let modifiedAt = 0;
    for (let i = 0; i <= changes.length; i++) {
        let originalEnd = i < changes.length ? changes[i].originalStart : originalLines;
        let modifiedEnd = i < changes.length ? changes[i].modifiedStart : modifiedLines;
        let length = Math.min(originalEnd - originalAt, modifiedEnd - modifiedAt);
        let before = i > 0 ? context : 0;
        let after = i < changes.length ? context : 0;
        let hidden = length - before - after;
        if (hidden >= CodeDiffLayout.fewestFolded && !((($r) => $r == null ? null : $eq.collections.contains($r, originalAt + before))(expanded) ?? false) && !CodeDiffLayout.crossesAGap(gaps, originalAt + before, originalAt + before + hidden)) {
            let label = foldLabel?.(hidden);
            original.push(new CodeCollapse(originalAt + before, originalAt + before + hidden - 1, true, label));
            modified.push(new CodeCollapse(modifiedAt + before, modifiedAt + before + hidden - 1, true, label));
        }
        if (i < changes.length) {
            originalAt = changes[i].originalStart + changes[i].originalCount;
            modifiedAt = changes[i].modifiedStart + changes[i].modifiedCount;
        }
    }
    return [original, modified]; } static crossesAGap(gaps: any, from: number, to: number) { if (gaps == null) return false;
    for (const gap of gaps) if (gap.originalLine > from && gap.originalLine <= to) return true;
    return false; } static defaultContext = 3; static fewestFolded = 3; toString() { return `CodeDiffLayout { Original = ${this.original}, Modified = ${this.modified} }`; } }
