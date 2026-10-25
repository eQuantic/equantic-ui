import { $eq, CancellationToken, CancellationTokenSource, CodeChange, CodeDiagnostic, CodeDocument, CodeEdit, CodeEditorController, CodePosition, CodeRange, ProviderRequest, TimeSpan, UiThread } from "../runtime-exports";

export class CodeDiagnostics {
    constructor(editor: CodeEditorController) {
        this._editor = null!;
        this._reported = [];
        this._answered = [];
        this._items = [];
        this._generation = 0;
        this._request = null;
        this._waiting = 0;
        this.providers = [];
        this.delay = $eq.time.timeSpan.fromMilliseconds(500n);
        this.changed = null;
        this.failed = null;
        this._moved = false;
        this._editor = editor;
        editor.changed = $eq.delegates.combine(editor.changed, this.edited.bind(this));
    }

    _editor!: CodeEditorController;
    _reported!: CodeDiagnostic[];
    _answered!: [any, CodeDiagnostic[]][];
    _items!: CodeDiagnostic[];
    _generation!: number;
    _request!: CancellationTokenSource | null;
    _waiting!: number;
    providers!: any[];
    delay!: TimeSpan;
    changed!: (() => void) | null;
    failed!: ((exception: Error) => void) | null;
    _moved!: boolean;

    get items(): CodeDiagnostic[] {
        return this._items;
    }

    get isChecking(): boolean {
        return this._waiting > 0;
    }

    report(problems: CodeDiagnostic[]) {
        this._reported = this.placed(problems);
        this.gather();
    }

    check() {
        return this.ask(false);
    }

    at(position: CodePosition) {
        return [...this._items.filter((problem) => CodePosition.opLessOrEqual(problem.range.start, position) && CodePosition.opLessOrEqual(position, problem.range.end))].sort(($a, $b) => { { const $k: ($element: typeof $a) => any = ((problem) => (($0) => (({ 'hint': 0, 'information': 1, 'warning': 2, 'error': 3 })[$0] ?? $0))(problem.severity)); const $x = $k($a), $y = $k($b); if ($x < $y) return 1; if ($x > $y) return -1; } { const $k: ($element: typeof $a) => any = ((problem) => problem.range.start.line); const $x = $k($a), $y = $k($b); if ($x < $y) return -1; if ($x > $y) return 1; } { const $k: ($element: typeof $a) => any = ((problem) => problem.range.start.column); const $x = $k($a), $y = $k($b); if ($x < $y) return -1; if ($x > $y) return 1; } return 0; }).slice();
    }

    next(backward: boolean = false) {
        if (this._items.length === 0) return null;
        let caret = this._editor.caret;
        let next = backward ? (this._items.filter((problem) => CodePosition.opLessThan(problem.range.start, caret)).pop() ?? null) ?? this._items[this._items.length - 1] : (this._items.find((problem) => CodePosition.opGreaterThan(problem.range.start, caret)) ?? null) ?? this._items[0];
        this._editor.selection = new CodeRange(next.range.start);
        this._editor.hover.showProblems(next.range.start);
        return next;
    }

    carry(edit: CodeEdit) {
        this._reported = CodeDiagnostics.carried(this._reported, edit);
        for (let i = 0; i < this._answered.length; i++) {
            let [provider, problems] = this._answered[i];
            this._answered[i] = [provider, CodeDiagnostics.carried(problems, edit)];
        }
        let items = CodeDiagnostics.carried(this._items, edit);
        if (CodeDiagnostics.same(items, this._items)) return;
        this._items = items;
        this._moved = true;
    }

    reset() {
        this._reported = [];
        this._answered.splice(0);
        if (this._items.length === 0) return;
        this._items = [];
        this._moved = true;
    }

    edited(change: CodeChange) {
        this.gather();
        this.ask(!change.replaced);
    }

    static carried(problems: CodeDiagnostic[], edit: CodeEdit) {
        return problems.map((problem) => $eq.withPatch(problem, { range: edit.carry(problem.range) })).slice();
    }

