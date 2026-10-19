import { $eq, CancellationToken, CancellationTokenSource, CodeDiagnostic, CodeDocument, CodeEditorController, CodeHover, CodePosition, CodeRange, TimeSpan, UiThread } from "../runtime-exports";

export class CodeHoverSession {
    constructor(editor: CodeEditorController) {
        this._editor = null!;
        this._generation = 0;
        this._request = null;
        this._pending = null;
        this._open = false;
        this._byKeyboard = false;
        this._requestByKeyboard = false;
        this._range = CodeRange.$zero();
        this._problems = [];
        this._answers = [];
        this._shown = [];
        this.providers = [];
        this.delay = $eq.time.timeSpan.fromMilliseconds(300n);
        this.changed = null;
        this.failed = null;
        this._editor = editor;
        editor.selectionChanged = $eq.delegates.combine(editor.selectionChanged, (_) => this.dismiss());
        editor.changed = $eq.delegates.combine(editor.changed, (_) => this.dismiss());
        editor.completion.changed = $eq.delegates.combine(editor.completion.changed, this.completionChanged.bind(this));
    }

    _editor!: CodeEditorController;
    _generation!: number;
    _request!: CancellationTokenSource | null;
    _pending!: CodeRange | null;
    _open!: boolean;
    _byKeyboard!: boolean;
    _requestByKeyboard!: boolean;
    _range!: CodeRange;
    _problems!: CodeDiagnostic[];
    _answers!: (CodeHover | null)[];
    _shown!: CodeHover[];
    providers!: any[];
    delay!: TimeSpan;
    changed!: (() => void) | null;
    failed!: ((exception: Error) => void) | null;

    get isOpen(): boolean {
        return this._open;
    }

    get byKeyboard(): boolean {
        return this._byKeyboard;
    }

    get range(): CodeRange {
        return this._range;
    }

    get problems(): CodeDiagnostic[] {
        return this._problems;
    }

    get answers(): CodeHover[] {
        return this._shown;
    }

    completionChanged() {
        if (this._editor.completion.isOpen) this.dismiss();
    }

    showAt(position: CodePosition) {
        if (this._editor.completion.isOpen) return false;
        let at = this._editor.document.clamp(position);
        let explained = this.explained(at, false);
        if (explained == null && $eq.collections.count(this.providers) === 0) return false;
        this.rest(explained ?? new CodeRange(at), at, false, true, true);
        return true;
    }

    dismiss() {
        this._generation++;
        this._request?.cancel();
        this._request = null;
        this._pending = null;
        this._requestByKeyboard = false;
        if (!this._open) return false;
        this._open = false;
        this._byKeyboard = false;
        this._problems = [];
        this._answers = [];
        this._shown = [];
        this.changed?.();
        return true;
    }

    pointerAt(character: CodePosition | null) {
        let position: any; 
        if (!((position = character) != null)) {
            this.pointerLeft();
            return;
        }
        if (this._editor.completion.isOpen) return;
        if (this._open && !this._byKeyboard && CodeHoverSession.covers(this._range, position)) return;
        let pending: any; 
        if ((pending = this._pending) != null && CodeHoverSession.covers(pending, position)) return;
        let explained: any; 
        if (!((explained = this.explained(position, true)) != null)) {
            this.pointerLeft();
            return;
        }
        if (this._open && !this._byKeyboard) this.dismiss();
        this._pending = explained;
        this.rest(explained, position, true, false, true);
    }

    pointerLeft() {
        if (this._requestByKeyboard) return;
        if (this._open && this._byKeyboard) {
            this._generation++;
            this._request?.cancel();
            this._request = null;
            this._pending = null;
            return;
        }
        this.dismiss();
    }

    showProblems(position: CodePosition) {
        let explained = this.explained(position, false) ?? new CodeRange(position);
        this.rest(explained, position, false, true, false);
    }

