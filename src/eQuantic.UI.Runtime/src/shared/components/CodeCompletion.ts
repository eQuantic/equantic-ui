import { $eq, CancellationToken, CancellationTokenSource, CodeCompletionAnswer, CodeCompletionContext, CodeCompletionList, CodeCompletionMatch, CodeCompletionOffer, CodeCompletionTriggerValue, CodeDocument, CodeEditorController, CodeFuzzyMatch, CodePosition, CodeRange, UiDispatcher } from "../runtime-exports";

export class CodeCompletion {
    constructor(editor: CodeEditorController) {
        this._editor = null!;
        this._answers = [];
        this._candidates = [];
        this._groups = 0;
        this._items = [];
        this._shown = [];
        this._selected = -1;
        this._active = false;
        this._start = CodePosition.$zero();
        this._openedEmpty = false;
        this._askedWord = '';
        this._generation = 0;
        this._request = null;
        this._list = null;
        this.providers = [];
        this.opensAsYouType = true;
        this.pageSize = 12;
        this.changed = null;
        this.failed = null;
        this._editor = editor;
        editor.selectionChanged = $eq.delegates.combine(editor.selectionChanged, (_) => this.follow());
    }

    _editor!: CodeEditorController;
    _answers!: CodeCompletionAnswer[];
    _candidates!: CodeCompletionOffer[];
    _groups!: number;
    _items!: CodeCompletionMatch[];
    _shown!: CodeCompletionOffer[];
    _selected!: number;
    _active!: boolean;
    _start!: CodePosition;
    _openedEmpty!: boolean;
    _askedWord!: string;
    _generation!: number;
    _request!: CancellationTokenSource | null;
    _list!: CancellationTokenSource | null;
    providers!: any[];
    opensAsYouType!: boolean;
    pageSize!: number;
    changed!: (() => void) | null;
    failed!: ((exception: Error) => void) | null;

    get isOpen(): boolean {
        return this._active && this._items.length > 0;
    }

    get isLoading(): boolean {
        return this._active && this._answers.length === 0;
    }

    get items(): CodeCompletionMatch[] {
        return this._items;
    }

    get selected(): number {
        return this._selected;
    }

    get start(): CodePosition {
        return this._start;
    }

    get acceptChangesText(): boolean {
        if (!this.isOpen || this._selected < 0) return false;
        let offer = this._shown[this._selected];
        return this._editor.document.textIn(this.rangeOf(offer)) !== (offer.item.insertText ?? offer.item.label);
    }

    invoke() {
        if (this._editor.readOnly || this.providers.length === 0) return false;
        this.open(this.wordStart(this._editor.caret));
        this.ask('invoked', null, this.providers.slice());
        return true;
    }

    typed(typed: string) {
        if (this._editor.readOnly || this._active || this.providers.length === 0 || !this._editor.selection.isEmpty) return;
        let triggered: any[] = [];
        for (const provider of this.providers) {
            if (provider.triggerCharacters.includes(typed)) triggered.push(provider);
        }
        if (triggered.length > 0) {
            this.open(this._editor.caret);
            this.ask('character', typed, triggered);
            return;
        }
        if (!this.opensAsYouType || !CodeDocument.isWordChar(typed)) return;
        let caret = this._editor.caret;
        let start = this.wordStart(caret);
        let first = this._editor.document.line(caret.line)[start.column];
        if (!(/^\p{L}$/u.test(first)) && first !== '_') return;
        if (this.inCommentOrString(caret)) return;
        this.open(start);
        this.ask('typing', typed, this.providers.slice());
    }

    open(start: CodePosition) {
        this.close(false);
        this._active = true;
        this._start = start;
        this._openedEmpty = $eq.equals(start, this._editor.caret);
        this._list = $eq.cancellation.source();
        this.changed?.();
    }

    async ask(trigger: CodeCompletionTriggerValue, character: string | null, providers: any[]) {
        let generation = ++this._generation;
        this.cancel(this._request);
        let cancellation = $eq.cancellation.source();
        this._request = cancellation;
        let document = this._editor.document;
        let position = this._editor.caret;
        let $n14: any; 
        let context = ($n14 = new CodeCompletionContext(trigger, this._editor.highlighter.language), $n14.character = character, $n14);
        this._askedWord = this.word();
        let asked = [];
        let errors: Error[] = [];
        for (const provider of providers) {
            try {
                asked.push(provider.completeAsync(document, position, context, cancellation.token));
            } catch (error: any) {
                asked.push(null);
                errors.push(error);
            }
        }
        let answers: CodeCompletionAnswer[] = [];
        for (let i = 0; i < providers.length; i++) {
            let list = CodeCompletionList.empty;
            let answer: any; 
            if ((answer = asked[i]) != null) {
                try {
                    list = await answer;
                } catch ($e: any) {
                    let error = $e;
                    if ($eq.exceptions.is($e, 'System.OperationCanceledException') && $eq.exceptions.filter(() => cancellation.isCancellationRequested)) {} else {
                        errors.push(error);
                    }
                }
            }
            answers.push(new CodeCompletionAnswer(providers[i], document.line(position.line).length, list));
        }
        CodeCompletion.onUiThread(() => {
            for (const error of errors) this.failed?.(error);
            this.deliver(generation, answers);
        });
    }

