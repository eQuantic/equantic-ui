import { CancellationToken, CodeCompletionContext, CodeCompletionItem, CodeCompletionList, CodeDocument, CodePosition } from "../runtime-exports";

export class CodeKeywordCompletionProvider {
    constructor(props?: any) {
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    async completeAsync(document: CodeDocument, position: CodePosition, context: CodeCompletionContext, _cancellation: CancellationToken) {
        let line = document.line(position.line);
        let start = position.column;
        while (start > 0 && CodeDocument.isWordChar(line[start - 1])) start--;
        if (start > 0 && line[start - 1] === '.') return Promise.resolve(CodeCompletionList.empty);
        let items: CodeCompletionItem[] = [];
        for (const keyword of context.language.keywords) items.push(new CodeCompletionItem(keyword, 'keyword'));
        return Promise.resolve(new CodeCompletionList(items));
    }

    get triggerCharacters(): string[] {
        return [];
    }

    async resolveAsync(item: CodeCompletionItem, _cancellation: CancellationToken) {
        return Promise.resolve(item);
    }
}