    async ask(wait: boolean) {
        let generation = ++this._generation;
        ProviderRequest.cancel(this._request, this.failed);
        this._request = null;
        if ($eq.collections.count(this.providers) === 0) {
            this.gather();
            return;
        }
        let request = $eq.cancellation.source();
        this._request = request;
        if (wait && (this.delay.compareTo($eq.time.timeSpan.zero) > 0)) {
            this._waiting++;
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
                this._waiting--;
                if (waited && generation === this._generation) this.askEvery(generation, request.token);
            });
            return;
        }
        this.askEvery(generation, request.token);
    }

    askEvery(generation: number, cancellation: CancellationToken) {
        let document = this._editor.document;
        for (const provider of this.providers.slice()) this.askOne(provider, document, generation, cancellation);
    }

    async askOne(provider: any, document: CodeDocument, generation: number, cancellation: CancellationToken) {
        this._waiting++;
        let answer: any = null;
        let failure: Error | null = null;
        try {
            answer = await provider.diagnoseAsync(document, cancellation);
        } catch ($e: any) {
            let error = $e;
            if ($eq.exceptions.is($e, 'System.OperationCanceledException') && $eq.exceptions.filter(() => cancellation.isCancellationRequested)) {} else {
                failure = error;
            }
        }
        UiThread.run(() => {
            this._waiting--;
            if (!(failure == null)) {
                this.failed?.(failure);
                return;
            }
            if (answer == null || generation !== this._generation) return;
            let problems = this.placed(answer);
            let at = -1;
            for (let i = 0; i < this._answered.length; i++) if ((this._answered[i][0] === provider)) at = i;
            if (at >= 0) this._answered[at] = [provider, problems]; else this._answered.push([provider, problems]);
            this.gather();
        });
    }

    placed(problems: CodeDiagnostic[]) {
        let document = this._editor.document;
        return $eq.linq.seq(problems).map((problem) => $eq.withPatch(problem, { range: new CodeRange(document.clamp(problem.range.start), document.clamp(problem.range.end)) })).slice();
    }

    gather() {
        for (let i = this._answered.length - 1; i >= 0; i--) {
            let provider = this._answered[i][0];
            if (!this.providers.some((held) => (held === provider))) this._answered.splice(i, 1);
        }
        let items = [...[...this._reported, ...this._answered.flatMap((entry) => entry[1])]].sort(($a, $b) => { { const $k: ($element: typeof $a) => any = ((problem) => problem.range.start.line); const $x = $k($a), $y = $k($b); if ($x < $y) return -1; if ($x > $y) return 1; } { const $k: ($element: typeof $a) => any = ((problem) => problem.range.start.column); const $x = $k($a), $y = $k($b); if ($x < $y) return -1; if ($x > $y) return 1; } { const $k: ($element: typeof $a) => any = ((problem) => (($0) => (({ 'hint': 0, 'information': 1, 'warning': 2, 'error': 3 })[$0] ?? $0))(problem.severity)); const $x = $k($a), $y = $k($b); if ($x < $y) return 1; if ($x > $y) return -1; } { const $k: ($element: typeof $a) => any = ((problem) => problem.range.end.line); const $x = $k($a), $y = $k($b); if ($x < $y) return -1; if ($x > $y) return 1; } { const $k: ($element: typeof $a) => any = ((problem) => problem.range.end.column); const $x = $k($a), $y = $k($b); if ($x < $y) return -1; if ($x > $y) return 1; } return 0; }).slice();
        if (CodeDiagnostics.same(items, this._items) && !this._moved) return;
        this._items = items;
        this._moved = false;
        this.changed?.();
    }

    static same(one: CodeDiagnostic[], other: CodeDiagnostic[]) {
        if (one.length !== other.length) return false;
        for (let i = 0; i < one.length; i++) if (!$eq.equalsInstance(one[i], other[i])) return false;
        return true;
    }

    toString(): string {
        return 'eQuantic.UI.Code.CodeDiagnostics';
    }
}

