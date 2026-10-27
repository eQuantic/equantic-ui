import { $eq, CancellationToken, CodeDocument, CodeEditorController, CodeLocation, CodePosition, UiThread } from "../runtime-exports";

export class CodeReferences {
    constructor(editor: CodeEditorController) {
        this._editor = null!;
        this.providers = [];
        this.failed = null;
        this._editor = editor;
    }

    _editor!: CodeEditorController;
    providers!: any[];
    failed!: ((exception: Error) => void) | null;

    async requestAsync(position: CodePosition, cancellation: CancellationToken) {
        let document = this._editor.document;
        let asked = [];
        for (const provider of this.providers) asked.push(this.askOne(provider, document, position, cancellation));
        let answers = [];
        for (const one of asked) answers.push(await one);
        cancellation.throwIfCancellationRequested();
        return CodeLocation.joined(answers);
    }

    async askOne(provider: any, document: CodeDocument, position: CodePosition, cancellation: CancellationToken) {
        try {
            return await provider.referencesAsync(document, position, cancellation);
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
        return 'eQuantic.UI.Code.CodeReferences';
    }
}

