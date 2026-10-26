import { $eq, CancellationToken, CodeCompletionContext, CodeCompletionItem, CodeCompletionList, CodeDocument, CodePosition } from "../runtime-exports";

export class CodeChoiceProvider {
    constructor(options: string[]) {
        this._list = null!;
        let items: CodeCompletionItem[] = [];
        for (let i = 0; i < $eq.collections.count(options); i++) {
            let $n13: any; 
            items.push(($n13 = new CodeCompletionItem($eq.collections.item(options, i), 'value'), $n13.sortText = $eq.text.format(i, null, undefined, undefined, 'int32').padStart(6, '0'), $n13));
        }
        this._list = new CodeCompletionList(items);
    }

    _list!: CodeCompletionList;

    async completeAsync(_document: CodeDocument, _position: CodePosition, _context: CodeCompletionContext, _cancellation: CancellationToken) {
        return Promise.resolve(this._list);
    }

    get triggerCharacters(): string[] {
        return [];
    }

    async resolveAsync(item: CodeCompletionItem, _cancellation: CancellationToken) {
        return Promise.resolve(item);
    }

    toString(): string {
        return 'eQuantic.UI.Code.CodeChoiceProvider';
    }
}