    deliver(generation: number, answers: CodeCompletionAnswer[]) {
        if (generation !== this._generation || !this._active) return;
        for (const answer of answers) {
            let at = this._answers.findIndex((known) => (known.provider === answer.provider));
            if (at >= 0) this._answers[at] = answer; else this._answers.push(answer);
        }
        if (this._answers.every((answer) => answer.offers.length === 0 && !answer.isIncomplete)) {
            this.close(true);
            return;
        }
        this.gather();
        this.filter();
        this.askAgainIfIncomplete();
        this.resolveSelected();
        this.changed?.();
    }

    follow() {
        if (!this._active) return;
        let caret = this._editor.caret;
        if (!this._editor.selection.isEmpty || caret.line !== this._start.line || caret.column < this._start.column || caret.column === this._start.column && !this._openedEmpty) {
            this.close(true);
            return;
        }
        let word = this.word();
        for (const c of word.split('')) {
            if (CodeDocument.isWordChar(c)) continue;
            this.close(true);
            return;
        }
        if (this._answers.length === 0) return;
        this.filter();
        this.askAgainIfIncomplete();
        this.resolveSelected();
        this.changed?.();
    }

    askAgainIfIncomplete() {
        if (this.word() === this._askedWord) return;
        let incomplete: any[] = [];
        for (const answer of this._answers) {
            if (answer.isIncomplete) incomplete.push(answer.provider);
        }
        if (incomplete.length > 0) this.ask('incomplete', null, incomplete);
    }

    gather() {
        this._candidates = [];
        let groups: any = $eq.collections.dictionary();
        for (const answer of this._answers) {
            for (const offer of answer.offers) {
                let item = offer.item;
                let key = item.label.length + ':' + item.label + (item.insertText ?? item.label);
                let group: any; 
                if (!(groups.has(key) ? ((group = groups.get(key)), true) : ((group = 0), false))) {
                    group = groups.size;
                    $eq.mapSet(groups, key, group);
                }
                offer.group = group;
                offer.sortKey = (item.sortText ?? item.label).toLowerCase();
                this._candidates.push(offer);
            }
        }
        this._groups = groups.size;
    }

    filter() {
        let line = this._editor.document.line(this._start.line);
        let caret = this._editor.caret;
        let word = this.word();
        let matches: CodeCompletionMatch[] = [];
        let offers: CodeCompletionOffer[] = [];
        let listed = new Array(this._groups).fill(false);
        for (const offer of this._candidates) {
            if (listed[offer.group]) continue;
            let item = offer.item;
            let typed = word;
            let replacing: any; 
            if ((replacing = item.replacing) != null && replacing.start.line === caret.line && replacing.start.column !== this._start.column && replacing.start.column <= caret.column) typed = $eq.text.substring(line, replacing.start.column, caret.column - replacing.start.column);
            let filter = item.filterText ?? item.label;
            let match: any; 
            if (!((match = CodeFuzzyMatch.of(typed, filter)) != null)) continue;
            listed[offer.group] = true;
            let highlights = item.filterText == null || item.filterText === item.label ? match.positions : CodeFuzzyMatch.of(typed, item.label, true)?.positions ?? [];
            matches.push(new CodeCompletionMatch(item, match.score, highlights));
            offers.push(offer);
        }
        let order: number[] = [];
        for (let i = 0; i < matches.length; i++) order.push(i);
        $eq.collections.listSortBy(order, (a: number, b: number) => {
            if (matches[a].score !== matches[b].score) return matches[b].score - matches[a].score;
            let byKey = $eq.text.compare(offers[a].sortKey, offers[b].sortKey, 'ordinal');
            if (byKey !== 0) return byKey;
            let byLabel = $eq.text.compare(matches[a].item.label, matches[b].item.label, 'ordinal');
            return byLabel !== 0 ? byLabel : a - b;
        }, 'System.Comparison`1[System.Int32]');
        this._items = [];
        this._shown = [];
        for (const i of order) {
            this._items.push(matches[i]);
            this._shown.push(offers[i]);
        }
        this._selected = this._items.length > 0 ? 0 : -1;
        for (let i = 0; i < this._items.length && this._items[i].score === this._items[0].score; i++) {
            if (!this._items[i].item.preselect) continue;
            this._selected = i;
            break;
        }
    }

