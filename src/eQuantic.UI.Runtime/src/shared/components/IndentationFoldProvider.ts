import { CodeDocument, CodeFold } from "../runtime-exports";
export class IndentationFoldProvider {
    constructor(props?: any) {
        if (props && typeof props === 'object') Object.assign(this, props);
    }
    foldsFor(document: CodeDocument) {
        let folds: CodeFold[] = [];
        for (let line = 0; line < document.lineCount - 1; line++) {
            let text = document.line(line);
            if (text.trim().length === 0) continue;
            let indent = document.indentOf(line).length;
            let last = line;
            for (let next = line + 1; next < document.lineCount; next++) {
                let candidate = document.line(next);
                if (candidate.trim().length === 0) continue;
                if (document.indentOf(next).length <= indent) break;
                last = next;
            }
            if (last > line) folds.push(new CodeFold(line, last));
        }
        return folds;
    }
}

