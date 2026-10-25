import { CancellationToken, CodeCompletionContext, CodeCompletionItem, CodeCompletionList, CodeDocument, CodePosition, CodeSnippet, CodeSnippetDefinition } from "../runtime-exports";

export class CodeSnippetCompletionProvider {
    constructor(snippets: CodeSnippetDefinition[]) {
        this._items = [];
        for (const snippet of snippets) {
            let $n40: any; 
            this._items.push(($n40 = new CodeCompletionItem(snippet.prefix, 'snippet'), $n40.detail = snippet.description, $n40.snippet = CodeSnippet.parse(snippet.body), $n40));
        }
    }

    _items!: CodeCompletionItem[];

    async completeAsync(document: CodeDocument, position: CodePosition, _context: CodeCompletionContext, _cancellation: CancellationToken) {
        let line = document.line(position.line);
        let start = position.column;
        while (start > 0 && CodeDocument.isWordChar(line[start - 1])) start--;
        if (start > 0 && line[start - 1] === '.') return Promise.resolve(CodeCompletionList.empty);
        return Promise.resolve(new CodeCompletionList(this._items));
    }

    get triggerCharacters(): string[] {
        return [];
    }

    async resolveAsync(item: CodeCompletionItem, _cancellation: CancellationToken) {
        return Promise.resolve(item);
    }

    toString(): string {
        return 'eQuantic.UI.Code.CodeSnippetCompletionProvider';
    }
}

