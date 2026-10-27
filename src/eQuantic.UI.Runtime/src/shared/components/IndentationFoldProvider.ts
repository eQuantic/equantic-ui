import { $eq, CancellationToken, CodeDocument, CodeFold, CodeLineCells } from "../runtime-exports";

export class IndentationFoldProvider {
    constructor(tabSize: number = 4) {
        this._tabSize = 0;
        this._tabSize = tabSize;
    }

    _tabSize!: number;

    async foldsAsync(document: CodeDocument, _cancellation: CancellationToken) {
        return Promise.resolve(this.foldsOf(document));
    }

    depthOf(document: CodeDocument, line: number) {
        return CodeLineCells.widthOf(document.indentOf(line), this._tabSize);
    }

    foldsOf(document: CodeDocument) {
        let folds: CodeFold[] = [];
        for (let line = 0; line < document.lineCount - 1; line++) {
            let text = document.line(line);
            if ($eq.text.trim(text).length === 0) continue;
            let indent = this.depthOf(document, line);
            let last = line;
            for (let next = line + 1; next < document.lineCount; next++) {
                let candidate = document.line(next);
                if ($eq.text.trim(candidate).length === 0) continue;
                if (this.depthOf(document, next) <= indent) break;
                last = next;
            }
            if (last > line) folds.push(new CodeFold(line, last));
        }
        return folds;
    }

    toString(): string {
        return 'eQuantic.UI.Code.IndentationFoldProvider';
    }
}

