import { $eq, CancellationToken, CodeCompletionContext, CodeCompletionItem, CodeCompletionList, CodeDocument, CodePosition } from "../runtime-exports";

export class CodeWordCompletionProvider {
    static $interfaces = ['eQuantic.UI.Code.ICodeCompletionProvider'];

    constructor() {}

    static budget: number = 50000;

    async completeAsync(document: CodeDocument, position: CodePosition, _context: CodeCompletionContext, _cancellation: CancellationToken) {
        let seen: Set<string> = $eq.collections.hashSet();
        let items: CodeCompletionItem[] = [];
        let left = CodeWordCompletionProvider.budget;
        for (let distance = 0; left > 0; distance = distance + 1 | 0) {
            let above = position.line - distance | 0;
            let below = position.line + distance | 0;
            if (above < 0 && below >= document.lineCount) break;
            if (above >= 0) left = CodeWordCompletionProvider.read(document.line(above), above === position.line ? position.column : -1, left, seen, items);
            if (distance > 0 && below < document.lineCount && left > 0) left = CodeWordCompletionProvider.read(document.line(below), -1, left, seen, items);
        }
        return Promise.resolve(new CodeCompletionList(items));
    }

    static read(text: string, caret: number, left: number, seen: Set<string>, items: CodeCompletionItem[]) {
        let first = caret < 0 ? 0 : Math.max(0, Math.min(caret - Math.trunc(left / 2) | 0, text.length - left | 0));
        let end = Math.min(text.length, first + left | 0);
        let from = first;
        if (from > 0 && CodeDocument.isWordChar(text[from - 1 | 0])) {
            while (from < end && CodeDocument.isWordChar(text[from])) from = from + 1 | 0;
        }
        let i = from;
        while (i < end) {
            if (!CodeDocument.isWordChar(text[i])) {
                i = i + 1 | 0;
                continue;
            }
            let start = i;
            while (i < end && CodeDocument.isWordChar(text[i])) i = i + 1 | 0;
            if (start <= caret && caret <= i) continue;
            if (i === end && end < text.length && CodeDocument.isWordChar(text[end])) continue;
            if ((/^\p{Nd}$/u.test(text[start]))) continue;
            let word = $eq.text.substring(text, start, i - start | 0);
            if ($eq.collections.setAdd(seen, word)) $eq.collections.listAdd(items, new CodeCompletionItem(word));
        }
        return (left - (end - first | 0) | 0) - 1 | 0;
    }

    get triggerCharacters(): string[] {
        return [];
    }

    async resolveAsync(item: CodeCompletionItem, _cancellation: CancellationToken) {
        return Promise.resolve(item);
    }

    toString(): string {
        return 'eQuantic.UI.Code.CodeWordCompletionProvider';
    }
}