    move(delta: number) {
        if (!this.isOpen || this._items.length < 2) return false;
        let count = this._items.length;
        this._selected = $eq.num.intRem($eq.num.intRem(this._selected + delta, count) + count, count);
        this.resolveSelected();
        this.changed?.();
        return true;
    }

    movePage(pages: number) {
        if (!this.isOpen || this._items.length < 2) return false;
        this._selected = Math.max(0, Math.min(this._items.length - 1, this._selected + pages * this.pageSize));
        this.resolveSelected();
        this.changed?.();
        return true;
    }

    select(index: number) {
        if (!this.isOpen || index < 0 || index >= this._items.length) return false;
        this._selected = index;
        this.resolveSelected();
        this.changed?.();
        return true;
    }

    accept() {
        if (!this.isOpen || this._selected < 0) return false;
        let offer = this._shown[this._selected];
        let range = this.rangeOf(offer);
        let text = offer.item.insertText ?? offer.item.label;
        this.close(true);
        if (this._editor.document.textIn(range) === text) {
            this._editor.selection = new CodeRange(range.end);
            return true;
        }
        this._editor.apply(range, text);
        return true;
    }

    acceptsOn(typed: string) {
        let characters: any; 
        return this.isOpen && this._selected >= 0 && (characters = this._shown[this._selected].item.commitCharacters) != null && characters.includes(typed);
    }

    dismiss() {
        if (!this._active) return false;
        this.close(true);
        return true;
    }

    close(raise: boolean) {
        this._generation++;
        let request = this._request;
        let list = this._list;
        this._request = null;
        this._list = null;
        let was = this._active;
        this._active = false;
        this._answers.splice(0);
        this._candidates = [];
        this._groups = 0;
        this._items = [];
        this._shown = [];
        this._selected = -1;
        this.cancel(request);
        this.cancel(list);
        if (raise && was) this.changed?.();
    }

    cancel(source: CancellationTokenSource | null) {
        if (source == null) return;
        try {
            source.cancel();
        } catch (error: any) {
            this.failed?.(error);
        }
    }

    resolveSelected() {
        if (this._selected < 0 || this._list == null) return;
        let offer = this._shown[this._selected];
        if (offer.resolving) return;
        offer.resolving = true;
        this.resolve(offer, this._list.token);
    }

    async resolve(offer: CodeCompletionOffer, cancellation: CancellationToken) {
        let resolved = null;
        try {
            resolved = await offer.answer.provider.resolveAsync(offer.item, cancellation);
        } catch ($e: any) {
            let error = $e;
            if ($eq.exceptions.is($e, 'System.OperationCanceledException') && $eq.exceptions.filter(() => cancellation.isCancellationRequested)) {
                return;
            } else {
                CodeCompletion.onUiThread(() => this.failed?.(error));
                return;
            }
        }
        CodeCompletion.onUiThread(() => {
            if (!this._active || (resolved === offer.item)) return;
            offer.item = resolved;
            let at = $eq.collections.indexOf(this._shown, offer, 'item');
            if (at < 0) return;
            this._items[at] = $eq.withPatch(this._items[at], { item: resolved });
            this.changed?.();
        });
    }

    word() {
        let caret = this._editor.caret;
        if (caret.line !== this._start.line || caret.column < this._start.column) return '';
        return $eq.text.substring(this._editor.document.line(caret.line), this._start.column, caret.column - this._start.column);
    }

    wordStart(caret: CodePosition) {
        let line = this._editor.document.line(caret.line);
        let column = caret.column;
        while (column > 0 && CodeDocument.isWordChar(line[column - 1])) column--;
        return new CodePosition(caret.line, column);
    }

    rangeOf(offer: CodeCompletionOffer) {
        let caret = this._editor.caret;
        let line = this._editor.document.line(caret.line);
        let replacing: any; 
        if ((replacing = offer.item.replacing) != null && replacing.start.line === caret.line && replacing.end.line === caret.line && replacing.start.column <= caret.column) {
            let fromLineEnd = Math.max(0, offer.answer.askedLineLength - replacing.end.column);
            return new CodeRange(replacing.start, new CodePosition(caret.line, Math.max(caret.column, line.length - fromLineEnd)));
        }
        return new CodeRange(this._start, caret);
    }

    inCommentOrString(caret: CodePosition) {
        if (caret.column === 0) return false;
        for (const token of this._editor.highlighter.tokensFor(this._editor.document, caret.line)) {
            if (token.start <= caret.column - 1 && caret.column - 1 < token.end) return (token.kind === 'comment' || token.kind === 'string');
        }
        return false;
    }

    static onUiThread(work: () => void) {
        let dispatcher: any; 
        if (((UiDispatcher.current != null && UiDispatcher.current.isOnUiThread === false) && (dispatcher = UiDispatcher.current, true))) dispatcher.post(work); else work();
    }
}

