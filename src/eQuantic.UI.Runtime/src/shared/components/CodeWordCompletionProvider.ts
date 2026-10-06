import { $eq, CancellationToken, CodeCompletionContext, CodeCompletionItem, CodeCompletionList, CodeDocument, CodePosition } from "../runtime-exports";

export class CodeWordCompletionProvider {
    constructor(props?: any) {
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    static _budget: number | undefined;

    static get budget(): number {
        return CodeWordCompletionProvider._budget ??= 50_000;
    }

    async completeAsync(document: CodeDocument, position: CodePosition, _context: CodeCompletionContext, _cancellation: CancellationToken) {
        let seen: Set<string> = $eq.collections.hashSet();
        let items: CodeCompletionItem[] = [];
        let left = CodeWordCompletionProvider.budget;
        for (let distance = 0; left > 0; distance++) {
            let above = position.line - distance;
            let below = position.line + distance;
            if (above < 0 && below >= document.lineCount) break;
            if (above >= 0) left = CodeWordCompletionProvider.read(document.line(above), above === position.line ? position.column : -1, left, seen, items);
            if (distance > 0 && below < document.lineCount && left > 0) left = CodeWordCompletionProvider.read(document.line(below), -1, left, seen, items);
        }
        return Promise.resolve(new CodeCompletionList(items));
    }

    static read(text: string, caret: number, left: number, seen: Set<string>, items: CodeCompletionItem[]) {
        let from = caret < 0 ? 0 : Math.max(0, Math.min(caret - Math.trunc(left / 2), text.length - left));
        if (from > 0 && CodeDocument.isWordChar(text[from - 1])) {
            while (from < text.length && CodeDocument.isWordChar(text[from])) from++;
        }
        let end = Math.min(text.length, from + left);
        let i = from;
        while (i < end) {
            if (!CodeDocument.isWordChar(text[i])) {
                i++;
                continue;
            }
            let start = i;
            while (i < end && CodeDocument.isWordChar(text[i])) i++;
            if (start <= caret && caret <= i) continue;
            if (i === end && end < text.length && CodeDocument.isWordChar(text[end])) continue;
            if ((/^\p{Nd}$/u.test(text[start]))) continue;
            let word = $eq.text.substring(text, start, i - start);
            if ($eq.collections.setAdd(seen, word)) items.push(new CodeCompletionItem(word));
        }
        return left - (end - from) - 1;
    }

    get triggerCharacters(): string[] {
        return [];
    }

    async resolveAsync(item: CodeCompletionItem, _cancellation: CancellationToken) {
        return Promise.resolve(item);
    }
}

