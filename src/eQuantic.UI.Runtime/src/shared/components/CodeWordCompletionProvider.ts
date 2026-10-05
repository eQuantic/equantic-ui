import { $eq, CancellationToken, CodeCompletionContext, CodeCompletionItem, CodeCompletionList, CodeDocument, CodePosition } from "../runtime-exports";

export class CodeWordCompletionProvider {
    constructor(props?: any) {
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    async completeAsync(document: CodeDocument, position: CodePosition, _context: CodeCompletionContext, _cancellation: CancellationToken) {
        let seen: Set<string> = new Set();
        let items: CodeCompletionItem[] = [];
        for (let line = 0; line < document.lineCount; line++) {
            let text = document.line(line);
            let i = 0;
            while (i < text.length) {
                if (!CodeDocument.isWordChar(text[i])) {
                    i++;
                    continue;
                }
                let start = i;
                while (i < text.length && CodeDocument.isWordChar(text[i])) i++;
                if (line === position.line && start <= position.column && position.column <= i) continue;
                if ((/^\p{Nd}$/u.test(text[start]))) continue;
                let word = $eq.text.substring(text, start, i - start);
                if ($eq.collections.setAdd(seen, word)) items.push(new CodeCompletionItem(word));
            }
        }
        return Promise.resolve(new CodeCompletionList(items));
    }

    get triggerCharacters(): string[] {
        return [];
    }

    async resolveAsync(item: CodeCompletionItem, _cancellation: CancellationToken) {
        return Promise.resolve(item);
    }
}