    async rest(explained: CodeRange, position: CodePosition, wait: boolean, keyboard: boolean, ask: boolean) {
        if (this._editor.completion.isOpen) return;
        let generation = ++this._generation;
        this._request?.cancel();
        let request = $eq.cancellation.source();
        this._request = request;
        this._pending = explained;
        this._requestByKeyboard = keyboard;
        if (wait && (this.delay.compareTo($eq.time.timeSpan.zero) > 0)) {
            let waited = true;
            try {
                await $eq.task.delay(this.delay, request.token);
            } catch ($e: any) {
                if ($eq.exceptions.is($e, 'System.OperationCanceledException')) {
                    waited = false;
                } else {
                    throw $e;
                }
            }
            UiThread.run(() => {
                if (waited && generation === this._generation) this.show(explained, position, keyboard, ask, generation, request.token);
            });
            return;
        }
        this.show(explained, position, keyboard, ask, generation, request.token);
    }

    show(explained: CodeRange, position: CodePosition, keyboard: boolean, ask: boolean, generation: number, cancellation: CancellationToken) {
        let document = this._editor.document;
        let providers = ask ? this.providers.slice() : [];
        this._range = explained;
        this._problems = keyboard ? this._editor.diagnostics.at(position).slice() : this.problemsUnder(position);
        this._answers = [];
        this._shown = [];
        for (const _ of providers) this._answers.push(null);
        this._byKeyboard = keyboard;
        let showed = this._open;
        this._open = this._problems.length > 0;
        if (this._open || showed) this.changed?.();
        for (let slot = 0; slot < providers.length; slot++) this.askOne(slot, providers[slot], document, position, generation, cancellation);
    }

    async askOne(slot: number, provider: any, document: CodeDocument, position: CodePosition, generation: number, cancellation: CancellationToken) {
        let answer: CodeHover | null = null;
        try {
            answer = await provider.hoverAsync(document, position, cancellation);
        } catch ($e: any) {
            let error = $e;
            if ($eq.exceptions.is($e, 'System.OperationCanceledException') && $eq.exceptions.filter(() => cancellation.isCancellationRequested)) {
                return;
            } else {
                UiThread.run(() => this.failed?.(error));
                return;
            }
        }
        UiThread.run(() => {
            if (generation !== this._generation || answer == null) return;
            this._answers[slot] = answer;
            let shown: CodeHover[] = [];
            for (const held of this._answers) if (!(held == null)) shown.push(held);
            this._shown = shown;
            let range = new CodeRange(document.clamp(answer.range.start), document.clamp(answer.range.end));
            this._range = CodeHoverSession.union(this._range, range);
            this._open = true;
            this.changed?.();
        });
    }

    explained(position: CodePosition, character: boolean) {
        let document = this._editor.document;
        let line = document.line(position.line);
        let explained = position.column < line.length && !$eq.text.isWhiteSpace(line[position.column]) ? document.wordAt(position) : null;
        for (const problem of character ? this.problemsUnder(position) : this._editor.diagnostics.at(position)) {
            let before: any; 
            explained = (before = explained) != null ? CodeHoverSession.union(before, problem.range) : problem.range;
        }
        return explained;
    }

    problemsUnder(position: CodePosition) {
        let under: CodeDiagnostic[] = [];
        for (const problem of this._editor.diagnostics.at(position)) if (CodeHoverSession.covers(problem.range, position)) under.push(problem);
        return under;
    }

    static covers(range: CodeRange, position: CodePosition) {
        return range.isEmpty ? $eq.equals(position, range.start) : CodePosition.opLessOrEqual(range.start, position) && CodePosition.opLessThan(position, range.end);
    }

    static union(a: CodeRange, b: CodeRange) {
        return new CodeRange(CodePosition.opLessOrEqual(a.start, b.start) ? a.start : b.start, CodePosition.opGreaterOrEqual(a.end, b.end) ? a.end : b.end);
    }

    toString(): string {
        return 'eQuantic.UI.Code.CodeHoverSession';
    }
}

