import { $CodeCompletionTriggerValue, $eq, CancellationToken, CancellationTokenSource, CodeChange, CodeChoiceProvider, CodeCompletionAnswer, CodeCompletionContext, CodeCompletionItem, CodeCompletionList, CodeCompletionMatch, CodeCompletionOffer, CodeCompletionRequest, CodeDocument, CodeEdit, CodeEditorController, CodeFuzzyMatch, CodePosition, CodeRange, ProviderRequest, UiThread } from "../runtime-exports";

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
        this._openedBy = 'invoked';
        this._start = CodePosition.$zero();
        this._openedEmpty = false;
        this._choosing = false;
        this._edits = [];
        this._requests = [];
        this._list = null;
        this.providers = [];
        this.opensAsYouType = true;
        this.pageSize = 12;
        this.changed = null;
        this.failed = null;
        this._editor = editor;
        editor.changed = $eq.delegates.combine(editor.changed, $eq.delegates.bound(this, this.carry));
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
    _openedBy!: $CodeCompletionTriggerValue;
    _start!: CodePosition;
    _openedEmpty!: boolean;
    _choosing!: boolean;
    _edits!: CodeEdit[];
    _requests!: CodeCompletionRequest[];
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
        return this._active && $eq.query.any(this._requests, (request) => !request.answered);
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
        if (this._choosing || !(offer.item.snippet == null)) return true;
        return this._editor.document.textIn(this.rangeOf(offer)) !== (offer.item.insertText ?? offer.item.label);
    }

    invoke() {
        if (this._editor.readOnly || $eq.collections.count(this.providers) === 0) return false;
        this.open(this.wordStart(this._editor.caret), 'invoked', null, this.providers.slice());
        return true;
    }

    typed(typed: string) {
        if (this._editor.readOnly || this._active || $eq.collections.count(this.providers) === 0 || !this._editor.selection.isEmpty) return;
        let triggered: any[] = [];
        for (const provider of $eq.collections.sequenceWalk(this.providers)) {
            if (provider.triggerCharacters.includes(typed)) $eq.collections.listAdd(triggered, provider);
        }
        if (triggered.length > 0) {
            this.open(this._editor.caret, 'character', typed, triggered);
            return;
        }
        if (!this.opensAsYouType || !CodeDocument.isWordChar(typed)) return;
        let caret = this._editor.caret;
        let start = this.wordStart(caret);
        let first = this._editor.document.line(caret.line)[start.column];
        if (!(/^\p{L}$/u.test(first)) && first !== '_') return;
        if (this.inCommentOrString(caret)) return;
        this.open(start, 'typing', typed, this.providers.slice());
    }

    open(start: CodePosition, trigger: $CodeCompletionTriggerValue, character: string | null, providers: any[], choosing: boolean = false) {
        this.close(false);
        this._active = true;
        this._choosing = choosing;
        $eq.collections.listClear(this._edits);
        this._openedBy = trigger;
        this._start = start;
        this._openedEmpty = choosing || $eq.equals(start, this._editor.caret);
        this._list = $eq.cancellation.source();
        this.ask(trigger, character, providers, true);
    }

    offerChoices(at: CodeRange, options: string[]) {
        if (this._editor.readOnly) return;
        this.open(at.start, 'invoked', null, [new CodeChoiceProvider(options)], true);
    }

    dropChoices() {
        if (this._choosing) this.close(true);
    }

    ask(trigger: $CodeCompletionTriggerValue, character: string | null, providers: any[], announce: boolean) {
        let document = this._editor.document;
        let position = this._editor.caret;
        let $n0: CodeCompletionContext; 
        let context = ($n0 = new CodeCompletionContext(trigger, this._editor.highlighter.language), $n0.character = character, $n0);
        let asked = this._edits.length;
        let word = this.word();
        let requests: CodeCompletionRequest[] = [];
        for (const provider of $eq.collections.listWalk(providers)) {
            for (let i = this._requests.length - 1 | 0; i >= 0; i = i - 1 | 0) {
                if (!(this._requests[i].provider === provider)) continue;
                let stale = this._requests[i];
                $eq.collections.listRemoveAt(this._requests, i);
                this.cancel(stale.cancellation);
            }
            let request = new CodeCompletionRequest(provider, word);
            $eq.collections.listAdd(this._requests, request);
            $eq.collections.listAdd(requests, request);
        }
        if (announce) this.changed?.();
        for (const request of $eq.collections.listWalk(requests)) this.askOne(request, document, position, context, asked);
    }

    async askOne(request: CodeCompletionRequest, document: CodeDocument, position: CodePosition, context: CodeCompletionContext, asked: number) {
        let list = CodeCompletionList.empty;
        let failure: Error | null = null;
        try {
            list = await request.provider.completeAsync(document, position, context, request.cancellation.token);
        } catch ($e: any) {
            let error = $e;
            if ($eq.exceptions.is($e, 'System.OperationCanceledException') && $eq.exceptions.filter(() => request.cancellation.isCancellationRequested)) {} else {
                failure = error;
            }
        }
        UiThread.run(() => {
            if (!(failure == null)) this.failed?.(failure);
            this.deliver(request, new CodeCompletionAnswer(request.provider, request.word, asked, list));
        });
    }

    deliver(request: CodeCompletionRequest, answer: CodeCompletionAnswer) {
        if (!this._active || request.answered || $eq.collections.indexOf(this._requests, request, 'item') < 0) return;
        request.answered = true;
        let known = this._answers.findIndex((held) => (held.provider === answer.provider));
        if (known >= 0) $eq.collections.listRemoveAt(this._answers, known);
        let rank = this.rankOf(answer.provider);
        let at = 0;
        while (at < this._answers.length && this.rankOf(this._answers[at].provider) <= rank) at = at + 1 | 0;
        $eq.collections.listInsert(this._answers, at, answer);
        if ($eq.query.all(this._answers, (held) => held.offers.length === 0 && !held.isIncomplete) && !this.isLoading) {
            this.close(true);
            return;
        }
        let selected = this._selected >= 0 ? CodeCompletion.entryOf(this._shown[this._selected].item) : null;
        this.gather();
        this.filter();
        if (!(selected == null)) {
            for (let i = 0; i < this._shown.length; i = i + 1 | 0) {
                if (CodeCompletion.entryOf(this._shown[i].item) !== selected) continue;
                this._selected = i;
                break;
            }
        }
        this.askAgainWhereTheWordLeftTheAnswer();
        this.resolveSelected();
        this.changed?.();
    }

    rankOf(provider: any) {
        for (let i = 0; i < $eq.collections.count(this.providers); i = i + 1 | 0) if (($eq.collections.item(this.providers, i) === provider)) return i;
        return $eq.collections.count(this.providers);
    }

    follow() {
        if (!this._active) return;
        let caret = this._editor.caret;
        if (!this._editor.selection.isEmpty && !this._choosing || caret.line !== this._start.line || caret.column < this._start.column || caret.column === this._start.column && !this._openedEmpty) {
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
        this.askAgainWhereTheWordLeftTheAnswer();
        this.resolveSelected();
        this.changed?.();
    }

    askAgainWhereTheWordLeftTheAnswer() {
        let word = this.word();
        let incomplete: any[] = [];
        let left: any[] = [];
        for (const answer of $eq.collections.listWalk(this._answers)) {
            if (this.lastAskedWord(answer.provider) === word) continue;
            if (answer.isIncomplete) $eq.collections.listAdd(incomplete, answer.provider); else if (!$eq.text.startsWith(word, answer.askedWord, 'ordinal')) $eq.collections.listAdd(left, answer.provider);
        }
        if (incomplete.length > 0) this.ask('incomplete', null, incomplete, false);
        if (left.length > 0) this.ask(this._openedBy === 'character' ? 'invoked' : this._openedBy, null, left, false);
    }

    lastAskedWord(provider: any) {
        for (const request of $eq.collections.listWalk(this._requests)) {
            if ((request.provider === provider)) return request.word;
        }
        return null;
    }

    gather() {
        this._candidates = [];
        let groups: any = $eq.collections.dictionary<string, number>();
        for (const answer of $eq.collections.listWalk(this._answers)) {
            for (const offer of $eq.collections.listWalk(answer.offers)) {
                let item = offer.item;
                let key = CodeCompletion.entryOf(item);
                let group: any; 
                if (!(groups.has(key) ? ((group = groups.get(key)), true) : ((group = 0), false))) {
                    group = groups.size;
                    $eq.mapSet(groups, key, group);
                }
                offer.group = group;
                offer.sortKey = $eq.text.toLower((item.sortText ?? item.label), true);
                $eq.collections.listAdd(this._candidates, offer);
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
        for (const offer of $eq.collections.listWalk(this._candidates)) {
            if (listed[offer.group]) continue;
            let item = offer.item;
            let typed = word;
            let asked: any; 
            if ((asked = item.replacing) != null) {
                let replacing = offer.answer.carried(asked, this._edits);
                if (replacing.start.line === caret.line && replacing.start.column !== this._start.column && replacing.start.column <= caret.column) typed = $eq.text.substring(line, replacing.start.column, caret.column - replacing.start.column | 0);
            }
            let filter = item.filterText ?? item.label;
            let match: any; 
            if (!((match = CodeFuzzyMatch.of(typed, filter)) != null)) continue;
            listed[offer.group] = true;
            let highlights = item.filterText == null || item.filterText === item.label ? match.positions : CodeFuzzyMatch.of(typed, item.label, true)?.positions ?? [];
            $eq.collections.listAdd(matches, new CodeCompletionMatch(item, match.score, highlights));
            $eq.collections.listAdd(offers, offer);
        }
        let order: number[] = [];
        for (let i = 0; i < matches.length; i = i + 1 | 0) $eq.collections.listAdd(order, i);
        $eq.collections.listSortBy(order, (a: number, b: number) => {
            if (matches[a].score !== matches[b].score) return matches[b].score - matches[a].score | 0;
            let byKey = $eq.text.compare(offers[a].sortKey, offers[b].sortKey, 'ordinal');
            if (byKey !== 0) return byKey;
            let byLabel = $eq.text.compare(matches[a].item.label, matches[b].item.label, 'ordinal');
            return byLabel !== 0 ? byLabel : a - b | 0;
        }, 'System.Comparison`1[System.Int32]');
        this._items = [];
        this._shown = [];
        for (const i of $eq.collections.listWalk(order)) {
            $eq.collections.listAdd(this._items, matches[i]);
            $eq.collections.listAdd(this._shown, offers[i]);
        }
        this._selected = this._items.length > 0 ? 0 : -1;
        for (let i = 0; i < this._items.length && this._items[i].score === this._items[0].score; i = i + 1 | 0) {
            if (!this._items[i].item.preselect) continue;
            this._selected = i;
            break;
        }
    }

    static entryOf(item: CodeCompletionItem) {
        let snippet: any; 
        return $eq.text.format(item.label.length, null, undefined, undefined, 'int32') + ':' + item.label + ((snippet = item.snippet) != null ? 's' + snippet.body : 't' + (item.insertText ?? item.label));
    }

    move(delta: number) {
        if (!this.isOpen || this._items.length < 2) return false;
        let count = this._items.length;
        this._selected = $eq.num.intRem($eq.num.intRem(this._selected + delta | 0, count) + count | 0, count);
        this.resolveSelected();
        this.changed?.();
        return true;
    }

    movePage(pages: number) {
        if (!this.isOpen || this._items.length < 2) return false;
        this._selected = Math.max(0, Math.min(this._items.length - 1 | 0, this._selected + Math.imul(pages, this.pageSize) | 0));
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
        if (this._choosing) {
            let option = offer.item.label;
            this.close(true);
            return this._editor.snippet.choose(option);
        }
        let range = this.rangeOf(offer);
        let text = offer.item.insertText ?? offer.item.label;
        this.close(true);
        let caret = this._editor.caret;
        let snippet: any; 
        if ((snippet = offer.item.snippet) != null && range.start.line === caret.line && range.end.line === caret.line) return this._editor.insertSnippetAround(snippet, caret.column - range.start.column | 0, range.end.column - caret.column | 0);
        if (this._editor.hasOtherCarets && range.start.line === caret.line && range.end.line === caret.line) return this._editor.replaceAroundEveryCaret(caret.column - range.start.column | 0, range.end.column - caret.column | 0, text);
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
        let requests = this._requests.slice();
        $eq.collections.listClear(this._requests);
        let list = this._list;
        this._list = null;
        let was = this._active;
        this._active = false;
        this._choosing = false;
        $eq.collections.listClear(this._edits);
        $eq.collections.listClear(this._answers);
        this._candidates = [];
        this._groups = 0;
        this._items = [];
        this._shown = [];
        this._selected = -1;
        for (const request of $eq.collections.listWalk(requests)) this.cancel(request.cancellation);
        this.cancel(list);
        if (raise && was) this.changed?.();
    }

    cancel(source: CancellationTokenSource | null) {
        return ProviderRequest.cancel(source, this.failed);
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
                UiThread.run(() => this.failed?.(error));
                return;
            }
        }
        UiThread.run(() => {
            if (!this._active || (resolved === offer.item)) return;
            offer.item = resolved;
            let at = $eq.collections.indexOf(this._shown, offer, 'item');
            if (at < 0) return;
            $eq.collections.listSet(this._items, at, $eq.withPatch(this._items[at], { item: resolved }));
            this.changed?.();
        });
    }

    word() {
        if (this._choosing && !this._editor.selection.isEmpty) return '';
        let caret = this._editor.caret;
        if (caret.line !== this._start.line || caret.column < this._start.column) return '';
        return $eq.text.substring(this._editor.document.line(caret.line), this._start.column, caret.column - this._start.column | 0);
    }

    wordStart(caret: CodePosition) {
        let line = this._editor.document.line(caret.line);
        let column = caret.column;
        while (column > 0 && CodeDocument.isWordChar(line[column - 1 | 0])) column = column - 1 | 0;
        return new CodePosition(caret.line, column);
    }

    rangeOf(offer: CodeCompletionOffer) {
        let caret = this._editor.caret;
        let asked: any; 
        if ((asked = offer.item.replacing) != null) {
            let replacing = offer.answer.carried(asked, this._edits);
            if (replacing.start.line === caret.line && replacing.end.line === caret.line && replacing.start.column <= caret.column) return new CodeRange(replacing.start, CodePosition.opLessThan(replacing.end, caret) ? caret : replacing.end);
        }
        return new CodeRange(this._start, caret);
    }

    carry(change: CodeChange) {
        if (!this._active) return;
        for (const edit of $eq.collections.sequenceWalk(change.edits)) {
            this._start = edit.carryAnchor(this._start);
            $eq.collections.listAdd(this._edits, edit);
        }
    }

    inCommentOrString(caret: CodePosition) {
        if (caret.column === 0) return false;
        for (const token of $eq.collections.sequenceWalk(this._editor.highlighter.tokensFor(this._editor.document, caret.line))) {
            if (token.start <= (caret.column - 1 | 0) && (caret.column - 1 | 0) < token.end) return (($0) => ($0 === 'comment' || $0 === 'string'))(token.kind);
        }
        return false;
    }

    toString(): string {
        return 'eQuantic.UI.Code.CodeCompletion';
    }
}

