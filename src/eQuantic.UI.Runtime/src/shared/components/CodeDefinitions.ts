import { $eq, CancellationToken, CancellationTokenSource, CodeDefinitionKindValue, CodeDocument, CodeEditorController, CodeLocation, CodePosition, CodeRange, ProviderRequest, UiThread } from "../runtime-exports";

export class CodeDefinitions {
    constructor(editor: CodeEditorController) {
        this._editor = null!;
        this._generation = 0;
        this._request = null;
        this._asked = null;
        this._link = null;
        this.providers = [];
        this.failed = null;
        this.changed = null;
        this._editor = editor;
        editor.changed = $eq.delegates.combine(editor.changed, (_) => this.clearLink());
        editor.selectionChanged = $eq.delegates.combine(editor.selectionChanged, this.selected.bind(this));
    }

    _editor!: CodeEditorController;
    _generation!: number;
    _request!: CancellationTokenSource | null;
    _asked!: CodeRange | null;
    _link!: CodeRange | null;
    providers!: any[];
    failed!: ((exception: Error) => void) | null;
    changed!: (() => void) | null;

    get link(): CodeRange | null {
        return this._link;
    }

    selected(selection: CodeRange) {
        if (!selection.isEmpty) this.clearLink();
    }

    async requestAsync(position: CodePosition, kind: CodeDefinitionKindValue, cancellation: CancellationToken) {
        let document = this._editor.document;
        let asked = [];
        for (const provider of this.providers) asked.push(this.askOne(provider, document, position, kind, cancellation));
        let answers = [];
        for (const one of asked) answers.push(await one);
        cancellation.throwIfCancellationRequested();
        return CodeLocation.joined(answers);
    }

    async askOne(provider: any, document: CodeDocument, position: CodePosition, kind: CodeDefinitionKindValue, cancellation: CancellationToken) {
        try {
            return await provider.definitionsAsync(document, position, kind, cancellation);
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

    static follows(modifiers: number) {
        return (modifiers & (4 | 8 | 2 | 1)) === 4;
    }

    pointerAt(character: CodePosition | null, modifiers: number) {
        let at: any; let word: any; 
        if (!CodeDefinitions.follows(modifiers) || $eq.collections.count(this.providers) === 0 || !((at = character) != null) || !((word = this.wordUnder(at)) != null)) {
            this.clearLink();
            return;
        }
        let asked: any; 
        if ((asked = this._asked) != null && $eq.equals(asked, word)) return;
        this.clearLink();
        this._asked = word;
        let generation = this._generation;
        let request = $eq.cancellation.source();
        this._request = request;
        this.askLink(word, at, generation, request.token);
    }

    async askLink(word: CodeRange, at: CodePosition, generation: number, cancellation: CancellationToken) {
        let places = null;
        try {
            places = await this.requestAsync(at, 'definition', cancellation);
        } catch ($e: any) {
            if ($eq.exceptions.is($e, 'System.OperationCanceledException') && $eq.exceptions.filter(() => cancellation.isCancellationRequested)) {
                return;
            } else {
                throw $e;
            }
        }
        UiThread.run(() => {
            if (generation !== this._generation || $eq.collections.count(places) === 0) return;
            this._link = word;
            this.changed?.();
        });
    }

    onLink(character: CodePosition | null) {
        let link: any; let at: any; 
        return (link = this._link) != null && (at = character) != null && CodePosition.opLessOrEqual(link.start, at) && CodePosition.opLessThan(at, link.end);
    }

    clearLink() {
        this._generation++;
        ProviderRequest.cancel(this._request, this.failed);
        this._request = null;
        this._asked = null;
        if (this._link == null) return;
        this._link = null;
        this.changed?.();
    }

    wordUnder(at: CodePosition) {
        let document = this._editor.document;
        if (at.line < 0 || at.line >= document.lineCount) return null;
        let line = document.line(at.line);
        if (at.column < 0 || at.column >= line.length || !CodeDocument.isWordChar(line[at.column])) return null;
        return document.wordAt(at);
    }

    toString(): string {
        return 'eQuantic.UI.Code.CodeDefinitions';
    }
}

