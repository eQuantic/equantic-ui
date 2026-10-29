import { $eq, CancellationToken, CodeDocument, CodeEditorController, CodeSymbol, UiThread } from "../runtime-exports";

export class CodeSymbols {
    constructor(editor: CodeEditorController) {
        this._editor = null!;
        this.providers = [];
        this.failed = null;
        this._editor = editor;
    }

    _editor!: CodeEditorController;
    providers!: any[];
    failed!: ((exception: Error) => void) | null;

    async requestAsync(cancellation: CancellationToken) {
        let document = this._editor.document;
        let asked = [];
        for (const provider of this.providers) asked.push(this.askOne(provider, document, cancellation));
        let answer: CodeSymbol[] = [];
        for (const one of asked) $eq.collections.addRange(answer, await one);
        cancellation.throwIfCancellationRequested();
        return answer;
    }

    async askOne(provider: any, document: CodeDocument, cancellation: CancellationToken) {
        try {
            return await provider.symbolsAsync(document, cancellation);
        } catch ($e: any) {
            let error = $e;
            if ($eq.exceptions.is($e, 'System.OperationCanceledException') && $eq.exceptions.filter(() => cancellation.isCancellationRequested)) {
                return [];
            } else {
                UiThread.run(() => this.failed?.(error));
                return [];
            }
        }
    }

    toString(): string {
        return 'eQuantic.UI.Code.CodeSymbols';
    }
}

