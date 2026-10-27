import { $eq, CancellationToken, CancellationTokenSource, CodeChange, CodeDocument, CodeEditorController, CodePosition, CodeSignature, CodeSignatureContext, CodeSignatureHelp, ProviderRequest, TimeSpan, UiThread } from "../runtime-exports";

export class CodeSignatureHelpSession {
    constructor(editor: CodeEditorController) {
        this._editor = null!;
        this._generation = 0;
        this._request = null;
        this._help = null;
        this._signature = 0;
        this._chosen = false;
        this._answers = [];
        this._owed = 0;
        this.providers = [];
        this.delay = $eq.time.timeSpan.fromMilliseconds(120n);
        this.changed = null;
        this.failed = null;
        this._editor = editor;
        editor.selectionChanged = $eq.delegates.combine(editor.selectionChanged, (_) => this.moved());
        editor.changed = $eq.delegates.combine(editor.changed, this.edited.bind(this));
    }

    _editor!: CodeEditorController;
    _generation!: number;
    _request!: CancellationTokenSource | null;
    _help!: CodeSignatureHelp | null;
    _signature!: number;
    _chosen!: boolean;
    _answers!: (CodeSignatureHelp | null)[];
    _owed!: number;
    providers!: any[];
    delay!: TimeSpan;
    changed!: (() => void) | null;
    failed!: ((exception: Error) => void) | null;

    get isOpen(): boolean {
        return !(this._help == null);
    }

    get help(): CodeSignatureHelp | null {
        return this._help;
    }

    get activeSignature(): number {
        return this._signature;
    }

    get signature(): CodeSignature | null {
        let help: any; 
        return (help = this._help) != null && this._signature >= 0 && this._signature < $eq.collections.count(help.signatures) ? $eq.collections.item(help.signatures, this._signature) : null;
    }

    get activeParameter(): number {
        let signature: any; 
        return (signature = this.signature) != null ? signature.activeParameter ?? (this._help!).activeParameter : -1;
    }

    invoke() {
        if ($eq.collections.count(this.providers) === 0) return false;
        let $n38: any; 
        this.ask(($n38 = new CodeSignatureContext('invoked'), $n38.isRetrigger = this.isOpen, $n38.active = this.shown(), $n38), false);
        return true;
    }

    dismiss() {
        this._generation++;
        ProviderRequest.cancel(this._request, this.failed);
        this._request = null;
        this._answers = [];
        this._owed = 0;
        if (this._help == null) return false;
        this._help = null;
        this._signature = 0;
        this._chosen = false;
        this.changed?.();
        return true;
    }

    move(delta: number) {
        let help: any; 
        if (!((help = this._help) != null) || $eq.collections.count(help.signatures) < 2) return false;
        let count = $eq.collections.count(help.signatures);
        this._signature = $eq.num.intRem($eq.num.intRem(this._signature + delta, count) + count, count);
        this._chosen = true;
        this.changed?.();
        return true;
    }

    typed(typed: string) {
        if ($eq.collections.count(this.providers) === 0) return;
        let triggered = false;
        let retriggered = false;
        for (const provider of this.providers) {
            triggered = $eq.logic.or(triggered, provider.triggerCharacters.includes(typed));
            retriggered = $eq.logic.or(retriggered, provider.retriggerCharacters.includes(typed));
        }
        if (!triggered && !(retriggered && this.isOpen)) return;
        let $n41: any; 
        this.ask(($n41 = new CodeSignatureContext('character'), $n41.character = typed, $n41.isRetrigger = this.isOpen, $n41.active = this.shown(), $n41), false);
    }

    moved() {
        if (this._help == null && this._request == null) return;
        let $n42: any; 
        this.ask(($n42 = new CodeSignatureContext('contentChange'), $n42.isRetrigger = this.isOpen, $n42.active = this.shown(), $n42), true);
    }

    edited(change: CodeChange) {
        if (change.replaced) this.dismiss();
    }

    shown() {
        let help: any; 
        return (help = this._help) != null ? $eq.withPatch(help, { activeSignature: this._signature }) : null;
    }

    async ask(context: CodeSignatureContext, wait: boolean) {
        let generation = ++this._generation;
        ProviderRequest.cancel(this._request, this.failed);
        let request = $eq.cancellation.source();
        this._request = request;
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
                if (waited && generation === this._generation) this.askEvery(context, generation, request.token);
            });
            return;
        }
        this.askEvery(context, generation, request.token);
    }

    askEvery(context: CodeSignatureContext, generation: number, cancellation: CancellationToken) {
        let document = this._editor.document;
        let position = this._editor.caret;
        let providers = this.providers.slice();
        if (providers.length === 0) {
            this._answers = [];
            this._owed = 0;
            this.apply();
            return;
        }
        this._answers = [];
        for (const _ of providers) this._answers.push(null);
        this._owed = providers.length;
        for (let slot = 0; slot < providers.length; slot++) this.askOne(slot, providers[slot], document, position, context, generation, cancellation);
    }

    async askOne(slot: number, provider: any, document: CodeDocument, position: CodePosition, context: CodeSignatureContext, generation: number, cancellation: CancellationToken) {
        let answer: CodeSignatureHelp | null = null;
        let failure: Error | null = null;
        try {
            answer = await provider.signatureHelpAsync(document, position, context, cancellation);
        } catch ($e: any) {
            let error = $e;
            if ($eq.exceptions.is($e, 'System.OperationCanceledException') && $eq.exceptions.filter(() => cancellation.isCancellationRequested)) {} else {
                failure = error;
            }
        }
        UiThread.run(() => {
            if (!(failure == null)) this.failed?.(failure);
            if (generation !== this._generation) return;
            this._answers[slot] = (answer != null && answer.signatures != null && $eq.collections.count(answer.signatures) > 0) ? answer : null;
            this._owed--;
            this.apply();
        });
    }

    apply() {
        let first: CodeSignatureHelp | null = null;
        for (const answer of this._answers) {
            if (answer == null) continue;
            first = answer;
            break;
        }
        if (first == null) {
            if (this._owed > 0) return;
            this._request = null;
            if (this._help == null) return;
            this._help = null;
            this._signature = 0;
            this._chosen = false;
            this.changed?.();
            return;
        }
        if (this._owed === 0) this._request = null;
        let shown: any; 
        let keep = this._chosen && (shown = this._help) != null && $eq.collections.count(shown.signatures) === $eq.collections.count(first.signatures);
        if (!keep) {
            this._signature = Math.max(0, Math.min($eq.collections.count(first.signatures) - 1, first.activeSignature));
            this._chosen = false;
        }
        this._help = first;
        this.changed?.();
    }

    toString(): string {
        return 'eQuantic.UI.Code.CodeSignatureHelpSession';
    }
}

